/*
-----------------------------------------------------------------------------------
TRACER FOUNDATION -
Toolset for Realtime Animation, Collaboration & Extended Reality

Copyright (c) 2024 Filmakademie Baden-Wuerttemberg, Animationsinstitut R&D Labs
https://research.animationsinstitut.de/tracer 
https://github.com/FilmakademieRnd/TRACER

TRACER FOUNDATION is a development by Filmakademie Baden-Wuerttemberg,
Animationsinstitut R&D Labs in the scope of the EU funded project
MAX-R (101070072) and funding on the own behalf of Filmakademie Baden-Wuerttemberg.
Former EU projects Dreamspace (610005) and SAUCE (780470) have inspired the
TRACER FOUNDATION development.

This program is distributed in the hope that it will be useful, but WITHOUT
ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
FOR A PARTICULAR PURPOSE. See the MIT License for more details.
You should have received a copy of the MIT License along with this program;
if not go to https://opensource.org/licenses/MIT
-----------------------------------------------------------------------------------
*/

//! @file "EvaluationHelper.cs"
//! @brief definition of TRACER layer evaluation helpers class.
//! @summary this helper will execute engine (unity) specific stuff to check what we would hit as a specific position
//! @author Thomas Krüger
//! @version 0
//! @date 28.04.2026


using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;

namespace tracer{

    public class EvaluationHelper : MonoBehaviour{
        
        #region SINGLETON
        private static EvaluationHelper _instance;
        private static readonly object _lock = new object();

        public static EvaluationHelper Instance{
            get{
                // Thread-safe lock for extra fail-safeness
                lock (_lock){
                    if (_instance == null){
                        // Look for the instance in the scene
                        _instance = (EvaluationHelper)FindFirstObjectByType(typeof(EvaluationHelper));

                        // If it doesn't exist, create a new GameObject to host it
                        if (_instance == null){
                            // 2. Load from Resources/Prefabs/...
                            var prefab = Resources.Load<GameObject>("Prefabs/"+typeof(EvaluationHelper).ToString());
                            
                            if (prefab != null){
                                _instance = Instantiate(prefab).GetComponent<EvaluationHelper>();
                            }else{
                                // Fallback: Create from scratch if prefab is missing
                                GameObject singletonObject = new GameObject();
                                _instance = singletonObject.AddComponent<EvaluationHelper>();
                                singletonObject.name = typeof(EvaluationHelper).ToString() + " (Singleton)";

                                // Make it persistent across scenes
                                DontDestroyOnLoad(singletonObject);
                            }
                        }
                    }
                    return _instance;
                }
            }
        }

        private void Awake(){
            if (_instance == null){
                _instance = this;
                
                DontDestroyOnLoad(gameObject);
            }else if (_instance != this){
                // If an instance already exists and it's not me, destroy me.
                Debug.LogWarning($"[Singleton] Instance of {typeof(EvaluationHelper)} already exists. Destroying duplicate on {gameObject.name}");
                Destroy(gameObject);
            }
        }
        #endregion

        #region INIT

        //!
        //! tracer core reference
        //!
        private Core core;

        //!
        //! Init our evaluation helper - via UnityInputModule
        //!
        public void Init(Core _core){
            core = _core;
            
            mainCam = Camera.main;
            selectableLayer = LayerMask.GetMask(CullingLayerName);

            core.getManager<SceneManager>().sceneReady += OnSceneReady;
        }

        // gets called when sceneManager emits a sceneReady event
        public void OnSceneReady(object sender, EventArgs e){  
            ModifyMaterialsForIDs();

            switch (calcBehaviour) {
                case CalculationBehaviourEnum.ongoing:
                    core.updateEvent += OnCoreUpdateEvent;
                    break;
                case CalculationBehaviourEnum.onInput:
                case CalculationBehaviourEnum.onInputAndCamChange:
                    //core.getManager<InputManager>().onAnyInputDetection += RenderUpdate;
                    core.getManager<InputManager>().anyInputEvent += OnAnyInputEvent;
                    break;
            }

            Debug.Log("<color=green>OnSceneReady within EvaluationHelper initialized with settings <b>"+calcBehaviour.ToString()+"</b></color>");
        }

        private void OnDestroy(){
            if(core == null) //never initialized or it's a duplicate
                return;

            core.getManager<SceneManager>().sceneReady -= OnSceneReady;

            if(m_materials == null) //scene was never ready
                return;

            switch (calcBehaviour) {
                case CalculationBehaviourEnum.ongoing:
                    core.updateEvent -= OnCoreUpdateEvent;
                    break;
                case CalculationBehaviourEnum.onInput:
                case CalculationBehaviourEnum.onInputAndCamChange:
                    //core.getManager<InputManager>().onAnyInputDetection -= RenderUpdate;
                    core.getManager<InputManager>().anyInputEvent -= OnAnyInputEvent;
                    break;
            }


            if (gpuTexture != null) gpuTexture.Release();
            if (cpuData.IsCreated) cpuData.Dispose();
        }

        #endregion


        #region Material Modifications
        //! 
        //! Gets a cached adjusted material or creates a new one based on the specified material.
        //! Selectable materials are identical to the specified material except that they have the
        //! SelectableType tag set so they are rendered in the replacement pass used to render
        //! selectable ids.
        //! 
        //! Note that all adjusted materials are destroyed when the selection manager is destroyed!
        //! 
        //! @param material The material to be changed for selection rendering.
        //! @return An adjusted m_instance of the specified material with the selectable tag set.
        //!
        private Material GetSelectableMaterial(Material material){
            if (!m_materials.TryGetValue(material, out Material selectableMaterial)) {
                selectableMaterial = UnityEngine.Object.Instantiate(material);
                #if UNITY_EDITOR
                selectableMaterial.name += "_ModifiedForIDE";
                #endif
                selectableMaterial.SetOverrideTag(SelectableTypeName, SelectableShaderTagValue);
                m_materials.Add(material, selectableMaterial);
            }

            return selectableMaterial;
        }

        //!
        //! Function that creates a new property block for all renderable
        //! objects in the scene to set the object ID as a shader parameter.
        //! This function is called after the scene has been loaded.
        //!
        private void ModifyMaterialsForIDs(){  
            objectIdShader = Resources.Load<Shader>("Shader/SelectableId");
            m_materials = new Dictionary<Material, Material>();
            m_selectableIdPropertyId = Shader.PropertyToID(SelectableIdPropertyName);

            MaterialPropertyBlock m_properties = new MaterialPropertyBlock(); //Re-used property block used to set selectable _id.
            Transform root = core.getManager<SceneManager>().scnRoot.transform;

            int sceneObjectMatsChanged = 0;
            int nonSceneObjectMatsChanged = 0;
            foreach (Renderer renderer in core.getManager<SceneManager>().scnRoot.GetComponentsInChildren<Renderer>()){
                SceneObject sceneObject = renderer.gameObject.GetComponent<SceneObject>();
                short soID = 0;
                byte sceneID = 0;
                if (sceneObject){
                    if ((sceneObject is SceneObjectCamera) || (sceneObject is SceneObjectLight))
                        continue;
                    soID = sceneObject._id;  
                    sceneID = sceneObject._sceneID;
                }else{
                    Transform t = renderer.transform;
                    
                    while (t.parent != root){
                        if (t.parent.CompareTag("editable")){
                            sceneObject = t.parent.GetComponent<SceneObject>();
                            if (sceneObject){
                                soID = sceneObject._id;  
                                sceneID = sceneObject._sceneID;
                                break;  //break was outside of this!
                            }
                        }else{
                            t = t.parent;
                        }
                    }
                }

                // use different scene id thingy for renderer-only objects (generic save as object + soID) in seperate manager
                // if (!sceneObject) {
                //     sceneID = 10;   //be careful! [REVIEW] 
                //     soID = m_sceneManager.AddNonSceneObject((object)renderer.gameObject);
                //     nonSceneObjectMatsChanged++;
                // }

                //ENCODE sceneID and sceneObjectID into a unity-color
                Color32 packedId = new Color32(
                    0,
                    sceneID,
                    (byte)(soID >> (8)),
                    (byte)(soID >> (0)) 
                );

                m_properties.Clear();

                // Keep existing changed properties.
                if (renderer.HasPropertyBlock())
                    renderer.GetPropertyBlock(m_properties);

                m_properties.SetColor(m_selectableIdPropertyId, packedId);
                renderer.SetPropertyBlock(m_properties);
                renderer.sharedMaterial = GetSelectableMaterial(renderer.sharedMaterial);
                sceneObjectMatsChanged++;
            }
            Debug.Log("<color=green>IDExtractorModule modified <b>"+sceneObjectMatsChanged+"</b> sceneObject materials and <b>"+nonSceneObjectMatsChanged+"</b> non-so materials</color>");
        }
        #endregion


        #region RTX BEHAVIOUR
        //ID Array (cpuData) Creation

        private enum CalculationBehaviourEnum {
            ongoing = 0,                //calculate ongoing once a previous calc finishes
            onInput = 10,               //start calc on any input down - should be ready until the up event gets triggered
            onInputAndCamChange = 20    //does not take into account any object modification
        }

        private readonly CalculationBehaviourEnum calcBehaviour = CalculationBehaviourEnum.onInput;

        //!
        //! Name of the shader tag for the selection shader.
        //!
        private const string SelectableTypeName = "SelectableType";
        //!
        //! Value of the shader tag for the selection shader.
        //!
        private const string SelectableShaderTagValue = "Selectable";
        //!
        //! Name of the shader property holding the selectable _id.
        //!
        private const string SelectableIdPropertyName = "_SelectableId";
        //!
        //! Name of the layer we use for selectable object culling
        //! if multiple layers become possible, use array
        //!
        private const string CullingLayerName = "LodMixed";

        //!
        //! Divides the screen resolution for the rtx calculation
        //! Keep this small! e.g. 0.25 means 1/4th resolution
        //!
        private readonly float scaleDivisor = 0.25f;
        //!
        //! Name of LayerMask (CullingLayerName) we use as culling mask for the cam for more performance
        //!
        private LayerMask selectableLayer;
        //!
        //! The shader to be used for object ID rendering
        //!
        private Shader objectIdShader;
        //!
        //! The rtx used for the cpuData creation
        //!
        private RenderTexture gpuTexture;
        //!
        //! The color/ID data to be stored in the CPU texture.
        //! we could not outsource this into UIManager, because both types are unity dependent
        //! although we could write an interface and have our own MyColor32 type
        //!
        private NativeArray<Color32> cpuData;
        //!
        //! a reference to the mainCam to not search by tag via Camera.main
        //!
        private Camera mainCam;
        //!
        //! Tracked materials with selectable tag.
        //!
        private Dictionary<Material, Material> m_materials;
        //!
        //! Cached shader property _id of selectable _ids
        //!
        private int m_selectableIdPropertyId;
        //!
        //! Scaled width and height of rtx, also used to check if cam size has changed
        //!
        private int dataWidth, dataHeight;
        //!
        //! only request one per time
        //!
        private bool m_gpuReadbackRequested = false;

        //!
        //! Callback from TRACER _core when Unity calls it's render update
        //!
        private void OnCoreUpdateEvent(object sender, EventArgs e){
            UpdateIDTexture();
        }
        //!
        //! Callback via TRACER InputManager (by UnityInputModile) when any input was detected
        //! so we can tell the gpu fast enough to create a rtx to use
        //! check if it would be valid to use only for portion of screen, if input pos matters
        //!
        private void OnAnyInputEvent(object sender, InputManager.AnyEventArgs data){
            //only do so on start
            if(data.State == InputManager.InputState.Started)
                UpdateIDTexture();
        }

        //!
        //! Used setup render texture, render the object ID pass and copy
        //! it asyncron into a Color32 array. 
        //!
        private void UpdateIDTexture(){
            // ONLY trigger a new render if we aren't currently waiting for a readback to finish.
            if (m_gpuReadbackRequested){
                return;
            }

            CreateOrUpdateTexture();

            // Cache camera state
            RenderTexture oldRenderTexture  = mainCam.targetTexture;
            CameraClearFlags oldClearFlags  = mainCam.clearFlags;
            Color oldBackgroundColor        = mainCam.backgroundColor;
            RenderingPath oldRenderingPath  = mainCam.renderingPath;
            bool oldAllowMsaa               = mainCam.allowMSAA;
            int oldCullingMask              = mainCam.cullingMask; // Cache culling mask

            // Apply temporary state
            mainCam.targetTexture   = gpuTexture;
            mainCam.clearFlags      = CameraClearFlags.SolidColor;  // Make sure non-rendered pixels have _id zero.
            mainCam.backgroundColor = Color.clear;
            mainCam.renderingPath   = RenderingPath.Forward;        // No gbuffer required.
            mainCam.allowMSAA       = false;                        // Avoid interpolated colors.
            
            // OPTIMIZATION: Only render the layer(s) that have selectable objects!
            // not so much optimization here, since nearly every sceneobject is in "LodMixed"
            mainCam.cullingMask = selectableLayer; 

            // Perform the render
            mainCam.RenderWithShader(objectIdShader, SelectableTypeName);

            // Restore camera state
            mainCam.targetTexture   = oldRenderTexture;
            mainCam.clearFlags      = oldClearFlags;
            mainCam.backgroundColor = oldBackgroundColor;
            mainCam.renderingPath   = oldRenderingPath;
            mainCam.allowMSAA       = oldAllowMsaa;
            mainCam.cullingMask     = oldCullingMask;

            m_gpuReadbackRequested = true;
            
            // Request readback - see https://dev.to/alpenglow/unity-fast-pixel-reading-part-2-asyncgpureadback-4kgn for example implementation
            //Debug.Log("<color=blue>AsyncGPURequest started <b>"+Time.time.ToString("F2")+"</b> </color>");
            AsyncGPUReadback.Request(gpuTexture, 0, TextureFormat.RGBA32, OnCompleteAsyncGPUReadback);
        }

        private void CreateOrUpdateTexture() {
            int currentWidth = Mathf.Max(1, (int)(mainCam.pixelWidth * scaleDivisor));
            int currentHeight = Mathf.Max(1, (int)(mainCam.pixelHeight * scaleDivisor));

            if (gpuTexture == null || dataWidth != currentWidth || dataHeight != currentHeight){
                if (gpuTexture != null) {
                    gpuTexture.Release();
                    GameObject.Destroy(gpuTexture); // Prevent C# object memory leak
                }
                if (cpuData.IsCreated) cpuData.Dispose();

                dataWidth = currentWidth;
                dataHeight = currentHeight;

                int depthBits = mainCam.depthTextureMode == DepthTextureMode.None ? 16 : 0;
                gpuTexture = new RenderTexture(dataWidth, dataHeight, depthBits, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) {
                    filterMode = FilterMode.Point
                };
                cpuData = new NativeArray<Color32>(dataWidth * dataHeight, Allocator.Persistent);
            }
        }

        private void OnCompleteAsyncGPUReadback(AsyncGPUReadbackRequest request){
            if (request.hasError){
                Debug.LogError("GPU readback error");
                m_gpuReadbackRequested = false;
                return;
            }

            if (request.done && cpuData.IsCreated){ // Ensure array wasn't disposed during resize
                //Debug.Log("<color=green>AsyncGPURequest finished <b>"+Time.time.ToString("F2")+"</b> </color>");
                request.GetData<Color32>().CopyTo(cpuData);
            } 
            // else {
            //     Debug.Log("<color=yellow>AsyncGPURequest ended <b>"+Time.time.ToString("F2")+"</b> </color>");
            // }

            // Now that we have the data, we allow the Update loop to trigger the next render
            m_gpuReadbackRequested = false; 
        }
        #endregion


        #region EVALUATION
        //QUERY AND BUFFER
        //TODO: implement buffer - write class with pos, outcome, time (if too old query again)
        //      to use as buffer if we request the same calculations for a position we already have

        public enum OperationLayer{
            UI2D = 10,
            UI3D = 20,
            SCENEOBJECT = 30,
            OTHER = 40
        }

        //!
        //! the object we hit in our last layer-to-operate evaluation (do not execute multiple times)
        //!
        private GameObject m_uiGameObjectWeHit, m_gameObjectWeHit, m_worldGameObjectWeHit;
        private SceneObject m_sceneObjectWeHit;
        //!
        //! the world position were a hit occured
        //!
        private Vector3 m_worldHitPos;

        //!
        //! use (input) position to check what layershould be used/would be hit
        //! @param screen-pos position we should use to check
        //! TODO: add buffer, add var to deny buffer
        //!
        public OperationLayer EvaluateOperationLayer(Vector2 screenPos){
            //if in buffer, return this
            if(Is2DUI(screenPos)){
                Debug.Log("OperationLayer <color=grey>2DUI</color>");
                return OperationLayer.UI2D;
            }else if(Is3DUI(screenPos)){
                Debug.Log("OperationLayer <color=grey>3DUI</color>");
                return OperationLayer.UI3D;
            }else if (IsSceneObject(screenPos) || IsSceneObjectAtPixel(screenPos)) {
                Debug.Log("OperationLayer <color=grey>SCENEOBJECT</color>");
                return OperationLayer.SCENEOBJECT;
            }else{
                Debug.Log("OperationLayer <color=grey>OTHER</color>");
                return OperationLayer.OTHER;
            }
        }

        //TODO: implement a buffering system for screenPos and all queries!

        public SceneObject EvaluateSceneObject(Vector2 screenPos){
            //how to implement an iterative selection of SceneObjects when clicking repeatingly at the same pos/obj?
            //  -> maybe within another "EvaluateSceneObjectsIterative"

            if (IsSceneObject(screenPos) || IsSceneObjectAtPixel(screenPos)) 
                return m_sceneObjectWeHit;

            return null;
        }

        // Evaluate and return a GameObject that was hit via a Physics Raycast
        public GameObject EvaluateGameObject(Vector2 screenPos) {
            if(Is3DUI(screenPos))
                return m_gameObjectWeHit;
            return null;
        }

        // Evaluate and return a Manipulator GameObject that was hit via a Physics Raycast at a different layer
        public GameObject EvaluateManipulator(Vector2 screenPos) {
            if(Is3DManipulator(screenPos))
                return m_gameObjectWeHit;
            return null;
        }

        // Evaluate and return a UI GameObject
        public GameObject EvaluateUIGameObject(Vector2 screenPos) {
            if(Is2DUI(screenPos))
                return m_uiGameObjectWeHit;
            return null;
        }

        //!
        //! returns true if pos is over any UI element
        //! (it goes over all raycaster in the scene - ideally that would be GraphicRaycaster from the 2D UI)
        //!
        //! @param pos position of the click/tap
        //!
        private bool Is2DUI(Vector2 pos){
            PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current){ position = pos };
            List<RaycastResult> m_raycastList = new List<RaycastResult>(5);
            EventSystem.current.RaycastAll(eventDataCurrentPosition, m_raycastList);
            if(m_raycastList.Count > 0) {
                m_uiGameObjectWeHit = m_raycastList[0].gameObject;
                m_worldGameObjectWeHit = null;
                return true;
            }
            return false;
        }

        //!
        //! returns true if pos is over any 3D manipulator object (layerMask 0 for Default)
        //! [REVIEW] should be seperate 3D-UI Layer
        //!
        private bool Is3DUI(Vector2 pos){
            bool hitObject = Physics.Raycast(mainCam.ScreenPointToRay(pos), out RaycastHit hitInfo, Mathf.Infinity, 1 << 0);
            if (hitObject) {
                m_gameObjectWeHit = hitInfo.transform.gameObject;
                m_worldGameObjectWeHit = null;
                m_worldHitPos = hitInfo.point;
                return true;
            }
            return false;
        }

        public Ray CreateScreenPointRay(Vector2 pos){ return mainCam.ScreenPointToRay(pos);}

        //!
        //! returns true if pos is over any 3D manipulator object (layerMask 5 for UI)
        //!
        private bool Is3DManipulator(Vector2 pos){
            bool hitObject = Physics.Raycast(mainCam.ScreenPointToRay(pos), out RaycastHit hitInfo, Mathf.Infinity, 1 << 5);
            if (hitObject) {
                m_gameObjectWeHit = hitInfo.transform.gameObject;
                m_worldGameObjectWeHit = null;
                m_worldHitPos = hitInfo.point;
                return true;
            }
            return false;
        }

        //!
        //! returns true if pos is over any 3d selectable object
        //! [REVIEW] will never hit, since there are no colliders (despite 3d ui gizmos) in the scene!
        //!
        private bool IsSceneObject(Vector2 pos){
            int layerMask = 1 << 5; //layer 5 (UI)
            layerMask = ~layerMask; //all but UI

            if (Physics.Raycast(mainCam.ScreenPointToRay(pos), out RaycastHit hitInfo, Mathf.Infinity, layerMask)){
                m_worldGameObjectWeHit = hitInfo.transform.gameObject;
                m_sceneObjectWeHit = m_worldGameObjectWeHit.GetComponent<SceneObject>();
                m_worldHitPos = hitInfo.point;
                if (m_sceneObjectWeHit) {
                    m_gameObjectWeHit = m_sceneObjectWeHit.gameObject;
                    return true;
                }
                m_sceneObjectWeHit = m_worldGameObjectWeHit.GetComponentInParent<SceneObject>();
                if (m_sceneObjectWeHit) {
                    m_gameObjectWeHit = m_sceneObjectWeHit.gameObject;
                    return true;
                }
            }
            return false;
        }

        public SceneObject GetSceneObjectViaScreenPosition(int x, int y){
            int scaledX = Mathf.Clamp((int)(x * scaleDivisor), 0, dataWidth - 1);
            int scaledY = Mathf.Clamp((int)(y * scaleDivisor), 0, dataHeight - 1);
            int index = scaledY * dataWidth + scaledX;

            if (!cpuData.IsCreated){
                return null;
            }

            //DECODE Color32 to sceneID and sceneObjectID
            Color32 unityColor = cpuData[index];
            byte sceneID = unityColor.g;
            short soID = (short) (
                (unityColor.b << (8)) | 
                (unityColor.a << (0)) );

            return core.getManager<SceneManager>().getSceneObject(sceneID, soID);
        }

        //!
        //! returns true if pos is over any 3d selectable object 
        //! (uses color array which gets created via rtx)
        //!
        private bool IsSceneObjectAtPixel(Vector2 pos) {
            m_sceneObjectWeHit = GetSceneObjectViaScreenPosition((int)pos.x, (int)pos.y);
            if (m_sceneObjectWeHit) {
                m_worldGameObjectWeHit = m_sceneObjectWeHit.gameObject;
                m_gameObjectWeHit = m_sceneObjectWeHit.gameObject;
                return true;
            }
            return false;
        }

        #endregion

        #region SCENE OBJECT NAVIGATOR

        public enum NavDirection { Left, Right }

        /// <summary>
        /// Finds the next visible object in the specified direction, or the center-closest if no selection exists.
        /// </summary>
        public SceneObject FindNextVisibleObject(List<SceneObject> sceneObjects, SceneObject currentSelection, Camera mainCam, NavDirection direction, UIManager.Roles role){
            if (sceneObjects == null || sceneObjects.Count == 0 || mainCam == null) return null;

            SceneObject bestMatch = null;
            float bestScore = float.MaxValue;

            // NO CURRENT SELECTION (select center-most, nearest to camera)
            if (currentSelection == null){
                Vector2 screenCenter = new Vector2(0.5f, 0.5f);

                foreach (var obj in sceneObjects){
                    if (obj == null || !obj.gameObject.activeInHierarchy || !IsSelectableWithRole(obj, role)) continue;

                    Vector3 vp = mainCam.WorldToViewportPoint(obj.transform.position);

                    if (IsWithinViewport(vp)){

                        float distanceFromCenter = Vector2.Distance(new Vector2(vp.x, vp.y), screenCenter);
                        
                        // multiply screen distance by 100 so the algorithm prioritizes centering over depth,
                        // but uses depth (vp.z) as the tie-breaker for objects stacked perfectly behind each other.
                        float score = (distanceFromCenter * 100f) + vp.z;

                        if (score < bestScore){
                            bestScore = score;
                            bestMatch = obj;
                        }
                    }
                }
                return bestMatch;
            }

            // NAVIGATING LEFT OR RIGHT
            Vector3 currentVp = mainCam.WorldToViewportPoint(currentSelection.transform.position);

            // track the  opposite for our wrap-around fallback
            SceneObject oppositeFallback = null;
            float oppositeVpX = (direction == NavDirection.Right) ? float.MaxValue : float.MinValue;

            foreach (var obj in sceneObjects){
                if (obj == null || obj == currentSelection || !obj.gameObject.activeInHierarchy) continue;

                Vector3 vp = mainCam.WorldToViewportPoint(obj.transform.position);

                if (IsWithinViewport(vp)){
                    // Continuously update the extreme opposite candidate
                    if (direction == NavDirection.Right && vp.x < oppositeVpX) {
                        oppositeVpX = vp.x;
                        oppositeFallback = obj;
                    }else if (direction == NavDirection.Left && vp.x > oppositeVpX) {
                        oppositeVpX = vp.x;
                        oppositeFallback = obj;
                    }

                    bool isRightwards = vp.x > currentVp.x;
                    bool isLeftwards = vp.x < currentVp.x;

                    if ((direction == NavDirection.Right && isRightwards) || 
                        (direction == NavDirection.Left && isLeftwards)){
                        
                        // to catch ALL objects, simply cycle through them by their x screen-pos
                        float score = Mathf.Abs(currentVp.x - vp.x);

                        // float screenDistance = Vector2.Distance(currentVp, vp)
                        // Multiply the Y-difference to penalize objects that are technically closer diagonally, 
                        // but feel unnatural because they are way above or below the current object.
                        // float verticalPenalty = Mathf.Abs(vp.y - currentVp.y) * 2f; 
                        // float score = screenDistance + verticalPenalty;

                        if (score < bestScore){
                            bestScore = score;
                            bestMatch = obj;
                        }
                    }
                }
            }

            return bestMatch != null ? bestMatch : oppositeFallback;
        }

        //!
        //! Checks if a Viewport point is within the camera's viewing frustum
        //! TODO: check if behind walls ("real IsVisible")
        //!
        private bool IsWithinViewport(Vector3 viewportPoint){
            // Z > 0 ensures it is in front of the camera.
            // X and Y between 0 and 1 ensures it is within the screen bounds.
            return viewportPoint.z > 0f && 
                viewportPoint.x >= 0f && viewportPoint.x <= 1f && 
                viewportPoint.y >= 0f && viewportPoint.y <= 1f;
        }

        public bool IsSelectableWithRole(SceneObject obj, UIManager.Roles role){
            switch (obj){
                case SceneObjectCamera:
                    if (role == UIManager.Roles.EXPERT ||
                        role == UIManager.Roles.DOP)
                        return true;
                    return false;
                case SceneObjectLight:
                    if (role == UIManager.Roles.EXPERT ||
                        role == UIManager.Roles.LIGHTING ||
                        role == UIManager.Roles.SET)
                        return true;
                    return false;
                default:
                    if (role == UIManager.Roles.EXPERT ||
                        role == UIManager.Roles.SET)
                        return true;
                    return false;
            }
        }

        #endregion

    }
}
