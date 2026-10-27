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

//! @file "SceneDataHandler.cs"
//! @brief implementation scene data deserialisation
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 11.03.2022

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using UnityEngine;

namespace tracer
{
    //!
    //! Class handling Unity scene parsing and serialisation.
    //!
    public class SceneParserModule : SceneManagerModule
    {
        //!
        //! The scenes root transform.
        //!
        private Transform scene;
        //!
        //! The layer ID tacked as LOD low.
        //!
        private int m_lodLowLayer;
        //!
        //! The layer ID tacked as LOD high.
        //!
        private int m_lodHighLayer;
        //!
        //! The layer ID tacked as LOD mixed.
        //!
        private int m_lodMixedLayer;


        //!
        //! Constructor
        //!
        public SceneParserModule(string name, Manager manager) : base(name, manager)
        {
            //if (!_core.isServer)
              //  load = false;

            m_lodLowLayer = LayerMask.NameToLayer("LodLow");
            m_lodHighLayer = LayerMask.NameToLayer("LodHigh");
            m_lodMixedLayer = LayerMask.NameToLayer("LodMixed");
        }

        protected override void Init(object sender, EventArgs e)
        {
            scene = GameObject.Find("Scene").transform;
            manager.parseScene += ParseSceneReceived;
        }

        private void ParseSceneReceived(object sender, bool emitSceneReady)
        {
            ParseScene(true, false, true, emitSceneReady);
        }

        //!
        //! Parses the Unity scene filtered by LOD layers and creates binary streams from it.
        //!
        //! @param getLowLayer Gather only scene elements from LOD low layer.
        //! @param getHighLayer Gather only scene elements from LOD high layer.
        //! @param getMixedLayer Gather only scene elements from LOD mixed layer.
        //!
        private void ParseScene(bool getLowLayer = true, bool getHighLayer = false, bool getMixedLayer = true, bool emitSceneReady = true)
        {
            SceneManager.SceneDataHandler.SceneData sceneData = new SceneManager.SceneDataHandler.SceneData();
            NetworkManager networkManager = core.getManager<NetworkManager>();

            if (networkManager != null)
                sceneData.header.senderID = networkManager.cID;
            else
                sceneData.header.senderID = 0;

            sceneData.header.lightIntensityFactor = 1f;
            sceneData.header.frameRate = (byte)core.settings.framerate;

            sceneData.thumbnail = manager.MakeScreenshotJPG(256);

            List<GameObject> gameObjects = new List<GameObject>();
            recursiveGameObjectIdExtract(scene, ref gameObjects, getLowLayer, getHighLayer, getMixedLayer);

            foreach (GameObject gameObject in gameObjects)
            {
                // OPTIMIZATION: Declare the reference as null first. 
                // This avoids creating a useless 'new SceneNode()' object on the heap for every single iteration.
                SceneManager.SceneNode node = null;

                Transform trans = gameObject.transform;
                SceneObject sceneObject = gameObject.GetComponent<SceneObject>();
                Light light = trans.GetComponent<Light>();

                if (light != null)
                {
                    node = ParseLight(light); // Returns a populated SceneNodeLight instance
                    if (core.isServer && !sceneObject)
                    {
                        switch (light.type)
                        {
                            case LightType.Point:
                                sceneObject = SceneObjectPointLight.Attach(gameObject, networkManager.cID);
                                break;
                            case LightType.Directional:
                                sceneObject = SceneObjectDirectionalLight.Attach(gameObject, networkManager.cID);
                                break;
                            case LightType.Spot:
                                sceneObject = SceneObjectSpotLight.Attach(gameObject, networkManager.cID);
                                break;
                            case LightType.Rectangle:
                                sceneObject = SceneObjectAreaLight.Attach(gameObject, networkManager.cID);
                                break;
                        }
                        manager.sceneLightList.Add((SceneObjectLight)sceneObject);
                        gameObject.tag = "editable";
                    }
                }
                else if (trans.GetComponent<Camera>() != null)
                {
                    node = ParseCamera(trans.GetComponent<Camera>()); // Returns a SceneNodeCam instance
                    if (core.isServer && !sceneObject)
                    {
                        sceneObject = SceneObjectCamera.Attach(gameObject, networkManager.cID);
                        manager.sceneCameraList.Add((SceneObjectCamera)sceneObject);
                        gameObject.tag = "editable";
                    }
                }
                else if (trans.GetComponent<MeshFilter>() != null)
                {
                    node = ParseMesh(trans, ref sceneData); // Returns a SceneNodeGeo instance
                    if (gameObject.tag == "editable")
                        if (core.isServer && !sceneObject)
                        {
                            sceneObject = SceneObject.Attach(gameObject, networkManager.cID);
                            manager.simpleSceneObjectList.Add((SceneObject)sceneObject);
                        }
                }
                else if (trans.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    node = ParseSkinnedMesh(trans, ref gameObjects, ref sceneData); // Returns a SceneNodeSkinnedGeo instance
                    if (gameObject.tag == "editable")
                        if (core.isServer && !sceneObject)
                            sceneObject = SceneCharacterObject.Attach(gameObject, networkManager.cID);
                }
                else if (trans.GetComponent<Animator>() != null)
                {
                    node = ParseCharacter(); 
                    if (gameObject.tag == "editable")
                        if (core.isServer && !sceneObject)
                            sceneObject = SceneCharacterObject.Attach(gameObject, networkManager.cID);
                }
                else
                {
                    node = new SceneManager.SceneNode(); // Fallback to base node only if no component matches
                    if (gameObject.tag == "editable")
                        if (core.isServer && !sceneObject)
                        {
                            sceneObject = SceneObject.Attach(gameObject, networkManager.cID);
                            manager.simpleSceneObjectList.Add((SceneObject)sceneObject);
                        }
                }

                // Safety fallback check to prevent NullReferenceExceptions
                if (node == null) node = new SceneManager.SceneNode();

                Animator animator = trans.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.logWarnings = false;
                    processCharacter(animator, ref gameObjects, ref sceneData.characterList);
                }

                // Apply shared transform base data safely to the polymorphism node instance
                node.position.x = trans.localPosition.x;
                node.position.y = trans.localPosition.y;
                node.position.z = trans.localPosition.z;

                node.scale.x = trans.localScale.x;
                node.scale.y = trans.localScale.y;
                node.scale.z = trans.localScale.z;

                node.rotation.x = trans.localRotation.x;
                node.rotation.y = trans.localRotation.y;
                node.rotation.z = trans.localRotation.z;
                node.rotation.w = trans.localRotation.w;

                byte[] tmpName = Encoding.ASCII.GetBytes(trans.name);
                int bytesToCopy = Math.Min(tmpName.Length, 64);

                for (int i = 0; i < bytesToCopy; i++)
                {
                    node.name[i] = tmpName[i];
                }
                for (int i = bytesToCopy; i < 64; i++)
                {
                    node.name[i] = 0; // Padding safety initialization
                }

                node.childCount = 0;
                foreach (Transform child in trans)
                    if (child.gameObject.activeSelf)
                        node.childCount++;

                node.editable = (gameObject.tag == "editable");

                if (trans.name != "root")
                    sceneData.nodeList.Add(node);
            }

            manager.sceneDataHandler.setSceneData(sceneData);

            if (emitSceneReady)
                manager.emitSceneReady();

            manager.emitSceneParsed();
        }

        //!
        //! Function for creating a TRACER light node out of an Unity light object.
        //!
        //! @param light The Unity light for which a TRACER node will be created.
        //! @return The created light node.
        //!
        private SceneManager.SceneNode ParseLight(Light light)
        {
            SceneManager.SceneNodeLight nodeLight = new SceneManager.SceneNodeLight();

            nodeLight.intensity = light.intensity;
            nodeLight.color.x = light.color.r;
            nodeLight.color.y = light.color.g;
            nodeLight.color.z = light.color.b;
            nodeLight.lightType = (SceneManager.LightType) light.type;
            nodeLight.angle = light.spotAngle;
            nodeLight.range = light.range;
            return nodeLight;
        }

        //!
        //! Function for creating a TRACER camera node out of an Unity camera object.
        //!
        //! @param camera The Unity light for which a TRACER node will be created.
        //! @return The created camera node.
        //!
        private SceneManager.SceneNode ParseCamera(Camera camera)
        {
            SceneManager.SceneNodeCam nodeCamera = new SceneManager.SceneNodeCam();

            nodeCamera.fov = camera.fieldOfView;
            nodeCamera.aspect = camera.aspect;
            nodeCamera.near = camera.nearClipPlane;
            nodeCamera.far = camera.farClipPlane;
            // [REVIEW] not supported by Unity?
            nodeCamera.focalDist = 1f;
            nodeCamera.aperture = 2.8f;
            return nodeCamera;
        }

        //!
        //! Function for creating a TRACER geo node out of an Unity object.
        //!
        //! @param transform The transform from the Unity mesh object.
        //! @param sceneData A reference to the structure that will store the serialised scene data. 
        //! @return Created scene node.
        //!
        private SceneManager.SceneNode ParseMesh(Transform location, ref SceneManager.SceneDataHandler.SceneData sceneData)
        {
            SceneManager.SceneNodeGeo nodeGeo = new SceneManager.SceneNodeGeo();
            nodeGeo.color.x = 0f;
            nodeGeo.color.y = 0f;
            nodeGeo.color.z = 0f;
            nodeGeo.color.w = 1f;
            nodeGeo.geoId = processGeometry(location.GetComponent<MeshFilter>().sharedMesh, ref sceneData);

            if (location.GetComponent<Renderer>() != null)
            {
                processMaterial(nodeGeo, location.GetComponent<Renderer>().material, ref sceneData);
            }

            return nodeGeo;
        }

        //!
        //! Function for creating a TRACER light node out of an Unity light object.
        //!
        //! @param light The Unity light for which a TRACER node will be created.
        //! @return The created light node.
        //!
        private SceneManager.SceneNodeCharacter ParseCharacter()
        {
            // empty so far...

            return new SceneManager.SceneNodeCharacter();
        }

        //!
        //! Function for creating a TRACER skinned geo node out of an skinned Unity object.
        //!
        //! @param transform The transform from the Unity mesh object.
        //! @param gameObjectList A reference to the list of the traversed Unity Game Object tree.       
        //! @param sceneData A reference to the structure that will store the serialised scene data. 
        //! @return Created scene node.
        //!
        private SceneManager.SceneNode ParseSkinnedMesh(Transform location, ref List<GameObject> gameObjectList, ref SceneManager.SceneDataHandler.SceneData sceneData)
        {
            SceneManager.SceneNodeSkinnedGeo nodeGeo = new SceneManager.SceneNodeSkinnedGeo();

            SkinnedMeshRenderer sRenderer = location.GetComponent<SkinnedMeshRenderer>();

            // 1. Farbe zuweisen (Float4)
            nodeGeo.color.x = 0f;
            nodeGeo.color.y = 0f;
            nodeGeo.color.z = 0f;
            nodeGeo.color.w = 1f;

            nodeGeo.geoId = processGeometry(sRenderer.sharedMesh, ref sceneData);
            nodeGeo.characterRootID = gameObjectList.IndexOf(location.GetComponentInParent<Animator>().gameObject);

            // 2. Bound Center zuweisen (Float3)
            nodeGeo.boundCenter.x = sRenderer.localBounds.center.x;
            nodeGeo.boundCenter.y = sRenderer.localBounds.center.y;
            nodeGeo.boundCenter.z = sRenderer.localBounds.center.z;

            // 3. Bound Extents zuweisen (Float3)
            nodeGeo.boundExtents.x = sRenderer.localBounds.extents.x;
            nodeGeo.boundExtents.y = sRenderer.localBounds.extents.y;
            nodeGeo.boundExtents.z = sRenderer.localBounds.extents.z;

            nodeGeo.bindPoseLength = sRenderer.sharedMesh.bindposes.Length;

            // 4. Bind Poses befüllen
            // Die Zeile "nodeGeo.bindPoses = new float[...]" fällt weg, da Float1584 bereits festen Speicher hat.
            // Wir begrenzen die Schleife zur Sicherheit auf maximal 99 Poses, um den Puffer der Struktur nicht zu überschreiten.
            int maxPoses = Math.Min(nodeGeo.bindPoseLength, 99);
            for (int i = 0; i < maxPoses; i++)
            {
                int baseIdx = i * 16;
                nodeGeo.bindPoses[baseIdx] = sRenderer.sharedMesh.bindposes[i].m00;
                nodeGeo.bindPoses[baseIdx + 1] = sRenderer.sharedMesh.bindposes[i].m01;
                nodeGeo.bindPoses[baseIdx + 2] = sRenderer.sharedMesh.bindposes[i].m02;
                nodeGeo.bindPoses[baseIdx + 3] = sRenderer.sharedMesh.bindposes[i].m03;
                nodeGeo.bindPoses[baseIdx + 4] = sRenderer.sharedMesh.bindposes[i].m10;
                nodeGeo.bindPoses[baseIdx + 5] = sRenderer.sharedMesh.bindposes[i].m11;
                nodeGeo.bindPoses[baseIdx + 6] = sRenderer.sharedMesh.bindposes[i].m12;
                nodeGeo.bindPoses[baseIdx + 7] = sRenderer.sharedMesh.bindposes[i].m13;
                nodeGeo.bindPoses[baseIdx + 8] = sRenderer.sharedMesh.bindposes[i].m20;
                nodeGeo.bindPoses[baseIdx + 9] = sRenderer.sharedMesh.bindposes[i].m21;
                nodeGeo.bindPoses[baseIdx + 10] = sRenderer.sharedMesh.bindposes[i].m22;
                nodeGeo.bindPoses[baseIdx + 11] = sRenderer.sharedMesh.bindposes[i].m23;
                nodeGeo.bindPoses[baseIdx + 12] = sRenderer.sharedMesh.bindposes[i].m30;
                nodeGeo.bindPoses[baseIdx + 13] = sRenderer.sharedMesh.bindposes[i].m31;
                nodeGeo.bindPoses[baseIdx + 14] = sRenderer.sharedMesh.bindposes[i].m32;
                nodeGeo.bindPoses[baseIdx + 15] = sRenderer.sharedMesh.bindposes[i].m33;
            }

            // 5. Bone IDs vorinitialisieren (Enumerable.Repeat fällt weg, da Int99 bereits existiert)
            for (int i = 0; i < 99; i++)
            {
                nodeGeo.skinnedMeshBoneIDs[i] = -1;
            }

            // 6. Tatsächliche Bones eintragen
            int maxBones = Math.Min(sRenderer.bones.Length, 99);
            for (int i = 0; i < maxBones; i++)
            {
                nodeGeo.skinnedMeshBoneIDs[i] = gameObjectList.IndexOf(sRenderer.bones[i].gameObject);
            }

            processMaterial(nodeGeo, sRenderer.material, ref sceneData);

            return nodeGeo;
        }

        //!
        //! Fills a given list with Unity Game Objects by traversing the scene object tree.
        //!
        //! @location The transform of the Unity Game Object to start at.
        //! @gameObjects The list to be filld with the Game Objects.
        //! @param getLowLayer Gather only scene elements from LOD low layer.
        //! @param getHighLayer Gather only scene elements from LOD high layer.
        //! @param getMixedLayer Gather only scene elements from LOD mixed layer.
        //!
        private void recursiveGameObjectIdExtract(Transform location, ref List<GameObject> gameObjects, bool getLowLayer, bool getHighLayer, bool getMixedLayer)
        {
            foreach (Transform child in location)
                if (child.gameObject.activeSelf &&
                    ((location.gameObject.layer == m_lodLowLayer && getLowLayer) ||
                    (location.gameObject.layer == m_lodHighLayer && getHighLayer) ||
                    (location.gameObject.layer == m_lodMixedLayer && getMixedLayer)))
                {
                    // fill game object list
                    gameObjects.Add(child.gameObject);
                    recursiveGameObjectIdExtract(child, ref gameObjects, getLowLayer, getHighLayer, getMixedLayer);
                }
        }

        //!
        //! Function that adds a TRACER nods material properties.
        //!
        //! @param node The node to which the properties will be added.
        //! @material The Unity material containing the properties to be added.
        //! @param sceneData A reference to the structure that will store the serialised scene data. 
        //!
        private void processMaterial(SceneManager.SceneNodeGeo node, Material material, ref SceneManager.SceneDataHandler.SceneData sceneData)
        {
            if (material == null || material.shader == null)
            {
                node.materialId = -1;
                return;
            }

            for (int i = 0; i < sceneData.materialList.Count; i++)
            {
                if (sceneData.materialList[i].materialID == material.GetInstanceID())
                {
                    node.materialId = i;
                    return; // Stop processing to prevent duplicate material entries in the file
                }
            }

            // Create material package
            SceneManager.MaterialPackage matPack = new SceneManager.MaterialPackage();
            matPack.materialID = material.GetInstanceID();
            matPack.name = material.name;

            // Shader config keywords
            matPack.shaderConfig = new bool[SceneManager.shaderKeywords.Length];
            for (int i = 0; i < SceneManager.shaderKeywords.Length; i++)
            {
                matPack.shaderConfig[i] = material.IsKeywordEnabled(SceneManager.shaderKeywords[i]);
            }

            // Shader property analysis
            Shader shader = material.shader;
            int propertyCount = shader.GetPropertyCount();
            matPack.shaderPropertyIds = new int[propertyCount];
            matPack.shaderPropertyTypes = new int[propertyCount];

            // Texture slots initialization
            int[] texNameIDs = material.GetTexturePropertyNameIDs();
            matPack.textureIds = new int[texNameIDs.Length];
            matPack.textureOffsets = new float[texNameIDs.Length * 2];
            matPack.textureScales = new float[texNameIDs.Length * 2];
            Array.Fill(matPack.textureIds, -1);
            Array.Fill(matPack.textureOffsets, 0f);
            Array.Fill(matPack.textureScales, 1f);

            // OPTIMIZATION: Calculate exact required binary property buffer size upfront to avoid GC garbage allocations
            int totalPropertiesByteSize = 0;
            for (int i = 0; i < propertyCount; i++)
            {
                int propType = (int)shader.GetPropertyType(i);
                if (propType == 0 || propType == 1) totalPropertiesByteSize += 16; // Color or Vector4 = 4 floats (16 bytes)
                else if (propType == 2 || propType == 3) totalPropertiesByteSize += 4;  // Float or Range = 1 float (4 bytes)
            }

            // Allocate the final byte buffer exactly ONCE
            matPack.shaderProperties = new byte[totalPropertiesByteSize];
            Span<byte> propertiesSpan = matPack.shaderProperties;

            int dstIdx = 0;
            int texIdx = 0;

            // Process properties directly into the pre-allocated Span buffer
            for (int i = 0; i < propertyCount; i++)
            {
                int shaderPropertyId = shader.GetPropertyNameId(i);
                matPack.shaderPropertyIds[i] = shaderPropertyId;

                int propType = (int)shader.GetPropertyType(i);
                matPack.shaderPropertyTypes[i] = propType;

                switch (propType)
                {
                    // Color (4 floats = 16 bytes)
                    case 0:
                        Color color = material.GetColor(shaderPropertyId);
                        // Cast the 16-byte slice directly into a Color layout and write it instantly
                        MemoryMarshal.Write(propertiesSpan.Slice(dstIdx, 16), ref color);
                        dstIdx += 16;
                        break;

                    // Vector4 (4 floats = 16 bytes)
                    case 1:
                        Vector4 vec4 = material.GetVector(shaderPropertyId);
                        MemoryMarshal.Write(propertiesSpan.Slice(dstIdx, 16), ref vec4);
                        dstIdx += 16;
                        break;

                    // Float, Range (1 float = 4 bytes)
                    case 2:
                    case 3:
                        float f = material.GetFloat(shaderPropertyId);
                        MemoryMarshal.Write(propertiesSpan.Slice(dstIdx, 4), ref f);
                        dstIdx += 4;
                        break;

                    // Texture maps (Handled dynamically)
                    case 4:
                        Texture baseTex = material.GetTexture(shaderPropertyId);
                        Texture2D texture = baseTex as Texture2D;
                        if (texture != null && texIdx < matPack.textureIds.Length)
                        {
                            matPack.textureIds[texIdx] = processTexture(texture, ref sceneData.textureList);

                            int offsetScaleIdx = texIdx * 2;
                            Vector2 offset = material.GetTextureOffset(shaderPropertyId);
                            Vector2 scale = material.GetTextureScale(shaderPropertyId);

                            matPack.textureOffsets[offsetScaleIdx] = offset.x;
                            matPack.textureOffsets[offsetScaleIdx + 1] = offset.y;
                            matPack.textureScales[offsetScaleIdx] = scale.x;
                            matPack.textureScales[offsetScaleIdx + 1] = scale.y;
                        }
                        else if (baseTex != null)
                        {
                            Helpers.Log($"Texture '{baseTex.name}' is not Texture2D, it's {baseTex.GetType().Name}", Helpers.logMsgType.ERROR);
                        }
                        texIdx++;
                        break;
                }
            }

            // Apply main geometry node fallback color if present
            if (material.HasProperty("_Color"))
            {
                Color mainColor = material.color;
                node.color.x = mainColor.r;
                node.color.y = mainColor.g;
                node.color.z = mainColor.b;
                node.color.w = mainColor.a;
            }

            matPack.type = 0;
            matPack.src = shader.name;

            sceneData.materialList.Add(matPack);
            node.materialId = sceneData.materialList.Count - 1;
        }

        //!
        //! Serialises a Unity mesh into a TRACER Object Package and adds it to the given 
        //! list, if it does not already contain the mesh. Otherweise returns the reference ID of the
        //! already existing mesh. 
        //!
        //! @mesh The mesh to be serialized
        //! @param sceneData A reference to the structure that will store the serialised scene data. 
        //! @return The reference ID for mesh. 
        //!
        private int processGeometry(Mesh mesh, ref SceneManager.SceneDataHandler.SceneData sceneData)
        {
            if (mesh == null) return -1;

            // 1. Check if the mesh is already stored to prevent duplication
            for (int i = 0; i < sceneData.objectList.Count; i++)
            {
                if (sceneData.objectList[i].mesh == mesh)
                {
                    return i;
                }
            }

            SceneManager.ObjectPackage objPack = new SceneManager.ObjectPackage();

            // Cache sizes upfront
            objPack.vSize = mesh.vertexCount;
            objPack.iSize = mesh.triangles.Length;

            // Open the native mesh data container safely for all standard streams
            using (var meshDataArray = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                var meshData = meshDataArray[0];

                // --- 2. VERTICES BLITTING (0% GC Garbage) ---
                int vertexElements = objPack.vSize * 3;
                if (vertexElements > 0)
                {
                    objPack.vertices = new float[vertexElements];
                    using (NativeArray<Vector3> nativePositions = new NativeArray<Vector3>(objPack.vSize, Allocator.Temp))
                    {
                        meshData.GetVertices(nativePositions);
                        ReadOnlySpan<float> floatSpan = MemoryMarshal.Cast<Vector3, float>(nativePositions.AsReadOnlySpan());
                        floatSpan.CopyTo(objPack.vertices);
                    }
                }
                else
                {
                    objPack.vertices = Array.Empty<float>();
                }

                // --- 3. NORMALS BLITTING (0% GC Garbage) ---
                if (mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Normal))
                {
                    objPack.nSize = objPack.vSize;
                    int normalElements = objPack.nSize * 3;
                    objPack.normals = new float[normalElements];

                    using (NativeArray<Vector3> nativeNormals = new NativeArray<Vector3>(objPack.nSize, Allocator.Temp))
                    {
                        meshData.GetNormals(nativeNormals);
                        ReadOnlySpan<float> floatSpan = MemoryMarshal.Cast<Vector3, float>(nativeNormals.AsReadOnlySpan());
                        floatSpan.CopyTo(objPack.normals);
                    }
                }
                else
                {
                    objPack.nSize = 0;
                    objPack.normals = Array.Empty<float>();
                }

                // --- 4. UVS BLITTING (0% GC Garbage) ---
                if (mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0))
                {
                    objPack.uvSize = objPack.vSize;
                    int uvElements = objPack.uvSize * 2;
                    objPack.uvs = new float[uvElements];

                    using (NativeArray<Vector2> nativeUVs = new NativeArray<Vector2>(objPack.uvSize, Allocator.Temp))
                    {
                        meshData.GetUVs(0, nativeUVs);
                        ReadOnlySpan<float> floatSpan = MemoryMarshal.Cast<Vector2, float>(nativeUVs.AsReadOnlySpan());
                        floatSpan.CopyTo(objPack.uvs);
                    }
                }
                else
                {
                    objPack.uvSize = 0;
                    objPack.uvs = Array.Empty<float>();
                }

                // --- 5. TRIANGLE INDICES (0% GC Garbage) ---
                if (objPack.iSize > 0)
                {
                    objPack.indices = new int[objPack.iSize];
                    using (NativeArray<int> nativeIndices = new NativeArray<int>(objPack.iSize, Allocator.Temp))
                    {
                        meshData.GetIndices(nativeIndices, 0);
                        nativeIndices.AsReadOnlySpan().CopyTo(objPack.indices);
                    }
                }
                else
                {
                    objPack.indices = Array.Empty<int>();
                }
            }

            // --- 6. BONE WEIGHTS & INDICES ---
            // Bone weights are extracted via a managed list configuration to stay safe and flexible
            List<BoneWeight> tempWeights = new List<BoneWeight>();
            mesh.GetBoneWeights(tempWeights);
            objPack.bWSize = tempWeights.Count;

            int boneElements = objPack.bWSize * 4;
            if (boneElements > 0)
            {
                objPack.boneWeights = new float[boneElements];
                objPack.boneIndices = new int[boneElements];

                for (int i = 0; i < objPack.bWSize; i++)
                {
                    int baseIdx = i * 4;
                    BoneWeight w = tempWeights[i];

                    objPack.boneWeights[baseIdx + 0] = w.weight0;
                    objPack.boneWeights[baseIdx + 1] = w.weight1;
                    objPack.boneWeights[baseIdx + 2] = w.weight2;
                    objPack.boneWeights[baseIdx + 3] = w.weight3;

                    objPack.boneIndices[baseIdx + 0] = w.boneIndex0;
                    objPack.boneIndices[baseIdx + 1] = w.boneIndex1;
                    objPack.boneIndices[baseIdx + 2] = w.boneIndex2;
                    objPack.boneIndices[baseIdx + 3] = w.boneIndex3;
                }
            }
            else
            {
                objPack.boneWeights = Array.Empty<float>();
                objPack.boneIndices = Array.Empty<int>();
            }

            objPack.mesh = mesh;
            sceneData.objectList.Add(objPack);

            return sceneData.objectList.Count - 1;
        }

        //!
        //! Serialises a Unity texture into a TRACER Texture Package and adds it to the given 
        //! list, if it does not already contain the texture. Otherwise returns the reference ID of the
        //! already existing texture. 
        //!
        //! @texture The texture to be serialized
        //! @textureList The list to add the serialised texture to.
        //! @return The reference ID for texture. 
        //!
        private int processTexture(Texture2D texture, ref List<SceneManager.TexturePackage> textureList)
        {
            for (int i = 0; i < textureList.Count; i++)
            {
                if (textureList[i].texture == texture)
                {
                    return i;
                }
            }

            SceneManager.TexturePackage texPack = new SceneManager.TexturePackage();

            texPack.width = texture.width;
            texPack.height = texture.height;
            texPack.format = texture.format;

            try
            {
                texPack.colorMapData = texture.GetRawTextureData();
                texPack.colorMapDataSize = texPack.colorMapData.Length;
            }
            catch (System.Exception e)
            {
                Helpers.Log($"Failed to get texture data: {e.Message}", Helpers.logMsgType.ERROR);
                texPack.colorMapData = new byte[0];
                texPack.colorMapDataSize = 0;
            }

            texPack.texture = texture;

            textureList.Add(texPack);

            return textureList.Count - 1;
        }

        //!
        //! Serialises a Unity skinned object into a TRACER Character Package and adds it to the given list.
        //!
        //! @animator The Unity animator containing the skinned object.
        //! @characterList The list to add the serialised skinned object to.
        //! @param gameObjectList A reference to the list of the traversed Unity Game Object tree. 
        //!
        private void processCharacter(Animator animator, ref List<GameObject> gameObjectList, ref List<SceneManager.CharacterPackage> characterList)
        {
            if (animator == null) return;

            // Create a new CharacterPackage to store bone data
            SceneManager.CharacterPackage chrPack = new SceneManager.CharacterPackage();
            chrPack.characterRootId = gameObjectList.IndexOf(animator.transform.gameObject);

            // Get all child bones (transforms) of the root object recursively
            List<Transform> boneList = new List<Transform>();
            CollectBones(animator.transform, boneList);

            chrPack.sSize = boneList.Count;

            // FIXED: boneMapping must match the full HumanBodyBones enum capacity (56 slots)
            // to prevent IndexOutOfRangeException when writing specific human bone IDs.
            chrPack.bMSize = 56;
            chrPack.boneMapping = new int[chrPack.bMSize];
            Array.Fill(chrPack.boneMapping, -1);

            chrPack.skeletonMapping = new int[chrPack.sSize];
            chrPack.bonePosition = new float[chrPack.sSize * 3];
            chrPack.boneRotation = new float[chrPack.sSize * 4];
            chrPack.boneScale = new float[chrPack.sSize * 3];

            // Read human skeletal structure safely
            for (int i = 0; i < boneList.Count; i++)
            {
                Transform boneTransform = boneList[i];
                if (boneTransform.name != null)
                {
                    string enumName = boneTransform.name.Replace(" ", "");

                    // Try parse the string safely into Unity's HumanBodyBones enum
                    if (Enum.TryParse<HumanBodyBones>(enumName, true, out HumanBodyBones enumNum))
                    {
                        int boneIdx = (int)enumNum;
                        // Safety boundary check before writing to array slot
                        if (boneIdx >= 0 && boneIdx < chrPack.bMSize)
                        {
                            // OPTIMIZATION: Removed FindDeepChild entirely. We already have the boneTransform from boneList!
                            chrPack.boneMapping[boneIdx] = gameObjectList.IndexOf(boneTransform.gameObject);
                        }
                    }
                }
            }

            // Loop through all bones and collect spatial transformation information flatly
            for (int i = 0; i < boneList.Count; i++)
            {
                Transform boneTransform = boneList[i];
                chrPack.skeletonMapping[i] = gameObjectList.IndexOf(boneTransform.gameObject);

                int idx3 = i * 3;
                int idx4 = i * 4;

                Vector3 localPos = boneTransform.localPosition;
                chrPack.bonePosition[idx3 + 0] = localPos.x;
                chrPack.bonePosition[idx3 + 1] = localPos.y;
                chrPack.bonePosition[idx3 + 2] = localPos.z;

                Quaternion localRot = boneTransform.localRotation;
                chrPack.boneRotation[idx4 + 0] = localRot.x;
                chrPack.boneRotation[idx4 + 1] = localRot.y;
                chrPack.boneRotation[idx4 + 2] = localRot.z;
                chrPack.boneRotation[idx4 + 3] = localRot.w;

                Vector3 localScale = boneTransform.localScale;
                chrPack.boneScale[idx3 + 0] = localScale.x;
                chrPack.boneScale[idx3 + 1] = localScale.y;
                chrPack.boneScale[idx3 + 2] = localScale.z;
            }

            // Add the character package to the character list
            characterList.Add(chrPack);
        }

        private void CollectBones(Transform root, List<Transform> boneList)
        {
            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = root.GetChild(i);
                boneList.Add(child);
                CollectBones(child, boneList); // Recursive step for deep hierarchies
            }
        }
    }
}
