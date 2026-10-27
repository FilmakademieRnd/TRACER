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

//! @file "SceneCreatorModule.cs"
//! @brief implementation of TRACER scene creator module
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 03.08.2022

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using UnityEngine;
using Unity.Collections;
using static tracer.AbstractParameter;

namespace tracer
{
    //!
    //! implementation of TRACER scene creator module
    //!
    public class SceneCreatorModule : SceneManagerModule
    {
        //! The list storing all Unity gameObjects in scene.
        private List<GameObject> gameObjectList = new List<GameObject>();

        //! The list storing Unity materials in scene.
        private List<Material> SceneMaterialList = new List<Material>();

        //! The list storing Unity textures in scene.
        private List<Texture2D> SceneTextureList = new List<Texture2D>();

        //! The list storing Unity meshes in scene.
        private List<Mesh> SceneMeshList = new List<Mesh>();

        //! The scaling factor for every TRACER light source.
        private float m_LightScale;

        //! The client ID from the scene sender, used as SceneObject's sceneID.
        private byte m_senderID;

        //! The scene senders framerate in fps.
        private byte m_frameRate;


        //!
        //! Constructor
        //! Creates an reference to the network manager and connects the scene creation method to the scene received event in the network requester.
        //!
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public SceneCreatorModule(string name, Manager manager) : base(name, manager)
        {
            //if (_core.isServer)
            //    load = false;
        }

        //!
        //! Cleaning up event registrations.
        //!
        public override void Dispose()
        {
            base.Dispose();

            NetworkManager networkManager = core.getManager<NetworkManager>();
           
            manager.sceneNew -= CreateScene;
        }

        //!
        //! Init function of the module.
        //!
        protected override void Init(object sender, EventArgs e)
        {
            NetworkManager networkManager = core.getManager<NetworkManager>();

            manager.sceneNew += CreateScene;
        }

        //!
        //! Function that creates the Unity scene content.
        //!
        public void CreateScene(object o, bool emitSceneReady)
        {
            manager.ResetScene();
            SceneManager.SceneDataHandler sceneDataHandler = manager.sceneDataHandler;
            SceneManager.SceneDataHandler.SceneData sceneData = sceneDataHandler.getSceneData();

            Helpers.Log(string.Format("Build scene from: {0} objects, {1} textures, {2} materials, {3} nodes, {4} parameter objects", sceneData.objectList.Count, sceneData.textureList.Count, sceneData.materialList.Count, sceneData.nodeList.Count, sceneData.parameterObjectList.Count));

            m_LightScale = sceneData.header.lightIntensityFactor;
            m_senderID = sceneData.header.senderID;
            m_frameRate = sceneData.header.frameRate;

            if (manager.settings.loadTextures)
                createTextures(ref sceneData);

            createParameterObjects(ref sceneData);

            createMaterials(ref sceneData);

            createMeshes(ref sceneData);

            createSceneGraphIter(ref sceneData, manager.scnRoot.transform);

            createSkinnedMeshes(ref sceneData, manager.scnRoot.transform);

            foreach (var sceneCharacterObject in manager.getAllSceneObjects())
            {
                if (sceneCharacterObject is SceneCharacterObject character)
                {
                    character.setBones();
                }
            }

            sceneDataHandler.clearSceneByteData();
            // [SEIM] disabled for better  CG!
            //sceneData.clear();
            clearData();

            if (emitSceneReady)
                manager.emitSceneReady();

            manager.emitSceneCreated();
        }

        //!
        //! Function that creates the materials in the Unity scene.
        //!
        //! @param sceneData A reference to the TRACER sceneData.
        //!
        private void createMaterials(ref SceneManager.SceneDataHandler.SceneData sceneData)
        {
            if (sceneData.materialList == null || sceneData.materialList.Count == 0) return;

            // Adjust list capacity upfront to avoid continuous inner resizing overhead
            SceneMaterialList.Capacity = Math.Max(SceneMaterialList.Capacity, SceneMaterialList.Count + sceneData.materialList.Count);

            // Local cache to avoid searching the same custom shaders over and over again via Shader.Find
            Dictionary<string, Shader> customShaderCache = new Dictionary<string, Shader>();

            foreach (SceneManager.MaterialPackage matPack in sceneData.materialList)
            {
                if (matPack == null) continue;

                if (matPack.type == 1)
                {
                    Material mat = new Material(StandardShader);
                    mat.name = matPack.name;

                    if (matPack.textureIds != null && matPack.textureIds.Length > 0)
                    {
                        int firstTexID = matPack.textureIds[0];
                        if (firstTexID > -1 && firstTexID < SceneTextureList.Count)
                        {
                            mat.mainTexture = SceneTextureList[firstTexID];
                        }
                    }

                    SceneMaterialList.Add(mat);
                }
                else if (matPack.type == 0)
                {
                    // Resolve shader using the optimized local dictionary cache
                    if (!customShaderCache.TryGetValue(matPack.src, out Shader customShader))
                    {
                        customShader = Shader.Find(matPack.src);
                        customShaderCache[matPack.src] = customShader;
                    }

                    if (customShader == null) continue;

                    Material mat = new Material(customShader);
                    mat.name = matPack.name;

                    // Apply shader configurations safely
                    if (matPack.shaderConfig != null && matPack.shaderConfig.Length > 4 && matPack.shaderConfig[4])
                    {
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }

                    if (matPack.shaderConfig != null && SceneManager.shaderKeywords != null)
                    {
                        int configLoopLength = Mathf.Min(matPack.shaderConfig.Length, SceneManager.shaderKeywords.Length);
                        for (int i = 0; i < configLoopLength; i++)
                        {
                            if (matPack.shaderConfig[i])
                                mat.EnableKeyword(SceneManager.shaderKeywords[i]);
                            else
                                mat.DisableKeyword(SceneManager.shaderKeywords[i]);
                        }
                    }

                    int dataIdx = 0;
                    int texIdx = 0;
                    ReadOnlySpan<byte> propertiesSpan = matPack.shaderProperties;
                    Shader targetShader = mat.shader;
                    int propertyCount = targetShader.GetPropertyCount();

                    for (int i = 0; i < propertyCount; i++)
                    {
                        if (matPack.shaderPropertyTypes == null || i >= matPack.shaderPropertyTypes.Length) break;

                        int shaderPropertyId = targetShader.GetPropertyNameId(i);

                        switch (matPack.shaderPropertyTypes[i])
                        {
                            // Color (Requires exactly 4 floats = 16 bytes)
                            case 0:
                                if (dataIdx + 16 <= propertiesSpan.Length)
                                {
                                    // Cast the 16-byte slice directly into a Color struct without any loops or BitConverter overhead
                                    ReadOnlySpan<Color> colorCast = MemoryMarshal.Cast<byte, Color>(propertiesSpan.Slice(dataIdx, 16));
                                    mat.SetColor(shaderPropertyId, colorCast[0]);
                                    dataIdx += 16;
                                }
                                break;

                            // Vector4 (Requires exactly 4 floats = 16 bytes)
                            case 1:
                                if (dataIdx + 16 <= propertiesSpan.Length)
                                {
                                    // Cast the 16-byte slice directly into a Vector4 struct
                                    ReadOnlySpan<Vector4> vectorCast = MemoryMarshal.Cast<byte, Vector4>(propertiesSpan.Slice(dataIdx, 16));
                                    mat.SetVector(shaderPropertyId, vectorCast[0]);
                                    dataIdx += 16;
                                }
                                break;

                            // Float, Range (Requires exactly 1 float = 4 bytes)
                            case 2:
                            case 3:
                                if (dataIdx + 4 <= propertiesSpan.Length)
                                {
                                    // Cast the 4-byte slice directly into a float value
                                    ReadOnlySpan<float> floatCast = MemoryMarshal.Cast<byte, float>(propertiesSpan.Slice(dataIdx, 4));
                                    float f = floatCast[0];
                                    mat.SetFloat(shaderPropertyId, f);
                                    dataIdx += 4;

                                    // Change render queue order for transparent or cutout materials
                                    if (i == 23 && (f == 3f || f == 2f))
                                    {
                                        mat.renderQueue = 3000;
                                    }
                                }
                                break;

                            // Texture maps
                            case 4:
                                if (matPack.textureIds != null && texIdx < matPack.textureIds.Length)
                                {
                                    int texID = matPack.textureIds[texIdx];
                                    if (texID > -1 && texID < SceneTextureList.Count)
                                    {
                                        mat.SetTexture(shaderPropertyId, SceneTextureList[texID]);

                                        int offsetScaleIdx = texIdx * 2;
                                        if (matPack.textureOffsets != null && offsetScaleIdx + 1 < matPack.textureOffsets.Length)
                                        {
                                            mat.SetTextureOffset(shaderPropertyId, new Vector2(matPack.textureOffsets[offsetScaleIdx], matPack.textureOffsets[offsetScaleIdx + 1]));
                                        }
                                        if (matPack.textureScales != null && offsetScaleIdx + 1 < matPack.textureScales.Length)
                                        {
                                            mat.SetTextureScale(shaderPropertyId, new Vector2(matPack.textureScales[offsetScaleIdx], matPack.textureScales[offsetScaleIdx + 1]));
                                        }
                                    }
                                    texIdx++;
                                }
                                break;
                        }
                    }

                    SceneMaterialList.Add(mat);
                }
            }
        }

        //!
        //! Function that creates the Tracer ParameterObjects.
        //!
        //! @param sceneData A reference to the TRACER sceneData.
        //!
        private void createParameterObjects(ref SceneManager.SceneDataHandler.SceneData sceneData)
        {
            if (sceneData.parameterObjectList.Count == 0)
                return;

            MenuTree customMenu = new MenuTree();

            customMenu.iconResourceLocation = "Images/button_more+gear";
            customMenu.caption = "Custom Menu";
            UIManager uiManager = core.getManager<UIManager>();
            
            customMenu = customMenu.Begin(MenuItem.IType.VSPLIT);   // <<< begin VSPLIT
            GameObject coreObject = core.transform.gameObject;

            foreach (SceneManager.ParameterObjectPackage po in sceneData.parameterObjectList)
            {
                // create ParameterObjects
                DynamicParameterObject obj = DynamicParameterObject.Attach(coreObject, 255);
                obj.objectName = po.name;

                customMenu = customMenu.Add(po.name, true);
                customMenu = customMenu.Add(MenuItem.IType.SPACE);

                // create the ParameterObject's parameters
                for (int i=0; i<po.pTypes.Length; i++)
                {
                    Type type = toCType((ParameterType) po.pTypes[i]);
                    Type paramType;
                    if (po.pRPC[i])
                        paramType = typeof(RPCParameter<>).MakeGenericType(type);
                    else
                        paramType = typeof(Parameter<>).MakeGenericType(type);

                    AbstractParameter parameter;
                    if (type == typeof(System.Action))
                    {
                        // create delegate to custom Action
                        var methodInfo = typeof(DynamicParameterObject).GetMethod("Empty");
                        var dlg = Delegate.CreateDelegate(typeof(Action), methodInfo);

                        parameter = Activator.CreateInstance(paramType, dlg, po.pNames[i], obj, true) as AbstractParameter;
                    }
                    else
                    {
                        parameter = Activator.CreateInstance(paramType, Activator.CreateInstance(type), po.pNames[i], obj, true) as AbstractParameter;
                    }

                    obj.SubscribeToParameterChange(parameter);

                    customMenu.scrollable = true;
                    customMenu = customMenu.Begin(MenuItem.IType.HSPLIT);  // <<< start HSPLIT

                    customMenu.Add(po.pNames[i]);
                    customMenu.Add(parameter);

                    customMenu.End();  // <<< end HSPLIT
                }
               
                
            }

            customMenu = customMenu.End();     // <<< end VSPLIT

            uiManager.addMenu(customMenu);
        }

        //!
        //! Function that creates the textures in the Unity scene.
        //!
        //! @param sceneData A reference to the TRACER sceneData.
        //!
        private void createTextures(ref SceneManager.SceneDataHandler.SceneData sceneData)
        {
            if (sceneData.textureList == null || sceneData.textureList.Count == 0) return;

            SceneTextureList.Capacity = Math.Max(SceneTextureList.Capacity, SceneTextureList.Count + sceneData.textureList.Count);

            foreach (SceneManager.TexturePackage texPack in sceneData.textureList)
            {
                if (texPack.colorMapData == null || texPack.colorMapData.Length == 0) continue;

                Texture2D tex_2d;

                if (texPack.format != 0)
                {
                    tex_2d = new Texture2D(texPack.width, texPack.height, texPack.format, false);
                    tex_2d.LoadRawTextureData(texPack.colorMapData);

                    tex_2d.Apply(updateMipmaps: false, makeNoLongerReadable: true);
                }
                else
                {
                    tex_2d = new Texture2D(2, 2);

                    if (tex_2d.LoadImage(texPack.colorMapData))
                        tex_2d.Apply(updateMipmaps: false, makeNoLongerReadable: true);
                }

                SceneTextureList.Add(tex_2d);
            }
        }

        //!
        //! Function that creates the meshes in the Unity scene.
        //!
        //! @param sceneData A reference to the TRACER sceneData.
        //!
        private void createMeshes(ref SceneManager.SceneDataHandler.SceneData sceneData)
        {
            if (sceneData.objectList == null || sceneData.objectList.Count == 0) return;

            // Adjust the destination list capacity upfront to prevent resizing
            SceneMeshList.Capacity = Math.Max(SceneMeshList.Capacity, SceneMeshList.Count + sceneData.objectList.Count);

            foreach (SceneManager.ObjectPackage objPack in sceneData.objectList)
            {
                Mesh mesh = new Mesh();
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                // Vertices: Fast memory copy using NativeArray.AsSpan()
                if (objPack.vSize > 0 && objPack.vertices != null)
                {
                    using (NativeArray<Vector3> nativeVertices = new NativeArray<Vector3>(objPack.vSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
                    {
                        ReadOnlySpan<Vector3> sourceSpan = MemoryMarshal.Cast<float, Vector3>(objPack.vertices);
                        // AsSpan() provides a writeable Span<Vector3> which accepts the ReadOnlySpan copy
                        sourceSpan.CopyTo(nativeVertices.AsSpan());

                        mesh.SetVertices(nativeVertices);
                    }
                }

                // Normals: Fast memory copy using NativeArray.AsSpan()
                if (objPack.nSize > 0 && objPack.normals != null)
                {
                    using (NativeArray<Vector3> nativeNormals = new NativeArray<Vector3>(objPack.nSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
                    {
                        ReadOnlySpan<Vector3> sourceSpan = MemoryMarshal.Cast<float, Vector3>(objPack.normals);
                        sourceSpan.CopyTo(nativeNormals.AsSpan());

                        mesh.SetNormals(nativeNormals);
                    }
                }

                // UVs: Fast memory copy using NativeArray.AsSpan()
                if (objPack.uvSize > 0 && objPack.uvs != null)
                {
                    using (NativeArray<Vector2> nativeUVs = new NativeArray<Vector2>(objPack.uvSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
                    {
                        ReadOnlySpan<Vector2> sourceSpan = MemoryMarshal.Cast<float, Vector2>(objPack.uvs);
                        sourceSpan.CopyTo(nativeUVs.AsSpan());

                        mesh.SetUVs(0, nativeUVs);
                    }
                }

                // Triangles / Indices
                if (objPack.indices != null)
                {
                    mesh.triangles = objPack.indices;
                }

                // Bone Weights: Optimized struct assignment loop
                if (objPack.bWSize > 0 && objPack.boneWeights != null && objPack.boneIndices != null)
                {
                    BoneWeight[] weights = new BoneWeight[objPack.bWSize];
                    for (int i = 0; i < objPack.bWSize; i++)
                    {
                        int idx4 = i * 4;
                        weights[i] = new BoneWeight
                        {
                            weight0 = objPack.boneWeights[idx4 + 0],
                            weight1 = objPack.boneWeights[idx4 + 1],
                            weight2 = objPack.boneWeights[idx4 + 2],
                            weight3 = objPack.boneWeights[idx4 + 3],
                            boneIndex0 = objPack.boneIndices[idx4 + 0],
                            boneIndex1 = objPack.boneIndices[idx4 + 1],
                            boneIndex2 = objPack.boneIndices[idx4 + 2],
                            boneIndex3 = objPack.boneIndices[idx4 + 3]
                        };
                    }
                    mesh.boneWeights = weights;
                }

                // Recalculate bounds for accurate camera culling since SetVertices was used
                mesh.RecalculateBounds();

                SceneMeshList.Add(mesh);
            }
        }

        // Cache the layer index globally to eliminate heavy string-lookups inside the recursive loop
        private static readonly int LodMixedLayer = LayerMask.NameToLayer("LodMixed");

        //!
        //! Function that recusively creates the gameObjects in the Unity scene.
        //!
        //! @param sceneData A reference to the TRACER sceneData.
        //! @param _parent The transform of the parant node.
        //! @param idx The index for referencing into the node list.
        //!
        private int createSceneGraphIter(ref SceneManager.SceneDataHandler.SceneData sceneData, Transform parent, int idx = 0, bool isRoot = true)
        {
            // Perform bounds check immediately to safeguard memory access
            if (sceneData.nodeList == null || idx >= sceneData.nodeList.Count) return idx;

            SceneManager.SceneNode node = sceneData.nodeList[idx];
            GameObject obj = CreateObject(node, parent);

            // Optimized to use the globally cached int instead of NameToLayer string lookups
            obj.layer = LodMixedLayer;

            gameObjectList.Add(obj);

            int idxChild = idx;
            Transform currentTransform = obj.transform;

            for (int k = 1; k <= node.childCount; k++)
                idxChild = createSceneGraphIter(ref sceneData, currentTransform, idxChild + 1, false);

            if (isRoot)
            {
                while (idxChild + 1 < sceneData.nodeList.Count)
                    idxChild = createSceneGraphIter(ref sceneData, parent, idxChild + 1, true);
            }

            return idxChild;
        }

        private static readonly string[] CachedHumanBodyBoneNames = InitHumanBodyBoneNames();

        private static string[] InitHumanBodyBoneNames()
        {
            var enums = (HumanBodyBones[])Enum.GetValues(typeof(HumanBodyBones));
            string[] names = new string[enums.Length];
            for (int i = 0; i < enums.Length; i++)
            {
                names[i] = enums[i].ToString();
            }
            return names;
        }

        //!
        //! Function that recusively creates the gameObjects in the Unity scene.
        //!
        //! @param sceneData A reference to the TRACER sceneData.
        //! @param root The transform of the root object.
        //!
        private void createSkinnedMeshes(ref SceneManager.SceneDataHandler.SceneData sceneData, Transform root)
        {
            List<SceneManager.CharacterPackage> characterList = sceneData.characterList;

            if (characterList == null || characterList.Count == 0) return;

            createSkinnedRendererIter(ref sceneData);

            // Find the target scene parent once before entering the loop to avoid severe performance drops
            GameObject sceneObj = GameObject.Find("Scene");
            Transform globalTargetParent = sceneObj != null ? sceneObj.transform.parent : null;

            foreach (SceneManager.CharacterPackage cp in characterList)
            {
                GameObject obj = gameObjectList[cp.characterRootId];
                Transform parentBackup = obj.transform.parent;

                if (globalTargetParent != null)
                {
                    obj.transform.parent = globalTargetParent;
                }

                // Setup HumanBone mapping using pre-cached string names
                HumanBone[] human = new HumanBone[cp.bMSize];
                for (int i = 0; i < human.Length; i++)
                {
                    int boneMapping = cp.boneMapping[i];
                    if (boneMapping == -1) continue;

                    GameObject boneObj = gameObjectList[boneMapping];
                    human[i].boneName = boneObj.name;

                    // Replaced the heavy reflection .ToString() with our zero-allocation cached array
                    human[i].humanName = i < CachedHumanBodyBoneNames.Length ? CachedHumanBodyBoneNames[i] : ((HumanBodyBones)i).ToString();
                    human[i].limit.useDefaultValues = true;
                }

                // Setup SkeletonBone data
                SkeletonBone[] skeleton = new SkeletonBone[cp.sSize];
                skeleton[0].name = obj.name;
                skeleton[0].position = new Vector3(cp.bonePosition[0], cp.bonePosition[1], cp.bonePosition[2]);
                skeleton[0].rotation = new Quaternion(cp.boneRotation[0], cp.boneRotation[1], cp.boneRotation[2], cp.boneRotation[3]);
                skeleton[0].scale = new Vector3(cp.boneScale[0], cp.boneScale[1], cp.boneScale[2]);

                for (int i = 1; i < cp.skeletonMapping.Length; i++)
                {
                    int mappingIdx = cp.skeletonMapping[i];
                    if (mappingIdx != -1)
                    {
                        int idx3 = i * 3;
                        int idx4 = i * 4;

                        skeleton[i].name = gameObjectList[mappingIdx].name;
                        skeleton[i].position = new Vector3(cp.bonePosition[idx3], cp.bonePosition[idx3 + 1], cp.bonePosition[idx3 + 2]);
                        skeleton[i].rotation = new Quaternion(cp.boneRotation[idx4], cp.boneRotation[idx4 + 1], cp.boneRotation[idx4 + 2], cp.boneRotation[idx4 + 3]);
                        skeleton[i].scale = new Vector3(cp.boneScale[idx3], cp.boneScale[idx3 + 1], cp.boneScale[idx3 + 2]);
                    }
                }

                // Build avatar, attach animator and enforce root motion
                Avatar avatar = AvatarBuilder.BuildGenericAvatar(obj, "hip");
                avatar.name = obj.name;

                Animator animator = obj.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.applyRootMotion = true;

                // Restore original parenting state and bind the animation hierarchy
                obj.transform.parent = parentBackup;
                animator.Rebind();
            }
        }

        // Cache the shader globally to eliminate severe performance degradation from Shader.Find inside loops
        private static readonly Shader StandardShader = Shader.Find("Standard");

        //!
        //! Creates an GameObject from an TRACER SceneNode beneath the _parent transform.
        //! @param node TRACER SceneNode to be parsed.
        //! @param parentTransform Unity _parent transform of the GameObject to-be created.
        //! @return The created GameObject.
        //!
        private GameObject CreateObject(SceneManager.SceneNode node, Transform parentTransform)
        {
            Vector3 pos = new Vector3(node.position[0], node.position[1], node.position[2]);
            Quaternion rot = new Quaternion(node.rotation[0], node.rotation[1], node.rotation[2], node.rotation[3]);
            Vector3 scl = new Vector3(node.scale[0], node.scale[1], node.scale[2]);

            // Setup basic object properties
            GameObject objMain = new GameObject();

            // 1. Temporäres managed Array erstellen
            byte[] managedBytes = new byte[64];

            // 2. Daten über den Indexer aus der Struktur kopieren
            for (int i = 0; i < 64; i++)
            {
                managedBytes[i] = node.name[i];
            }

            // 3. String konvertieren und an den Namen übergeben (Trim entfernt eventuelle Null-Bytes am Ende)
            objMain.name = Encoding.ASCII.GetString(managedBytes).TrimEnd('\0');

            //place object
            Transform mainTransform = objMain.transform;
            mainTransform.parent = parentTransform; // GameObject.Find( "Scene" ).transform;
            mainTransform.localPosition = pos;
            mainTransform.localRotation = rot;
            mainTransform.localScale = scl;

            // Use pattern matching to handle geometry nodes elegantly
            if (node is SceneManager.SceneNodeGeo nodeGeo)
            {
                Material mat;
                if (nodeGeo.materialId > -1 && nodeGeo.materialId < SceneMaterialList.Count)
                {
                    mat = SceneMaterialList[nodeGeo.materialId];
                }
                else
                {
                    // Replaced Shader.Find with our globally cached static shader reference
                    mat = new Material(StandardShader);
                    mat.color = new Color(nodeGeo.color[0], nodeGeo.color[1], nodeGeo.color[2], nodeGeo.color[3]);
                }

                // Add correct renderer type using modern C# type checks
                Renderer renderer;
                bool isSkinned = node is SceneManager.SceneNodeSkinnedGeo;

                if (isSkinned)
                    renderer = objMain.AddComponent<SkinnedMeshRenderer>();
                else
                    renderer = objMain.AddComponent<MeshRenderer>();

                renderer.material = mat;

                if (nodeGeo.geoId > -1 && nodeGeo.geoId < SceneMeshList.Count)
                {
                    Mesh mesh = SceneMeshList[nodeGeo.geoId];

                    manager.sceneBoundsMax = Vector3.Max(manager.sceneBoundsMax, renderer.bounds.max);
                    manager.sceneBoundsMin = Vector3.Min(manager.sceneBoundsMin, renderer.bounds.min);

                    if (isSkinned && node is SceneManager.SceneNodeSkinnedGeo sNodeGeo)
                    {
                        SkinnedMeshRenderer sRenderer = (SkinnedMeshRenderer)renderer;
                        sRenderer.localBounds = new Bounds(
                            new Vector3(sNodeGeo.boundCenter[0], sNodeGeo.boundCenter[1], sNodeGeo.boundCenter[2]),
                            new Vector3(sNodeGeo.boundExtents[0] * 2f, sNodeGeo.boundExtents[1] * 2f, sNodeGeo.boundExtents[2] * 2f)
                        );

                        // Precompute parent transformation matrix once outside the bone loop to save CPU cycles
                        Matrix4x4 parentMatrix = Matrix4x4.Scale(parentTransform.localScale) * Matrix4x4.Rotate(parentTransform.localRotation);
                        Matrix4x4[] bindposes = new Matrix4x4[sNodeGeo.bindPoseLength];

                        for (int i = 0; i < sNodeGeo.bindPoseLength; i++)
                        {
                            int idx16 = i * 16;
                            Matrix4x4 bp = new Matrix4x4();
                            bp[0, 0] = sNodeGeo.bindPoses[idx16];
                            bp[0, 1] = sNodeGeo.bindPoses[idx16 + 1];
                            bp[0, 2] = sNodeGeo.bindPoses[idx16 + 2];
                            bp[0, 3] = sNodeGeo.bindPoses[idx16 + 3];
                            bp[1, 0] = sNodeGeo.bindPoses[idx16 + 4];
                            bp[1, 1] = sNodeGeo.bindPoses[idx16 + 5];
                            bp[1, 2] = sNodeGeo.bindPoses[idx16 + 6];
                            bp[1, 3] = sNodeGeo.bindPoses[idx16 + 7];
                            bp[2, 0] = sNodeGeo.bindPoses[idx16 + 8];
                            bp[2, 1] = sNodeGeo.bindPoses[idx16 + 9];
                            bp[2, 2] = sNodeGeo.bindPoses[idx16 + 10];
                            bp[2, 3] = sNodeGeo.bindPoses[idx16 + 11];
                            bp[3, 0] = sNodeGeo.bindPoses[idx16 + 12];
                            bp[3, 1] = sNodeGeo.bindPoses[idx16 + 13];
                            bp[3, 2] = sNodeGeo.bindPoses[idx16 + 14];
                            bp[3, 3] = sNodeGeo.bindPoses[idx16 + 15];

                            bindposes[i] = parentMatrix * bp;
                        }

                        mesh.bindposes = bindposes;
                        sRenderer.sharedMesh = mesh;
                    }
                    else
                    {
                        // Optimized to use the direct return value instead of redundant GetComponent calls
                        MeshFilter filter = objMain.AddComponent<MeshFilter>();
                        filter.mesh = mesh;
                    }
                }

                if (nodeGeo.editable)
                {
                    objMain.tag = "editable";
                    SceneObject sco = SceneObject.Attach(objMain, m_senderID);
                    manager.simpleSceneObjectList.Add(sco);
                }
            }
            else if (node is SceneManager.SceneNodeLight nodeLight)
            {
                Light lightComponent = objMain.AddComponent<Light>();

                lightComponent.type = (UnityEngine.LightType)nodeLight.lightType;
                lightComponent.color = new Color(nodeLight.color[0], nodeLight.color[1], nodeLight.color[2]);
                lightComponent.intensity = nodeLight.intensity * m_LightScale;
                lightComponent.spotAngle = nodeLight.angle;
                lightComponent.shadowBias = 0f;
                lightComponent.shadowNormalBias = 1f;
                lightComponent.range = nodeLight.range * manager.settings.sceneScale;

                if (nodeLight.editable)
                {
                    objMain.tag = "editable";
                    SceneObjectLight sco;
                    switch (nodeLight.lightType)
                    {
                        case SceneManager.LightType.POINT:
                            sco = SceneObjectPointLight.Attach(objMain, m_senderID);
                            lightComponent.shadows = LightShadows.None;
                            break;
                        case SceneManager.LightType.DIRECTIONAL:
                            lightComponent.shadows = LightShadows.Soft;
                            lightComponent.shadowStrength = 0.8f;
                            sco = SceneObjectDirectionalLight.Attach(objMain, m_senderID);
                            break;
                        case SceneManager.LightType.SUN:
                            lightComponent.shadows = LightShadows.Soft;
                            lightComponent.shadowStrength = 0.8f;
                            sco = SceneObjectSunLight.Attach(objMain, m_senderID);
                            break;
                        case SceneManager.LightType.SPOT:
                            lightComponent.range *= 2;
                            lightComponent.shadows = LightShadows.None;
                            sco = SceneObjectSpotLight.Attach(objMain, m_senderID);
                            break;
                        case SceneManager.LightType.AREA:
                            lightComponent.spotAngle = 170;
                            lightComponent.range *= 4;
                            lightComponent.shadows = LightShadows.None;
                            sco = SceneObjectAreaLight.Attach(objMain, m_senderID);
                            break;
                        default:
                            sco = SceneObjectLight.Attach(objMain, m_senderID);
                            Debug.Log($"Unknown Light Type {nodeLight.lightType} in NodeBuilderBasic::CreateLight");
                            break;
                    }
                    manager.sceneLightList.Add(sco);
                }
            }
            else if (node is SceneManager.SceneNodeCam nodeCam)
            {
                Camera camera = objMain.AddComponent<Camera>();
                camera.fieldOfView = nodeCam.fov;
                camera.aspect = nodeCam.aspect;
                camera.nearClipPlane = nodeCam.near;
                camera.farClipPlane = nodeCam.far;
                //camera.focalDistance = nodeCam.focalDist;     // not available in unity
                //camera.aperture = nodeCam.aperture;           // not available in unity
                //disable the component, because its only used for its value and to modify them
                camera.enabled = false;

                if (nodeCam.editable)
                {
                    objMain.tag = "editable";
                    SceneObjectCamera sco = SceneObjectCamera.Attach(objMain, m_senderID);
                    manager.sceneCameraList.Add(sco);
                }
            }
            // Optimized type check using modern pattern matching
            else if (node is SceneManager.SceneNodeCharacter)
            {
                if (node.editable)
                {
                    objMain.tag = "editable";
                    SceneObject sdo = SceneCharacterObject.Attach(objMain, m_senderID);
                }
            }
            // Handle default scene object path
            else
            {
                if (node.editable)
                {
                    objMain.tag = "editable";
                    SceneObject sdo = SceneObject.Attach(objMain, m_senderID);
                    manager.simpleSceneObjectList.Add(sdo);
                }
            }

            // Update scene global extents calculations
            Vector3 sceneExtends = manager.sceneBoundsMax - manager.sceneBoundsMin;
            manager.maxExtend = Mathf.Max(Mathf.Max(sceneExtends.x, sceneExtends.y), sceneExtends.z);

            return objMain;
        }

        //!
        //! function that determines if a texture has alpha
        //! @param  texture   the texture to be checked
        //!
        private bool hasAlpha(Texture2D texture)
        {
            TextureFormat textureFormat = texture.format;
            return (textureFormat == TextureFormat.Alpha8 ||
                textureFormat == TextureFormat.ARGB4444 ||
                textureFormat == TextureFormat.ARGB32 ||
                textureFormat == TextureFormat.DXT5 ||
                textureFormat == TextureFormat.PVRTC_RGBA2 ||
                textureFormat == TextureFormat.PVRTC_RGBA4 ||
                textureFormat == TextureFormat.ETC2_RGBA8);
        }

        //!
        //! Function that adds bone transforms to renderers of SkinnedMesh objects.
        //!
        //! @param sceneData A reference to the TRACER sceneData.
        //! @param _parent the _parent Unity transform.
        //!
        private void createSkinnedRendererIter(ref SceneManager.SceneDataHandler.SceneData sceneData)
        {
            if (sceneData.nodeList == null || sceneData.nodeList.Count == 0) return;

            int characterRootID = -1;
            int skinnedRendererCount = 0;
            SkinnedMeshRenderer[] renderers = System.Array.Empty<SkinnedMeshRenderer>();

            for (int it = 0; it < sceneData.nodeList.Count; it++)
            {
                // Use pattern matching for safe and performant type casting
                if (sceneData.nodeList[it] is SceneManager.SceneNodeSkinnedGeo node)
                {
                    // Update and fetch components only when shifting to a new character root hierarchy
                    if (characterRootID != node.characterRootID)
                    {
                        characterRootID = node.characterRootID;
                        skinnedRendererCount = 0;

                        if (characterRootID >= 0 && characterRootID < gameObjectList.Count && gameObjectList[characterRootID] != null)
                        {
                            renderers = gameObjectList[characterRootID].GetComponentsInChildren<SkinnedMeshRenderer>();
                        }
                        else
                        {
                            renderers = System.Array.Empty<SkinnedMeshRenderer>();
                        }
                    }

                    // Fixed bounds check to prevent IndexOutOfRangeException (strictly less than length)
                    if (skinnedRendererCount < renderers.Length)
                    {
                        SkinnedMeshRenderer renderer = renderers[skinnedRendererCount];
                        if (renderer != null)
                        {
                            renderer.rootBone = renderer.gameObject.transform;
                            List<Transform> validBones = new List<Transform>();

                            // Int99 hat eine feste Größe von 99 Elementen
                            for (int i = 0; i < 99; i++)
                            {
                                int boneID = node.skinnedMeshBoneIDs[i];
                                if (boneID >= 0 && boneID < gameObjectList.Count)
                                {
                                    GameObject boneObj = gameObjectList[boneID];
                                    if (boneObj != null)
                                        validBones.Add(boneObj.transform);
                                    else
                                        validBones.Add(null);
                                }
                                else if (boneID == -1)
                                    break;
                            }

                            // 2. Das bereinigte Array an den Renderer übergeben
                            renderer.bones = validBones.ToArray();
                            renderer.updateWhenOffscreen = true;
                        }
                        skinnedRendererCount++;
                    }
                }
            }
        }

        //!
        //! Function that deletes all Unity scene content and clears the TRACER scene object lists.
        //!
        public void clearData()
        {
            SceneMaterialList.Clear();
            SceneTextureList.Clear();
            SceneMeshList.Clear();
            gameObjectList.Clear();
        }

    }
}
