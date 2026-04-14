using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace tracer{

    public class IDExtractorModule : UIManagerModule, UIManager.ISelectableSceneObjectProvider {

        #region Interface Implementation
        //if we every want to utilize the "get selectable via pixel id" we have come with this solution
        //to implement a function in the UIManager without ANY unity dependency and
        //without to do the RenderUpdate step in different functions!
        public SceneObject GetSelectableViaScreenPosition(int x, int y){
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

            return m_sceneManager.getSceneObject(sceneID, soID);
        }
        #endregion

        #region Settings
        private enum CalculationBehaviourEnum {
            ongoing = 0,                //calculate ongoing once a previous calc finishes
            onInput = 10,               //start calc on any input down - should be ready until the up event gets triggered
            onInputAndCamChange = 20    //does not take into account any object modification
        }

        private readonly CalculationBehaviourEnum calcBehaviour = CalculationBehaviourEnum.onInput;
        #endregion

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
        //! A reference to the TRACER scene manager.
        //!
        private SceneManager m_sceneManager;
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
        private readonly Dictionary<Material, Material> m_materials;
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
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public IDExtractorModule(string name, Manager manager) : base(name, manager){
            objectIdShader = Resources.Load<Shader>("Shader/SelectableId");
            m_materials = new Dictionary<Material, Material>();
            m_selectableIdPropertyId = Shader.PropertyToID(SelectableIdPropertyName);
        }

        //! 
        //! Function called when Unity initializes the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Init(object sender, EventArgs e){
            mainCam = Camera.main;
            selectableLayer = LayerMask.GetMask(CullingLayerName);

            switch (calcBehaviour) {
                case CalculationBehaviourEnum.ongoing:
                    core.updateEvent += RenderUpdate;
                    break;
                case CalculationBehaviourEnum.onInput:
                case CalculationBehaviourEnum.onInputAndCamChange:
                    core.getManager<InputManager>().onAnyInputDetection += RenderUpdate;
                    break;
            }

            Debug.Log("<color=green>IDExtractorModule initialized with settings <b>"+calcBehaviour.ToString()+"</b></color>");

            manager.RegisterProvider(this);
            m_sceneManager = core.getManager<SceneManager>();
            m_sceneManager.sceneReady += ModifyMaterialsForIDs;
        }

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
            Material selectableMaterial;
            if (!m_materials.TryGetValue(material, out selectableMaterial)){
                selectableMaterial = UnityEngine.Object.Instantiate(material);
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
        private void ModifyMaterialsForIDs(object sender, EventArgs e){  
            MaterialPropertyBlock m_properties = new MaterialPropertyBlock(); //Re-used property block used to set selectable _id.
            Transform root = m_sceneManager.scnRoot.transform;

            foreach (Renderer renderer in m_sceneManager.scnRoot.GetComponentsInChildren<Renderer>()){
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
                            SceneObject so = t.parent.GetComponent<SceneObject>();
                            if (so){
                                soID = so._id;  
                                sceneID = so._sceneID;
                            }
                            break;  //shouldn't this be within the 'if' above?
                        }else{
                            t = t.parent;
                        }
                    }
                }

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
            }
        }
        #endregion


        #region ID Array (cpuData) Creation
        //!
        //! Callback from TRACER _core when Unity calls it's render update.
        //! Used setup render texture, render the object ID pass and copy
        //! it asyncron into a Color32 array. 
        //!
        private void RenderUpdate(object sender, EventArgs e){
            // ONLY trigger a new render if we aren't currently waiting for a readback to finish.
            if (!m_gpuReadbackRequested){
                UpdateIDTexture();
            }
        }

        private void UpdateIDTexture(){

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
                request.GetData<Color32>().CopyTo(cpuData);
            }

            // Now that we have the data, we allow the Update loop to trigger the next render
            m_gpuReadbackRequested = false; 
        }
        #endregion


        //!
        //! Callback from TRACER _core when Unity calls OnDestroy.
        //!
        public override void Dispose(){
            base.Dispose();

            switch (calcBehaviour) {
                case CalculationBehaviourEnum.ongoing:
                    core.updateEvent -= RenderUpdate;
                    break;
                case CalculationBehaviourEnum.onInput:
                case CalculationBehaviourEnum.onInputAndCamChange:
                    core.getManager<InputManager>().onAnyInputDetection -= RenderUpdate;
                    break;
            }
            m_sceneManager.sceneReady -= ModifyMaterialsForIDs;
            manager.UnregisterProvider();

            if (gpuTexture != null) gpuTexture.Release();
            if (cpuData.IsCreated) cpuData.Dispose();
        }
    }
}
