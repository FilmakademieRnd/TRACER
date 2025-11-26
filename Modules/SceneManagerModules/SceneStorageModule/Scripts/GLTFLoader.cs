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

//! @file "GLTFLoader.cs"
//! @brief glTF file loader with iOS texture support and TRACER integration
//! @author Jonas Trottnow
//! @version 1.0
//! @date 05.11.2025

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using GLTFast;
using GLTFast.Loading;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace tracer
{
    //!
    //! Handles loading glTF/GLB files and converting them for TRACER pipeline integration.
    //! Includes iOS-specific fixes for material and texture handling.
    //!
    public class GLTFLoader
    {
        private Core m_core;
        private SceneManager m_sceneManager;
        private GltfImport m_gltfImport;

        //!
        //! Constructor
        //!
        //! @param core The TRACER core instance
        //! @param sceneManager The scene manager instance
        //!
        public GLTFLoader(Core core, SceneManager sceneManager)
        {
            m_core = core;
            m_sceneManager = sceneManager;
        }

        //!
        //! Show platform-specific file picker for glTF/GLB files
        //!
        //! @param onFileSelected Callback invoked with selected file path (empty if cancelled)
        //!
        public static void BrowseForFile(Action<string> onFileSelected)
        {
#if UNITY_EDITOR
            // In Editor, use native file browser
            string selectedPath = EditorUtility.OpenFilePanelWithFilters("Select glTF/GLB File",
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                new string[] { "glTF Files", "gltf,glb", "All Files", "*" });

            if (!string.IsNullOrEmpty(selectedPath))
            {
                onFileSelected?.Invoke(selectedPath);
            }
#else
            // Use native file picker on iOS
            if (NativeFilePicker.IsAvailable())
            {
                NativeFilePicker.PickFile("gltf,glb", (string selectedPath) =>
                {
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        onFileSelected?.Invoke(selectedPath);
                    }
                });
            }
            else
            {
                Helpers.Log("Native file picker not available on this platform", Helpers.logMsgType.WARNING);
            }
#endif
        }

        //!
        //! Load a glTF/GLB file from disk
        //!
        //! @param filePath Absolute path to the glTF/GLB file
        //! @return Root GameObject containing the loaded scene, or null if failed
        //!
        public async Task<GameObject> LoadFile(string filePath)
        {
            try
            {
                Helpers.Log($"glTF Loader: Loading file from path: {filePath}");

                // Verify file exists
                if (!File.Exists(filePath))
                {
                    Helpers.Log($"glTF Loader: File does not exist at path: {filePath}", Helpers.logMsgType.ERROR);
                    return null;
                }

                // Log file size
                FileInfo fileInfo = new FileInfo(filePath);
                Helpers.Log($"glTF Loader: File size: {fileInfo.Length} bytes");

                // Clean up previous import
                if (m_gltfImport != null)
                {
                    m_gltfImport.Dispose();
                }

                // Create new glTFast importer with custom import settings
                m_gltfImport = new GltfImport();

                // Configure import settings for better iOS compatibility
                var importSettings = new GLTFast.ImportSettings
                {
                    // Generate mipmaps for better quality
                    GenerateMipMaps = true,
                    // Anisotropic filtering
                    AnisotropicFilterLevel = 3,
                };

                // Load the file with settings using file:// URI scheme
                string uri = "file://" + filePath;
                Helpers.Log($"glTF Loader: Loading from URI: {uri}");
                bool success = await m_gltfImport.Load(uri, importSettings);

                if (!success)
                {
                    Helpers.Log("glTF Loader: Failed to load glTF file", Helpers.logMsgType.ERROR);
                    return null;
                }

                Helpers.Log("glTF Loader: File loaded successfully");

                // Create a temporary parent object for the glTF content
                GameObject loadedObject = new GameObject(Path.GetFileNameWithoutExtension(filePath));

                var instantiator = new GameObjectInstantiator(m_gltfImport, loadedObject.transform);
                success = await m_gltfImport.InstantiateMainSceneAsync(instantiator);

                if (!success)
                {
                    Helpers.Log("glTF Loader: Failed to instantiate scene", Helpers.logMsgType.ERROR);
                    if (loadedObject != null)
                        UnityEngine.Object.Destroy(loadedObject);
                    return null;
                }

                // Wait for materials to be fully instantiated (iOS timing issue)
                // On iOS, glTFast creates materials asynchronously and needs real time to complete
                Helpers.Log("glTF Loader: Waiting for materials to be ready on iOS...");
                await WaitForMaterials(loadedObject, 2000);

                // Access scene instance to check for cameras and lights
                var sceneInstance = instantiator.SceneInstance;

                // Enable imported cameras (disabled by default in glTFast)
                int cameraCount = 0;
                if (sceneInstance.Cameras != null && sceneInstance.Cameras.Count > 0)
                {
                    foreach (var camera in sceneInstance.Cameras)
                    {
                        if (camera != null)
                        {
                            camera.enabled = true;
                            cameraCount++;
                        }
                    }
                    Helpers.Log($"glTF Loader: Enabled {cameraCount} camera(s)");
                }

                // Log light count
                int lightCount = 0;
                if (sceneInstance.Lights != null && sceneInstance.Lights.Count > 0)
                {
                    lightCount = sceneInstance.Lights.Count;
                    Helpers.Log($"glTF Loader: Imported {lightCount} light(s)");
                }

                Helpers.Log($"glTF Loader: Successfully loaded {filePath} ({cameraCount} cameras, {lightCount} lights)");
                return loadedObject;
            }
            catch (Exception ex)
            {
                Helpers.Log($"glTF Loader: Exception loading glTF: {ex.Message}", Helpers.logMsgType.ERROR);
                return null;
            }
        }

        //!
        //! Prepare loaded glTF object for TRACER pipeline integration
        //!
        //! @param rootObject The root GameObject of the loaded glTF scene
        //! @param scale Scale factor to apply to the loaded object
        //!
        public async Task PrepareForTRACER(GameObject rootObject, float scale)
        {
            // Parent under Scene GameObject
            rootObject.transform.SetParent(m_sceneManager.scnRoot.transform);

            // Apply scale
            rootObject.transform.localScale = new Vector3(scale, scale, scale);

            // Set layer to LodMixed (required for scene parser)
            int lodMixedLayer = LayerMask.NameToLayer("LodMixed");
            SetLayerRecursively(rootObject, lodMixedLayer);

            // Tag all mesh objects as editable (so they get SceneObject components)
            TagMeshObjectsAsEditable(rootObject);

            // Add default directional light if no lights exist
            Light[] existingLights = rootObject.GetComponentsInChildren<Light>();
            if (existingLights == null || existingLights.Length == 0)
            {
                AddDefaultDirectionalLight(rootObject);
            }

            // iOS FIX: Wait for materials to finalize after parenting
            // On iOS, glTFast doesn't assign materials to renderers until after the object is parented
            await WaitForMaterials(rootObject, 3000);

            // Convert glTFast materials to Unity Standard shader
            ConvertMaterialsToStandardShader(rootObject);

            // Enable double-sided rendering (will be captured in TRACER binary)
            EnableDoubleSidedRendering(rootObject);
        }

        //!
        //! Extract and assign textures from glTFast to materials
        //! iOS FIX: On iOS, glTFast creates materials but doesn't populate them with textures
        //!
        //! @param sceneRoot The root GameObject of the scene to process
        //!
        public async Task ExtractAndAssignTextures(GameObject sceneRoot)
        {
            // iOS FIX: Final material check before parsing
            await WaitForMaterials(sceneRoot, 500);

            // Get all renderers for texture assignment
            MeshRenderer[] allRenderers = sceneRoot.GetComponentsInChildren<MeshRenderer>();

            // Extract textures from glTFast and assign to materials
            if (m_gltfImport != null && m_gltfImport.TextureCount > 0)
            {
                // Create a dictionary of textures by type based on their names
                Dictionary<string, Texture2D> texturesByType = new Dictionary<string, Texture2D>();

                for (int i = 0; i < m_gltfImport.TextureCount; i++)
                {
                    var texture = m_gltfImport.GetTexture(i);
                    if (texture != null)
                    {
                        string texName = texture.name.ToLower();

                        // Make texture readable for TRACER serialization
                        Texture2D readableTexture = MakeTextureReadable(texture);

                        // Identify texture type by name
                        if (texName.Contains("color") || texName.Contains("basecolor") || texName.Contains("albedo") || texName.Contains("diffuse"))
                        {
                            texturesByType["baseColor"] = readableTexture;
                        }
                        else if (texName.Contains("normal") || texName.Contains("normalobject"))
                        {
                            texturesByType["normal"] = readableTexture;
                        }
                        else if (texName.Contains("metallic") || texName.Contains("roughness") || texName.Contains("metallicroughness"))
                        {
                            texturesByType["metallic"] = readableTexture;
                        }
                        else if (texName.Contains("occlusion") || texName.Contains("ao"))
                        {
                            texturesByType["occlusion"] = readableTexture;
                        }
                        else if (texName.Contains("emissive") || texName.Contains("emission"))
                        {
                            texturesByType["emissive"] = readableTexture;
                        }
                        else
                        {
                            // Default to base color if unknown
                            if (!texturesByType.ContainsKey("baseColor"))
                            {
                                texturesByType["baseColor"] = readableTexture;
                            }
                        }
                    }
                }

                // Assign textures to materials
                int texturesAssigned = 0;
                foreach (var renderer in allRenderers)
                {
                    if (renderer != null && renderer.sharedMaterials != null)
                    {
                        Material[] materials = renderer.sharedMaterials;
                        bool modified = false;

                        for (int i = 0; i < materials.Length; i++)
                        {
                            Material mat = materials[i];
                            if (mat != null && mat.shader.name == "Standard")
                            {
                                // Assign base color texture
                                if (texturesByType.ContainsKey("baseColor"))
                                {
                                    mat.SetTexture("_MainTex", texturesByType["baseColor"]);
                                    texturesAssigned++;
                                    modified = true;
                                }

                                // Assign normal map
                                if (texturesByType.ContainsKey("normal"))
                                {
                                    mat.SetTexture("_BumpMap", texturesByType["normal"]);
                                    mat.EnableKeyword("_NORMALMAP");
                                    texturesAssigned++;
                                    modified = true;
                                }

                                // Assign metallic/roughness
                                if (texturesByType.ContainsKey("metallic"))
                                {
                                    mat.SetTexture("_MetallicGlossMap", texturesByType["metallic"]);
                                    mat.EnableKeyword("_METALLICGLOSSMAP");
                                    texturesAssigned++;
                                    modified = true;
                                }

                                // Assign occlusion
                                if (texturesByType.ContainsKey("occlusion"))
                                {
                                    mat.SetTexture("_OcclusionMap", texturesByType["occlusion"]);
                                    texturesAssigned++;
                                    modified = true;
                                }

                                // Assign emissive
                                if (texturesByType.ContainsKey("emissive"))
                                {
                                    mat.SetTexture("_EmissionMap", texturesByType["emissive"]);
                                    mat.EnableKeyword("_EMISSION");
                                    texturesAssigned++;
                                    modified = true;
                                }
                            }
                        }

                        if (modified)
                        {
                            renderer.sharedMaterials = materials;
                        }
                    }
                }

                if (texturesAssigned > 0)
                {
                    Helpers.Log($"glTF Loader: Assigned {texturesAssigned} texture(s) to materials");
                }
            }
            else
            {
                Helpers.Log("glTF Loader: No textures found in glTFast importer", Helpers.logMsgType.WARNING);
            }

            // Enable double-sided rendering AFTER texture assignment
            EnableDoubleSidedRendering(sceneRoot);
        }

        //!
        //! Apply double-sided rendering to the rebuilt scene after TRACER binary deserialization
        //!
        //! @param sceneRoot The root GameObject of the rebuilt scene
        //!
        public void ApplyDoubleSidedRenderingToRebuiltScene(GameObject sceneRoot)
        {
            Helpers.Log("glTF Loader: Applying double-sided rendering to rebuilt scene");

            // Get all SceneObjects in the rebuilt scene
            SceneObject[] sceneObjects = sceneRoot.GetComponentsInChildren<SceneObject>();
            int materialsProcessed = 0;

            foreach (SceneObject sceneObj in sceneObjects)
            {
                if (sceneObj != null && sceneObj.gameObject != null)
                {
                    MeshRenderer renderer = sceneObj.GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.sharedMaterials != null)
                    {
                        Material[] materials = renderer.sharedMaterials;

                        foreach (Material mat in materials)
                        {
                            if (mat != null && mat.shader.name == "Standard")
                            {
                                mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                                materialsProcessed++;
                            }
                        }
                    }
                }
            }

            Helpers.Log($"glTF Loader: Applied double-sided rendering to {materialsProcessed} materials in rebuilt scene");
        }

        //!
        //! Dispose of glTFast resources
        //!
        public void Dispose()
        {
            if (m_gltfImport != null)
            {
                m_gltfImport.Dispose();
                m_gltfImport = null;
            }
        }

        #region Helper Methods

        //!
        //! Wait for materials to be fully instantiated (iOS timing issue)
        //!
        //! @param rootObject The root GameObject to check for materials
        //! @param maxWaitMs Maximum time to wait in milliseconds
        //!
        private async Task WaitForMaterials(GameObject rootObject, int maxWaitMs)
        {
            int waitIntervalMs = 50;
            int elapsed = 0;
            bool materialsReady = false;

            while (elapsed < maxWaitMs && !materialsReady)
            {
                await Task.Delay(waitIntervalMs);
                elapsed += waitIntervalMs;

                MeshRenderer[] renderers = rootObject.GetComponentsInChildren<MeshRenderer>();
                materialsReady = true;

                foreach (var renderer in renderers)
                {
                    if (renderer != null && renderer.sharedMaterials != null)
                    {
                        foreach (var mat in renderer.sharedMaterials)
                        {
                            if (mat == null)
                            {
                                materialsReady = false;
                                break;
                            }
                        }
                    }
                    if (!materialsReady) break;
                }
            }

            if (!materialsReady)
            {
                Helpers.Log($"glTF Loader: Warning - Materials not ready after {maxWaitMs}ms wait", Helpers.logMsgType.WARNING);
            }
        }

        //!
        //! Create a readable copy of a texture for TRACER serialization
        //! glTFast textures on iOS are not marked as readable by default
        //!
        //! @param texture The texture to make readable
        //! @return A readable copy of the texture
        //!
        private Texture2D MakeTextureReadable(Texture2D texture)
        {
            // Create a readable copy of the texture for TRACER serialization
            // glTFast textures on iOS are not marked as readable by default
            try
            {
                // Create a temporary RenderTexture
                RenderTexture tmp = RenderTexture.GetTemporary(
                    texture.width,
                    texture.height,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);

                // Blit the texture to the RenderTexture
                Graphics.Blit(texture, tmp);

                // Save the active RenderTexture
                RenderTexture previous = RenderTexture.active;

                // Set the RenderTexture as active
                RenderTexture.active = tmp;

                // Create a new readable Texture2D and read pixels from RenderTexture
                Texture2D readableTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, true);
                readableTexture.name = texture.name;
                readableTexture.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
                readableTexture.Apply();

                // Restore the active RenderTexture
                RenderTexture.active = previous;

                // Release the temporary RenderTexture
                RenderTexture.ReleaseTemporary(tmp);

                return readableTexture;
            }
            catch (Exception ex)
            {
                Helpers.Log($"glTF Loader: Failed to make texture readable: {ex.Message}", Helpers.logMsgType.ERROR);
                return texture; // Return original as fallback
            }
        }

        //!
        //! Convert glTFast materials to Unity Standard shader
        //!
        //! @param rootObject The root GameObject containing renderers to process
        //!
        private void ConvertMaterialsToStandardShader(GameObject rootObject)
        {
            MeshRenderer[] renderers = rootObject.GetComponentsInChildren<MeshRenderer>();
            int convertedCount = 0;

            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer != null && renderer.sharedMaterials != null)
                {
                    Material[] materials = renderer.sharedMaterials;
                    bool modified = false;

                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material mat = materials[i];

                        // Only convert glTFast materials to Standard shader
                        if (mat != null && (mat.shader.name.Contains("glTF") || mat.shader.name.Contains("PbrMetallicRoughness")))
                        {
                            Shader standardShader = Shader.Find("Standard");
                            if (standardShader == null)
                            {
                                Helpers.Log("glTF Loader: Could not find Standard shader!", Helpers.logMsgType.ERROR);
                                continue;
                            }

                            // Extract textures and properties from glTF material
                            Texture mainTex = mat.mainTexture;
                            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

                            // Create new Standard material
                            Material newMat = new Material(standardShader);
                            newMat.name = mat.name;

                            // Copy base properties
                            if (mainTex != null)
                            {
                                newMat.SetTexture("_MainTex", mainTex);
                            }
                            newMat.SetColor("_Color", color);

                            // Set default PBR values
                            newMat.SetFloat("_Metallic", 0.0f);
                            newMat.SetFloat("_Glossiness", 0.5f);

                            // Enable double-sided rendering
                            newMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

                            // Replace the material
                            materials[i] = newMat;
                            modified = true;
                            convertedCount++;

                            Helpers.Log($"glTF Loader: Converted material '{mat.name}' from {mat.shader.name} to Standard shader");
                        }
                    }

                    if (modified)
                    {
                        renderer.sharedMaterials = materials;
                    }
                }
            }

            Helpers.Log($"glTF Loader: Converted {convertedCount} materials to Standard shader");
        }

        //!
        //! Enable double-sided rendering on all Standard shader materials
        //!
        //! @param rootObject The root GameObject containing renderers to process
        //!
        private void EnableDoubleSidedRendering(GameObject rootObject)
        {
            // Get all renderers in the hierarchy
            MeshRenderer[] renderers = rootObject.GetComponentsInChildren<MeshRenderer>();
            int materialsProcessed = 0;

            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer != null && renderer.sharedMaterials != null)
                {
                    // Process each material on the renderer
                    Material[] materials = renderer.sharedMaterials;

                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material mat = materials[i];

                        if (mat != null && mat.shader.name == "Standard")
                        {
                            // Enable double-sided rendering by disabling back-face culling
                            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                            materialsProcessed++;
                        }
                    }
                }
            }

            if (materialsProcessed > 0)
            {
                Helpers.Log($"glTF Loader: Applied double-sided rendering to {materialsProcessed} materials");
            }
        }

        //!
        //! Add a default directional light to the scene if none exist
        //!
        //! @param rootObject The root GameObject to add the light to
        //!
        private void AddDefaultDirectionalLight(GameObject rootObject)
        {
            // Create directional light GameObject
            GameObject lightObj = new GameObject("Default Directional Light");

            // Parent under the root object
            lightObj.transform.SetParent(rootObject.transform);

            // Position and rotate (standard 3/4 view lighting angle)
            lightObj.transform.localPosition = Vector3.zero;
            lightObj.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
            lightObj.transform.localScale = Vector3.one;

            // Add Light component
            Light lightComponent = lightObj.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.color = Color.white;
            lightComponent.intensity = 1.0f;
            lightComponent.shadows = LightShadows.Soft;
            lightComponent.shadowStrength = 0.8f;

            // Set layer to LodMixed
            int lodMixedLayer = LayerMask.NameToLayer("LodMixed");
            lightObj.layer = lodMixedLayer;

            // Tag as editable so TRACER parser picks it up
            lightObj.tag = "editable";

            Helpers.Log("glTF Loader: Added default directional light (no lights found in glTF)");
        }

        //!
        //! Tag all mesh objects as "editable" so TRACER parser picks them up
        //!
        //! @param rootObject The root GameObject to process
        //!
        private void TagMeshObjectsAsEditable(GameObject rootObject)
        {
            // Tag the root
            rootObject.tag = "editable";

            // Tag all children with MeshFilter or MeshRenderer
            MeshFilter[] meshFilters = rootObject.GetComponentsInChildren<MeshFilter>();
            foreach (MeshFilter meshFilter in meshFilters)
            {
                meshFilter.gameObject.tag = "editable";
            }

            MeshRenderer[] meshRenderers = rootObject.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in meshRenderers)
            {
                renderer.gameObject.tag = "editable";
            }

            // Tag all cameras
            Camera[] cameras = rootObject.GetComponentsInChildren<Camera>();
            foreach (Camera camera in cameras)
            {
                camera.gameObject.tag = "editable";
            }

            // Tag all lights
            Light[] lights = rootObject.GetComponentsInChildren<Light>();
            foreach (Light light in lights)
            {
                light.gameObject.tag = "editable";
            }
        }

        //!
        //! Set layer recursively on GameObject and all children
        //!
        //! @param obj The GameObject to process
        //! @param layer The layer index to set
        //!
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;

            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        //!
        //! Extract texture from material by trying multiple property names
        //!
        //! @param mat The material to extract from
        //! @param propertyNames Array of property names to try
        //! @return The extracted texture, or null if not found
        //!
        private Texture ExtractTexture(Material mat, params string[] propertyNames)
        {
            foreach (string propName in propertyNames)
            {
                if (mat.HasProperty(propName))
                {
                    Texture tex = mat.GetTexture(propName);
                    if (tex != null)
                    {
                        return tex;
                    }
                }
            }

            // Last resort - try mainTexture for base color
            if (propertyNames.Length > 0 && (propertyNames[0].Contains("BaseColor") || propertyNames[0].Contains("MainTex")))
            {
                if (mat.mainTexture != null)
                {
                    return mat.mainTexture;
                }
            }

            return null;
        }

        //!
        //! Extract color from material by trying multiple property names
        //!
        //! @param mat The material to extract from
        //! @param propertyNames Array of property names to try
        //! @return The extracted color, or Color.white if not found
        //!
        private Color ExtractColor(Material mat, params string[] propertyNames)
        {
            foreach (string propName in propertyNames)
            {
                if (mat.HasProperty(propName))
                {
                    return mat.GetColor(propName);
                }
            }
            return Color.white;
        }

        //!
        //! Extract float value from material by trying multiple property names
        //!
        //! @param mat The material to extract from
        //! @param propertyNames Array of property names to try
        //! @return The extracted float value, or 0.0f if not found
        //!
        private float ExtractFloat(Material mat, params string[] propertyNames)
        {
            foreach (string propName in propertyNames)
            {
                if (mat.HasProperty(propName))
                {
                    return mat.GetFloat(propName);
                }
            }
            return 0.0f;
        }

        #endregion

        #region Public Import Methods

        //!
        //! Main import entry point with full UI handling
        //!
        //! @param core The TRACER core instance
        //! @param filePath Absolute path to the glTF/GLB file to import
        //! @param scale Scale factor to apply to the imported object
        //! @return True if import succeeded, false otherwise
        //!
        public async Task<bool> ImportWithDialog(Core core, string filePath, float scale)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Helpers.Log("glTF Loader: File not found: " + filePath, Helpers.logMsgType.ERROR);
                return false;
            }

            Helpers.Log($"glTF Loader: Starting import of {filePath}");

            // Create progress dialog
            Dialog progressDialog = new Dialog();
            UIManager uiManager = core.getManager<UIManager>();
            uiManager.showDialog(progressDialog);
            progressDialog.caption = "Importing glTF...";
            progressDialog.progress = 0;

            try
            {
                bool success = await ImportGLTFAsync(filePath, scale, progressDialog);

                if (success)
                {
                    uiManager.hideMenu();
                }

                return success;
            }
            finally
            {
                uiManager.showDialog(null);
            }
        }

        //!
        //! Complete glTF import pipeline with UI feedback
        //!
        //! @param filePath Absolute path to the glTF/GLB file to import
        //! @param scale Scale factor to apply to the imported object
        //! @param progressDialog Progress dialog to update during import
        //! @return True if import succeeded, false otherwise
        //!
        public async Task<bool> ImportGLTFAsync(string filePath, float scale, Dialog progressDialog)
        {
            try
            {
                // Step 1: Clear existing scene
                progressDialog.caption = "Clearing scene...";
                progressDialog.progress = 10;
                ClearExistingScene();

                // Step 2: Load glTF file and instantiate into scene
                progressDialog.caption = "Loading glTF file...";
                progressDialog.progress = 20;
                GameObject loadedObject = await LoadFile(filePath);

                if (loadedObject == null)
                {
                    Helpers.Log("glTF Loader: Failed to load glTF file", Helpers.logMsgType.ERROR);
                    return false;
                }

                // Step 3: Prepare for TRACER integration
                progressDialog.caption = "Preparing for TRACER...";
                progressDialog.progress = 50;
                await PrepareForTRACER(loadedObject, scale);

                // Step 4: Parse scene to TRACER binary format
                progressDialog.caption = "Parsing to TRACER format...";
                progressDialog.progress = 60;
                await ParseSceneToTRACER();

                // Step 5: Extract and assign textures (iOS fix)
                progressDialog.caption = "Extracting textures...";
                progressDialog.progress = 70;
                await ExtractAndAssignTextures(m_sceneManager.scnRoot);

                // Step 6: Re-parse scene to capture textures
                progressDialog.caption = "Re-parsing with textures...";
                progressDialog.progress = 75;
                await ParseSceneToTRACER();

                // Step 7: Rebuild scene from TRACER binary
                progressDialog.caption = "Rebuilding from TRACER binary...";
                progressDialog.progress = 80;
                await RebuildSceneFromBinary();

                // Step 8: Apply double-sided rendering to rebuilt scene
                progressDialog.caption = "Applying double-sided rendering...";
                progressDialog.progress = 85;
                ApplyDoubleSidedRenderingToRebuiltScene(m_sceneManager.scnRoot);

                // Step 9: Finalize
                progressDialog.caption = "Finalizing...";
                progressDialog.progress = 90;
                m_sceneManager.emitSceneReady();

                progressDialog.progress = 100;
                Helpers.Log($"glTF Loader: Successfully imported {filePath} (SceneObject components attached)");

                return true;
            }
            catch (Exception ex)
            {
                Helpers.Log($"glTF Loader: Error importing file: {ex.Message}\n{ex.StackTrace}", Helpers.logMsgType.ERROR);
                return false;
            }
        }

        //!
        //! Parse Unity scene to TRACER binary format
        //!
        private async Task ParseSceneToTRACER()
        {
            // Use TaskCompletionSource to wait for scene parsing to complete
            TaskCompletionSource<bool> parseComplete = new TaskCompletionSource<bool>();

            EventHandler<EventArgs> parseHandler = null;
            parseHandler = (sender, e) =>
            {
                m_sceneManager.sceneParsed -= parseHandler;
                parseComplete.SetResult(true);
            };

            m_sceneManager.sceneParsed += parseHandler;

            // Trigger scene parsing (this converts Unity scene -> TRACER binary format in memory)
            m_sceneManager.emitParseScene(false);

            // Wait for parsing to complete
            await parseComplete.Task;
        }

        //!
        //! Rebuild Unity scene from TRACER binary format
        //!
        private async Task RebuildSceneFromBinary()
        {
            // Use TaskCompletionSource to wait for scene creation to complete
            TaskCompletionSource<bool> sceneCreationComplete = new TaskCompletionSource<bool>();

            EventHandler<EventArgs> createdHandler = null;
            createdHandler = (sender, e) =>
            {
                m_sceneManager.sceneCreated -= createdHandler;
                sceneCreationComplete.SetResult(true);
            };

            m_sceneManager.sceneCreated += createdHandler;

            // Trigger scene rebuild from binary (destroys temp objects, rebuilds from TRACER format)
            m_sceneManager.emitSceneNew(false);

            // Wait for scene creation to complete
            await sceneCreationComplete.Task;
        }

        //!
        //! Clear all existing scene objects before importing new glTF file
        //!
        private void ClearExistingScene()
        {
            List<SceneObject> allObjects = m_sceneManager.getAllSceneObjects();

            foreach (SceneObject obj in allObjects.ToList())
            {
                if (obj != null && obj.gameObject != null)
                {
                    m_core.removeParameterObject(obj);
                    UnityEngine.Object.DestroyImmediate(obj.gameObject);
                }
            }

            m_sceneManager.sceneCameraList.Clear();

            Helpers.Log("glTF Loader: Cleared existing scene");
        }

        #endregion
    }
}
