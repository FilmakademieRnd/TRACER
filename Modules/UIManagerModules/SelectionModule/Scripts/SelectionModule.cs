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

//! @file "PexelSelectorModule.cs"
//! @brief implementation of the TRACER SelectionModule, a pixel precise selection method.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 23.11.2021

using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace tracer
{
    //!
    //! Module to be used per camera that provide selection from main camera.
    //! There can be multiple instances of this class, providing local camera space selection.
    //!
    public class SelectionModule : UIManagerModule
    {
        #if UNITY_EDITOR
        private const bool debugRenderTextureCreation = false;
        private int debugRenderTextureInScene_Counter = 0;
        #endif

        //!
        //! Name of the shader tag for the selection shader.
        //!
        private const string SelectableType = "SelectableType";
        //!
        //! Value od the shader tag for the selection shader.
        //!
        public const string Selectable = "Selectable";
        //!
        //! Name of the shader property holding the selectable _id.
        //!
        private const string m_SelectableIdPropertyName = "_SelectableId";
        //!
        //! The shater to be used for object ID rendering.
        //!
        private Shader objectIdShader;
        //!
        //! The render texture for the selection.
        //!
        private RenderTexture gpuTexture;
        //!
        //! The color/ID data to be stored in the CPU texture.
        //!
        private NativeArray<Color32> cpuData;
        //!
        //! Tracked materials with selectable tag.
        //!
        private readonly Dictionary<Material, Material> m_materials;
        //!
        //! A reference to the TRACER scene manager.
        //!
        private SceneManager m_sceneManager;
        //!
        //! A reference to the TRACER input manager.
        //!
        private InputManager m_inputManager;
        //!
        //! The async redner request.
        //!
        //private AsyncGPUReadbackRequest request;
        //!
        //! Re-used property block used to set selectable _id.
        //!
        private MaterialPropertyBlock m_properties;
        //!
        //! Cached shader property _id of selectable _id.
        //!
        private int m_selectableIdPropertyId;
        //!
        //! Scaled width and height of render texture.
        //!
        private int dataWidth, dataHeight;
        //!
        //! Divides the screen resolution for the selection.
        //!
        private int scaleDivisor = 4;
        //!
        //! Flag to wait for execution to finish
        //!
        private bool m_gpuReadbackRequested = false;
        //!
        //! Flag to determine if touch is active.
        //!
        private bool m_isRenderActive = false;

        public bool isRenderActive
        {
            set => m_isRenderActive = value;
        }

        //!
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public SelectionModule(string name, Manager manager) : base(name, manager)
        {
            objectIdShader = Resources.Load<Shader>("Shader/SelectableId");
            m_materials = new Dictionary<Material, Material>();
            m_selectableIdPropertyId = Shader.PropertyToID(m_SelectableIdPropertyName);
        }

        //! 
        //! Function called when Unity initializes the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Init(object sender, EventArgs e)
        {
            //DEBUGGING
            //Debug.unityLogger.logEnabled = false;

            core.updateEvent += renderUpdate;
           
            m_sceneManager = core.getManager<SceneManager>();
            m_inputManager = core.getManager<InputManager>();
            
            m_sceneManager.sceneReady += modifyMaterials;

            // hookup to input events
            m_inputManager.objectSelectionEvent += SelectFunction;
            
        }

        //!
        //! Callback from TRACER _core when Unity calls OnDestroy.
        //! Used to cleanup resources used by the PixelSelector module.
        //!
        protected override void Cleanup(object sender, EventArgs e)
        {
            base.Cleanup(sender, e);

            core.updateEvent -= renderUpdate;
            m_sceneManager.sceneReady -= modifyMaterials;
            m_inputManager.objectSelectionEvent -= SelectFunction;

            dataWidth = 0;
            dataHeight = 0;

            if (gpuTexture != null)
                gpuTexture.Release();

            if (cpuData.IsCreated)
                cpuData.Dispose();

            //m_hasAsyncRequest = false;
            //request = default(AsyncGPUReadbackRequest);
        }

        //!
        //! Function to connect input managers input event for estimating a scene object at
        //! a certain screen coortinate with UI managers scene object selection mechanism.
        //!
        //! @param sender The input manager.
        //! @param e The screen coorinates from the input event.
        //!
        private async void SelectFunction(object sender, Vector2 point){
            //Debug.Log("<color=orange>SelectFunction called</color>");

            //if already waiting for a previous select function to be resolved, abort
            //very unlikely, but if the system has low fps due something and we'Re spamming clicks, it may be possible
            if(m_isRenderActive || m_gpuReadbackRequested)
                return;

            SceneObject obj = GetSelectableAtCollider(point);
            if (!obj){
                //Debug.Log("<color=yellow>GetSelectableAtCollider did not find an object. Testing with Pixel</color>");
                m_isRenderActive = true;
                // give the system some time to render the object _id's
                while(m_isRenderActive || m_gpuReadbackRequested)
                    await System.Threading.Tasks.Task.Delay(50);

                obj = GetSelectableAtPixel(point);
            }
            //else{
            //    Debug.Log("<color=green>Found clicked object via GetSelectableAtCollider</color>");
            //}

            if (obj){                
                CheckDoubleClick(obj);

                if(manager.isThisOurSelectedObject(obj)){
                    //Debug.Log("<color=green>already selected</color>");
                    m_isRenderActive = false;
                    return;
                }else{
                    manager.clearSelectedObject();
                    // if(obj._lock)
                    //     Debug.Log("<color=red>clicked object is locked</color>");
                    // else
                    //     Debug.Log("<color=blue>new selected object</color>");
                }

                AddSelectionByRole(obj);
            }else{
                manager.clearSelectedObject();
                //Debug.Log("<color=red>clicked nothing. selected objects cleared</color>");
            }
        }
        //!
        //! Function to add the found selected object to the manager, depending by our Role
        //!
        private void AddSelectionByRole(SceneObject clickedSceneObject){
            switch (clickedSceneObject){
                case SceneObjectCamera:
                    if (manager.activeRole == UIManager.Roles.EXPERT ||
                        manager.activeRole == UIManager.Roles.DOP)
                        manager.addSelectedObject(clickedSceneObject);
                    break;
                case SceneObjectLight:
                    if (manager.activeRole == UIManager.Roles.EXPERT ||
                        manager.activeRole == UIManager.Roles.LIGHTING ||
                        manager.activeRole == UIManager.Roles.SET)
                        manager.addSelectedObject(clickedSceneObject);
                    break;
                default:
                    if (manager.activeRole == UIManager.Roles.EXPERT ||
                        manager.activeRole == UIManager.Roles.SET)
                        manager.addSelectedObject(clickedSceneObject);
                    break;
            }
        }
        //!
        //! Function to check for a double-click/tap to focus on an object
        //!
        private void CheckDoubleClick(SceneObject obj){
            if(!obj){
                manager.setLastClickedObject(null);
                return;
            }

            //Double-Click on the same obj -> focus on it
            if(m_inputManager.WasDoubleClick()){
                if(manager.LastClickedObject == obj){  //works with locked objects as well!
                    manager.focusOnLastClickedObject();
                }
            }
            manager.setLastClickedObject(obj);
        }
        //!
        //! Function to simulate Select
        //!
        public void SetSelectedObjectViaScript(SceneObject obj){
            if(!obj || manager.isThisOurSelectedObject(obj))
                return;
            manager.clearSelectedObject();
            switch (obj){
                case SceneObjectCamera:
                    if (manager.activeRole == UIManager.Roles.EXPERT ||
                        manager.activeRole == UIManager.Roles.DOP)
                        manager.addSelectedObject(obj);
                    break;
                case SceneObjectLight:
                    if (manager.activeRole == UIManager.Roles.EXPERT ||
                        manager.activeRole == UIManager.Roles.LIGHTING ||
                        manager.activeRole == UIManager.Roles.SET)
                        manager.addSelectedObject(obj);
                    break;
                default:
                    if (manager.activeRole == UIManager.Roles.EXPERT ||
                        manager.activeRole == UIManager.Roles.SET)
                        manager.addSelectedObject(obj);
                    break;
            }
        }
        //!
        //! Retrieve the selectable present at the current location in camera screenspace, if any.
        //! 
        //! @param screenPosition The position to get the selectable at.
        //! @return The selectable at the specified screen position or null if there is none.
        //!
        public SceneObject GetSelectableAtPixel(Vector2 screenPosition)
        {
            if (!cpuData.IsCreated){
                //Debug.Log("<color=red>GetSelectableAtPixel cpuData.IsCreated = false!</color>");
                return null;
            }

            int scaledX = (int)screenPosition.x / scaleDivisor;
            int scaledY = (int)screenPosition.y / scaleDivisor;
            int pos = scaledX + dataWidth * scaledY;
            
            if (cpuData.Length < pos || pos < 0){
                //Debug.Log("<color=red>GetSelectableAtPixel pos wrong ("+cpuData.Length+" /"+pos+") </color>");
                return null;
            }

            //Save Texture for debugging
            #if UNITY_EDITOR
            if(debugRenderTextureCreation){
                debugRenderTextureInScene_Counter++;

                Texture2D tex = new Texture2D(gpuTexture.width, gpuTexture.height, TextureFormat.ARGB32, false);
                tex.LoadRawTextureData(cpuData);
                tex.Apply();
                GameObject debugGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                debugGo.transform.position = new Vector3(10,10,0) + Vector3.right*1.1f*debugRenderTextureInScene_Counter;
                debugGo.name = "RenderTextureDebugGO_"+Time.time;
                GameObject.Destroy(debugGo.GetComponent<MeshCollider>());
                debugGo.GetComponent<MeshRenderer>().material.mainTexture = tex;
                debugGo.GetComponent<MeshRenderer>().material.shader = Shader.Find("Unlit/Texture");
            }
            #endif
            
            byte sceneID = 0;
            short soID = 0;
            DecodeId(cpuData[pos], ref sceneID, ref soID);
            //Debug.Log("<color=yellow>DecodeId sceneID = "+sceneID+", soID = "+soID+"</color>");

            return m_sceneManager.getSceneObject(sceneID, soID);
        }

        //!
        //! Retrieve the selectable present at the current location by tracing a ray into the scene and looking for colliders.
        //! 
        //! @param screenPosition The position to get the selectable at.
        //! @param layerMask The object layers to be considered for the ray intersection.
        //! @return The selectable at the traced collider or null if there is none.
        //!
        public SceneObject GetSelectableAtCollider(Vector2 screenPosition)
        {
            RaycastHit hit;
            SceneObject sceneObject = null;

            if (Physics.Raycast(Camera.main.ScreenPointToRay(screenPosition), out hit, Mathf.Infinity))
            {
                GameObject gameObject = hit.collider.gameObject;
                IconUpdate iconUpdate = gameObject.GetComponent<IconUpdate>();
                // check if an icon has been hit
                if (iconUpdate)
                    sceneObject = iconUpdate.m_parentObject;

                if (!sceneObject)
                    sceneObject = gameObject.GetComponent<SceneObject>();

                //changed by Thomas: Try to get SceneObject in any parent
                if(!sceneObject){
                    sceneObject = gameObject.GetComponentInParent<SceneObject>();
                    //Debug.Log("<color=orange>Found GetComponentInParent ? "+(sceneObject != null)+"</color>");
                }
                //if (!sceneObject && gameObject.transform.parent)
                    //sceneObject = gameObject.transform.parent.GetComponent<SceneObject>();

            }

            return sceneObject;
        }

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
        private Material GetSelectableMaterial(Material material)
        {
            Material selectableMaterial;
            if (!m_materials.TryGetValue(material, out selectableMaterial))
            {
                selectableMaterial = UnityEngine.Object.Instantiate(material);
                selectableMaterial.SetOverrideTag(SelectableType, Selectable);
                m_materials.Add(material, selectableMaterial);
            }

            return selectableMaterial;
        }

        //!
        //! Callback from TRACER _core when Unity calls it's render update.
        //! Used setup render texture, render the object ID pass and copy
        //! it asyncron into a Color32 array. 
        //!
        private void renderUpdate(object sender, EventArgs e)
        {
            if (m_isRenderActive){
                //Debug.Log("renderUpdate called "+Time.time);
                //Debug.Log("<color=green>ACTIVE renderUpdate called</color>");
                Camera camera = Camera.main;

                // If camera size changed (e.g. window resize) adjust sizes of selection textures.
                int currentWidth = camera.pixelWidth / scaleDivisor;
                int currentHeight = camera.pixelHeight / scaleDivisor;
                if (gpuTexture == null || dataWidth != currentWidth || dataHeight != currentHeight)
                {
                    if (gpuTexture != null) gpuTexture.Release();
                    if (cpuData.IsCreated) cpuData.Dispose();

                    dataWidth = currentWidth;
                    dataHeight = currentHeight;

                    int depthBits = camera.depthTextureMode == DepthTextureMode.None ? 16 : 0;
                    gpuTexture = new RenderTexture(dataWidth, dataHeight, depthBits, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    gpuTexture.filterMode = FilterMode.Point;
                    cpuData = new NativeArray<Color32>(dataWidth * dataHeight, Allocator.Persistent);
                }

                RenderTexture oldRenderTexture = camera.targetTexture;
                CameraClearFlags oldClearFlags = camera.clearFlags;
                Color oldBackgroundColor = camera.backgroundColor;
                RenderingPath oldRenderingPath = camera.renderingPath;
                bool oldAllowMsaa = camera.allowMSAA;

                camera.targetTexture = gpuTexture; // Render into render texture.
                camera.clearFlags = CameraClearFlags.SolidColor; // Make sure non-rendered pixels have _id zero.
                camera.backgroundColor = Color.clear;
                camera.renderingPath = RenderingPath.Forward; // No gbuffer required.
                camera.allowMSAA = false; // Avoid interpolated colors.

                camera.RenderWithShader(objectIdShader, SelectableType);

                camera.targetTexture = oldRenderTexture;
                camera.clearFlags = oldClearFlags;
                camera.backgroundColor = oldBackgroundColor;
                camera.renderingPath = oldRenderingPath;
                camera.allowMSAA = oldAllowMsaa;

                m_isRenderActive = false;
                m_gpuReadbackRequested = true;
                
                //see https://dev.to/alpenglow/unity-fast-pixel-reading-part-2-asyncgpureadback-4kgn for example implementation
                AsyncGPUReadback.Request(gpuTexture, 0, OnCompleteAsyncGPUReadback);
            }
        }

        private void OnCompleteAsyncGPUReadback(AsyncGPUReadbackRequest request){
            m_gpuReadbackRequested = false;
            if (!request.done){
                Debug.Log("<color=yellow>request not done</color>");
                return;
            }

            if (request.hasError){
                Debug.Log("<color=red>request.hasError</color>");
                return;
            }
                
            //Debug.Log("<color=green>request.done</color>");
            request.GetData<Color32>().CopyTo(cpuData);
        }

        //!
        //! Function that creates a new property block for all renderable
        //! objects in the scene to set the object ID as a shader parameter.
        //! This function is called after the scene has been loaded.
        //!
        private void modifyMaterials(object sender, EventArgs e)
        {
            foreach (Renderer renderer in m_sceneManager.scnRoot.GetComponentsInChildren<Renderer>())
            {
                if (m_properties == null)
                    m_properties = new MaterialPropertyBlock();

                SceneObject sceneObject = renderer.gameObject.GetComponent<SceneObject>();

                if ((sceneObject is SceneObjectCamera) || (sceneObject is SceneObjectLight))
                    continue;

                int soID = 0;
                int sceneID = 0;
                if (sceneObject)
                {
                    soID = sceneObject._id;  
                    sceneID = sceneObject._sceneID;
                }
                else
                {
                    Transform t = renderer.transform;
                    Transform root = core.getManager<SceneManager>().scnRoot.transform;

                    while (t.parent != root)
                    {
                        if (t.parent.tag == "editable")
                        {
                            SceneObject so = t.parent.GetComponent<SceneObject>();
                            if (so)
                            {
                                soID = so._id;  
                                sceneID = so._sceneID;
                            }
                            break;
                        }
                        else
                        {
                            t = t.parent;
                        }
                    }
                }

                Color32 packedId;
                EncodeId(out packedId, (byte) sceneID, (short) soID);

                m_properties.Clear();

                // Keep existing changed properties.
                if (renderer.HasPropertyBlock())
                    renderer.GetPropertyBlock(m_properties);

                m_properties.SetColor(m_selectableIdPropertyId, packedId);

                renderer.SetPropertyBlock(m_properties);

                renderer.sharedMaterial = GetSelectableMaterial(renderer.sharedMaterial);
            }
        }

        //! 
        //! Encodes a selectable _id into a color.
        //! 
        //! @param _id The selectable _id to be encoded.
        //! @return The color representing the encoded _id.
        //!
        private void EncodeId(out Color32 color, byte sceneID, short soID)
        {
            color = new Color32(
                0,
                sceneID,
                (byte)(soID >> (8)),
                (byte)(soID >> (0)) );
        }

        //! 
        //! Decodes a color into a selectable _id.
        //! 
        //! @param color The color to decode into a selectable _id.
        //! @return The decoded selectable _id.
        //!
        private void DecodeId(Color32 color, ref byte sceneID, ref short soID)
        {
            sceneID = color.g;
            soID = (short) (
                (color.b << (8)) | 
                (color.a << (0)) );
        }
    }

}
