/*
TRACER FOUNDATION - 
Toolset for Realtime Animation, Collaboration & Extended Reality
tracer.research.animationsinstitut.de
https://github.com/FilmakademieRnd/TRACER

Copyright (c) 2023 Filmakademie Baden-Wuerttemberg, Animationsinstitut R&D Lab

TRACER is a development by Filmakademie Baden-Wuerttemberg, Animationsinstitut
R&D Labs in the scope of the EU funded project MAX-R (101070072) and funding on
the own behalf of Filmakademie Baden-Wuerttemberg.  Former EU projects Dreamspace
(610005) and SAUCE (780470) have inspired the TRACER development.

This program is distributed in the hope that it will be useful, but WITHOUT
ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
FOR A PARTICULAR PURPOSE. See the MIT License for more details.
You should have received a copy of the MIT License along with
this program; if not go to
https://opensource.org/licenses/MIT
*/

//! @file "SceneDataHandler.cs"
//! @brief implementation scene data deserialisation
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 11.03.2022

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace tracer
{
    //!
    //! Class handling scene data deserialisation.
    //!
    public partial class SceneManager : Manager
    {
        public class SceneDataHandler
        {
            //!
            //! Class for storing TRACER scene object information.
            //!
            public class SceneData
            {
                public TracerHeader header;
                public byte[] thumbnail;
                public List<SceneNode> nodeList;
                public List<ObjectPackage> objectList;
                public List<CharacterPackage> characterList;
                public List<GaussianSpaltPackage> gaussianSpaltList;
                public List<TexturePackage> textureList;
                public List<MaterialPackage> materialList;
                public List<ParameterObjectPackage> parameterObjectList;

                public SceneData()
                {
                    header = new TracerHeader();
                    thumbnail = new byte[0];
                    nodeList = new List<SceneNode>();
                    objectList = new List<ObjectPackage>();
                    characterList = new List<CharacterPackage>();
                    gaussianSpaltList = new List<GaussianSpaltPackage>();
                    textureList = new List<TexturePackage>();
                    materialList = new List<MaterialPackage>();
                    parameterObjectList = new List<ParameterObjectPackage>();
                }

                // [SEIM] deactivated to enhance GC!
                //~SceneData() { clear(); }

                //public void clear()
                //{
                //    nodeList.Clear();
                //    objectList.Clear();
                //    characterList.Clear();
                //    gaussianSpaltList.Clear();
                //    textureList.Clear();
                //    materialList.Clear();
                //    parameterObjectList.Clear();
                //}
            }

            //!
            //! storing system specific int size for faster access.
            //!
            public static readonly int size_bool = sizeof(bool);
            //!
            //! storing system specific int size for faster access.
            //!
            public static readonly int size_int = sizeof(int);
            //!
            //! storing system specific float size for faster access.
            //!
            public static readonly int size_float = sizeof(float);
            //!
            //! The array containing the serialised header.
            //!
            private byte[] m_headerByteData;
            //!
            //! Getter function returning a reference to the byte array  
            //! containing the serialised header data.
            //!
            //! @return A reference to the serialised header data.
            //!
            public ref byte[] headerByteDataRef
            {
                get { return ref m_headerByteData; }
            }
            //!
            //! Setter function for setting a the byte array,
            //! containing the serialised header data.
            //!
            public byte[] headerByteData
            {
                set { m_headerByteData = value; }
                get { return m_headerByteData; }
            }
            //!
            //! The array containing the serialised scene thumbnail.
            //!
            private byte[] m_thumbnailByteData;
            //!
            //! Getter function returning a reference to the byte array  
            //! containing the serialised scene thumbnail.
            //!
            //! @return A reference to the serialised scene thumbnail data.
            //!
            public ref byte[] thumbnailByteDataRef
            {
                get { return ref m_thumbnailByteData; }
            }
            //!
            //! Setter function for setting a the byte array,
            //! containing the serialised scene thumbnail data.
            //!
            public byte[] thumbnailByteData
            {
                set { m_thumbnailByteData = value; }
                get { return m_thumbnailByteData; }
            }
            //!
            //! The list containing the serialised nodes.
            //!
            private byte[] m_nodesByteData;
            //!
            //! Getter function returning a reference to the byte array  
            //! containing the serialised nodes data.
            //!
            //! @return A reference to the serialised nodes data.
            //!
            public ref byte[] nodesByteDataRef
            {
                get { return ref m_nodesByteData; }
            }
            //!
            //! Setter function for setting a the byte array,
            //! containing the serialised nodes data.
            //!
            public byte[] nodesByteData
            {
                set { m_nodesByteData = value; }
            }
            //!
            //! The list containing the serialised meshes.
            //!
            private byte[] m_objectsByteData;
            //!
            //! Getter function returning a reference to the byte array  
            //! containing the serialised objects data.
            //!
            //! @return A reference to the serialised objects data.
            //!
            public ref byte[] objectsByteDataRef
            {
                get { return ref m_objectsByteData; }
            }
            //!
            //! Setter function for setting a the byte array,
            //! containing the serialised object data.
            //!
            public byte[] objectsByteData
            {
                set { m_objectsByteData = value; }
            }
            //!
            //! The list containing the serialised skinned meshes.
            //!
            private byte[] m_characterByteData;
            //!
            //! Getter function returning a reference to the byte array  
            //! containing the serialised characters data.
            //!
            //! @return A reference to the serialised characters data.
            //!
            public ref byte[] characterByteDataRef
            {
                get { return ref m_characterByteData; }
            }
            //!
            //! Setter function for setting a the byte array,
            //! containing the serialised skinned mesh data.
            //!
            public byte[] characterByteData
            {
                set { m_characterByteData = value; }
            }
            //!
            //! The list containing the gaussian splat objects.
            //!
            private byte[] m_gaussianSplatByteData;
            //!
            //! Getter function returning a reference to the byte array  
            //! containing the gaussian splat objects.
            //!
            //! @return A reference to the serialised gaussian splat objects data.
            //!
            public ref byte[] gaussianSplatByteDataRef
            {
                get { return ref m_gaussianSplatByteData; }
            }
            //!
            //! Setter function for setting a the byte array,
            //! containing the serialised gaussian splat object data.
            //!
            public byte[] gaussianSplatByteData
            {
                set { m_gaussianSplatByteData = value; }
            }
            //!
            //! The list containing the serialised textures.
            //!
            private byte[] m_texturesByteData;
            //!
            //! Getter function returning a reference to the byte array  
            //! containing the serialised textures data.
            //!
            //! @return A reference to the serialised textures data.
            //!
            public ref byte[] texturesByteDataRef
            {
                get { return ref m_texturesByteData; }
            }
            //!
            //! Setter function for setting a the byte array,
            //! containing the serialised texture data.
            //!
            public byte[] texturesByteData
            {
                set { m_texturesByteData = value; }
            }
            //!
            //! The list containing the serialised materials.
            //!
            private byte[] m_materialsByteData;
            //!
            //! Getter function returning a reference to the byte array  
            //! containing the serialised materials data.
            //!
            //! @return A reference to the serialised materials data.
            //!
            public ref byte[] materialsByteDataRef
            {
                get { return ref m_materialsByteData; }
            }
            //!
            //! Setter function for setting a the byte array,
            //! containing the serialised material data.
            //!
            public byte[] materialsByteData
            {
                set { m_materialsByteData = value; }
            }

            //!
            //! The list containing the serialised ParameterObjects.
            //!
            private byte[] m_parameterObjectsByteData;
            //!
            //! Getter function returning a reference to the byte array  
            //! containing the serialised ParameterObjects data.
            //!
            //! @return A reference to the serialised ParameterObjects data.
            //!
            public ref byte[] parameterObjectsByteDataRef
            {
                get { return ref m_parameterObjectsByteData; }
            }
            //!
            //! Setter function for setting a the byte array,
            //! containing the serialised material data.
            //!
            public byte[] parameterObjectsByteData
            {
                set { m_parameterObjectsByteData = value; }
            }
            //!
            //! Initialisation of the lists storing deserialised the scene data.
            //!
            public SceneDataHandler()
            {
            }

            ~SceneDataHandler()
            {
                clearSceneByteData();
            }

            //!
            //! Deletes all scene byte data stored in the SceneDataHandler.
            //!
            public void clearSceneByteData()
            {
                m_headerByteData = new byte[0];
                m_thumbnailByteData = new byte[0];
                m_nodesByteData = new byte[0];
                m_objectsByteData = new byte[0];
                m_characterByteData = new byte[0];
                m_gaussianSplatByteData = new byte[0];
                m_texturesByteData = new byte[0];
                m_materialsByteData = new byte[0];
                m_parameterObjectsByteData = new byte[0];
            }

            //!
            //! Function to convert byte arrays into TRACER SceneData.
            //! Make shure to set the respective byte arrays before calling this function!
            //!
            public SceneData getSceneData()
            {
                SceneData sceneData = new SceneData();

                if (m_headerByteData != null && m_headerByteData.Length > 0)
                    sceneData.header = convertHeaderByteStream();
                else
                    Helpers.Log("SceneDataHandler: Header byte array null or empty!", Helpers.logMsgType.WARNING);

                if (m_nodesByteData != null && m_nodesByteData.Length > 0)
                    sceneData.nodeList = convertNodesByteStream();
                else
                    Helpers.Log("SceneDataHandler: Nodes byte array null or empty!", Helpers.logMsgType.WARNING);

                if (m_objectsByteData != null && m_objectsByteData.Length > 0)
                    sceneData.objectList = convertObjectsByteStream();
                else
                    Helpers.Log("SceneDataHandler: Objects byte array null or empty!", Helpers.logMsgType.WARNING);

                if (m_characterByteData != null && m_characterByteData.Length > 0)
                    sceneData.characterList = convertCharacterByteStream();
                else
                    Helpers.Log("SceneDataHandler: Character byte array null or empty!", Helpers.logMsgType.WARNING);

                if (m_gaussianSplatByteData != null && m_gaussianSplatByteData.Length > 0)
                    sceneData.gaussianSpaltList = convertGaussianSplatByteStream();
                else
                    Helpers.Log("SceneDataHandler: Gaussian Splat byte array null or empty!", Helpers.logMsgType.WARNING);

                if (m_texturesByteData != null && m_texturesByteData.Length > 0)
                    sceneData.textureList = convertTexturesByteStream(ref sceneData);
                else
                    Helpers.Log("SceneDataHandler: Textures byte array null or empty!", Helpers.logMsgType.WARNING);

                if (m_materialsByteData != null && m_materialsByteData.Length > 0)
                    sceneData.materialList = convertMaterialsByteStream();
                else
                    Helpers.Log("SceneDataHandler: Materieals byte array null or empty!", Helpers.logMsgType.WARNING);
                
                if (m_parameterObjectsByteData != null && m_parameterObjectsByteData.Length > 0)
                    sceneData.parameterObjectList = convertParameterObjectsByteStream();
                else
                    Helpers.Log("SceneDataHandler: Parameter Objects byte array null or empty!", Helpers.logMsgType.WARNING);

                return sceneData;
            }

            //!
            //! Function to convert SceneData into byte arrays.
            //! SceneData will be deleted during this process!
            //! 
            //! @param sceneData the struct containing the TRACER scene data.
            //!
            public void setSceneData(SceneData sceneData)
            {
                if (sceneData == null) return;

                // create byte arrays and clear buffers
                m_headerByteData = new byte[8];
                using (MemoryStream stream = new MemoryStream(m_headerByteData))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    sceneData.header.Serialize(writer);
                }

                m_thumbnailByteData = sceneData.thumbnail;

                getNodesByteArray(sceneData.nodeList);
                sceneData.nodeList.Clear();

                getObjectsByteArray(sceneData.objectList);
                sceneData.objectList.Clear();

                getCharacterByteArray(sceneData.characterList);
                sceneData.characterList.Clear();

                getGaussianSplatByteArray(sceneData.gaussianSpaltList);
                sceneData.gaussianSpaltList.Clear();

                getTexturesByteArray(sceneData.textureList);
                sceneData.textureList.Clear();

                getMaterialsByteArray(sceneData.materialList);
                sceneData.materialList.Clear();

                getParameterObjectsByteArray(sceneData.parameterObjectList);
                sceneData.parameterObjectList.Clear();
            }

            //!
            //! The function deserialises a header byte stream and stores it as scene defaults.
            //!
            private TracerHeader convertHeaderByteStream()
            {
                // Ensure we actually have data and it contains at least the 8 required bytes
                if (m_headerByteData == null || m_headerByteData.Length < 8)
                {
                    Debug.LogError("[Parser] Header byte data is empty or invalid!");
                    return null;
                }

                TracerHeader header = new TracerHeader();

                // Open a safe binary stream over the header byte array
                using (MemoryStream stream = new MemoryStream(m_headerByteData))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    try
                    {
                        // Populate the header fields using the newly implemented interface method
                        header.Populate(reader);

                        Debug.Log($"[Parser] Header successfully loaded. Intensity Factor: {header.lightIntensityFactor}, Sender ID: {header.senderID}, FPS: {header.frameRate}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Parser] Failed to deserialize TracerHeader under WebGL: {ex.Message}");
                        return null;
                    }
                }

                return header;
            }

            //!
            //! The function deserialises a node byte stream and stores it into the sceneNode list.
            //!
            //!
            //! The function deserialises a node byte stream and stores it into the sceneNode list.
            //!
            private List<SceneNode> convertNodesByteStream()
            {
                List<SceneManager.SceneNode> nodeList = new List<SceneManager.SceneNode>();

                if (m_nodesByteData == null || m_nodesByteData.Length == 0)
                    return nodeList;

                int dataIdx = 0;

                using (MemoryStream stream = new MemoryStream(m_nodesByteData))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    while (dataIdx < m_nodesByteData.Length)
                    {
                        stream.Position = dataIdx;

                        // 1. Header lesen
                        NodeType nodeType = (NodeType)reader.ReadInt32();
                        dataIdx += 4;

                        // 2. Reine Instanziierung über den Switch
                        SceneNode node = null;
                        switch (nodeType)
                        {
                            case NodeType.GROUP: node = new SceneManager.SceneNode(); break;
                            case NodeType.CHARACTER: node = new SceneManager.SceneNodeCharacter(); break;
                            case NodeType.GEO: node = new SceneManager.SceneNodeGeo(); break;
                            case NodeType.SKINNEDMESH: node = new SceneManager.SceneNodeSkinnedGeo(); break;
                            case NodeType.LIGHT: node = new SceneManager.SceneNodeLight(); break;
                            case NodeType.CAMERA: node = new SceneManager.SceneNodeCam(); break;
                            default:
                                throw new InvalidOperationException($"Unbekannter Typ: {nodeType}");
                        }

                        if (node is IBinarySerializable serializable)
                        {
                            serializable.Populate(reader);
                            dataIdx += serializable.FileStructureSize;
                        }

                        nodeList.Add(node);
                    }
                }
                return nodeList;
            }

            //!
            //! The function deserialises a material byte stream and stores it into the material list.
            //!
            private List<MaterialPackage> convertMaterialsByteStream()
            {
                List<MaterialPackage> materialList = new List<MaterialPackage>();

                if (m_materialsByteData == null || m_materialsByteData.Length == 0)
                {
                    return materialList;
                }

                // Create a global span over the source data for fast and safe slicing
                ReadOnlySpan<byte> sourceSpan = m_materialsByteData;
                int dataIdx = 0;

                // Iterate through the byte stream until the end is reached
                while (dataIdx < sourceSpan.Length)
                {
                    MaterialPackage matPack = new MaterialPackage();

                    // 1. Get type (int)
                    matPack.type = BitConverter.ToInt32(m_materialsByteData, dataIdx);
                    dataIdx += size_int;

                    // 2. Get material name length (int)
                    int nameLength = BitConverter.ToInt32(m_materialsByteData, dataIdx);
                    dataIdx += size_int;

                    // 3. Get material name (string)
                    if (nameLength > 0)
                    {
                        ReadOnlySpan<byte> nameSpan = sourceSpan.Slice(dataIdx, nameLength);
                        matPack.name = Encoding.ASCII.GetString(nameSpan);
                        dataIdx += nameLength;
                    }
                    else
                    {
                        matPack.name = string.Empty;
                    }

                    // 4. Get material src length (int)
                    int srcLength = BitConverter.ToInt32(m_materialsByteData, dataIdx);
                    dataIdx += size_int;

                    // 5. Get material src (string)
                    if (srcLength > 0)
                    {
                        ReadOnlySpan<byte> srcSpan = sourceSpan.Slice(dataIdx, srcLength);
                        matPack.src = Encoding.ASCII.GetString(srcSpan);
                        dataIdx += srcLength;
                    }
                    else
                    {
                        matPack.src = string.Empty;
                    }

                    // 6. Get material ID (int)
                    matPack.materialID = BitConverter.ToInt32(m_materialsByteData, dataIdx);
                    dataIdx += size_int;

                    // 7. Get texture count size (int)
                    int textureCount = BitConverter.ToInt32(m_materialsByteData, dataIdx);
                    dataIdx += size_int;

                    // 8. Get textureIds (int[])
                    matPack.textureIds = new int[textureCount];
                    if (textureCount > 0)
                    {
                        int byteLength = textureCount * size_int;
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                        MemoryMarshal.Cast<byte, int>(byteSlice).CopyTo(matPack.textureIds);
                        dataIdx += byteLength;
                    }

                    // 9. Get textureOffsets (float[]) - Size is textureCount * 2
                    int offsetElements = textureCount * 2;
                    matPack.textureOffsets = new float[offsetElements];
                    if (offsetElements > 0)
                    {
                        int byteLength = offsetElements * size_float;
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                        MemoryMarshal.Cast<byte, float>(byteSlice).CopyTo(matPack.textureOffsets);
                        dataIdx += byteLength;
                    }

                    // 10. Get textureScales (float[]) - Size is textureCount * 2
                    int scaleElements = textureCount * 2;
                    matPack.textureScales = new float[scaleElements];
                    if (scaleElements > 0)
                    {
                        int byteLength = scaleElements * size_float;
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                        MemoryMarshal.Cast<byte, float>(byteSlice).CopyTo(matPack.textureScales);
                        dataIdx += byteLength;
                    }

                    // 11. Optional Shader Parsing (if type != 1)
                    if (matPack.type != 1)
                    {
                        // Shader config size (int)
                        int shaderConfigLength = BitConverter.ToInt32(m_materialsByteData, dataIdx);
                        dataIdx += size_int;

                        // Shader config (bool[]) - bool is 1 byte per element in this structure
                        matPack.shaderConfig = new bool[shaderConfigLength];
                        if (shaderConfigLength > 0)
                        {
                            ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, shaderConfigLength);
                            MemoryMarshal.Cast<byte, bool>(byteSlice).CopyTo(matPack.shaderConfig);
                            dataIdx += shaderConfigLength;
                        }

                        // Shader property count (int)
                        int propertyCount = BitConverter.ToInt32(m_materialsByteData, dataIdx);
                        dataIdx += size_int;

                        // Shader property IDs (int[])
                        matPack.shaderPropertyIds = new int[propertyCount];
                        if (propertyCount > 0)
                        {
                            int byteLength = propertyCount * size_int;
                            ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                            MemoryMarshal.Cast<byte, int>(byteSlice).CopyTo(matPack.shaderPropertyIds);
                            dataIdx += byteLength;
                        }

                        // Shader property types (int[])
                        matPack.shaderPropertyTypes = new int[propertyCount];
                        if (propertyCount > 0)
                        {
                            int byteLength = propertyCount * size_int;
                            ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                            MemoryMarshal.Cast<byte, int>(byteSlice).CopyTo(matPack.shaderPropertyTypes);
                            dataIdx += byteLength;
                        }

                        // Shader properties data size (int)
                        int propertiesDataSize = BitConverter.ToInt32(m_materialsByteData, dataIdx);
                        dataIdx += size_int;

                        // Shader property raw data (byte[])
                        matPack.shaderProperties = new byte[propertiesDataSize];
                        if (propertiesDataSize > 0)
                        {
                            ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, propertiesDataSize);
                            byteSlice.CopyTo(matPack.shaderProperties);
                            dataIdx += propertiesDataSize;
                        }
                    }

                    materialList.Add(matPack);
                }

                return materialList;
            }

            //!
            //! The function deserialises a ParameterObjects byte stream and stores it into the ParameterObjects list.
            //!
            private List<ParameterObjectPackage> convertParameterObjectsByteStream()
            {
                List<ParameterObjectPackage> parameterObjectList = new List<ParameterObjectPackage>();

                if (m_parameterObjectsByteData == null || m_parameterObjectsByteData.Length == 0)
                    return parameterObjectList;

                // Create a global span over the source data for fast and safe slicing
                ReadOnlySpan<byte> sourceSpan = m_parameterObjectsByteData;
                int dataIdx = 0;

                // Iterate through the byte stream until the end is reached
                while (dataIdx < sourceSpan.Length)
                {
                    ParameterObjectPackage poPack = new ParameterObjectPackage();

                    // Get _id (int)
                    poPack.id = BitConverter.ToInt32(m_parameterObjectsByteData, dataIdx);
                    dataIdx += size_int;

                    // parameter object name length (int)
                    int nameLength = BitConverter.ToInt32(m_parameterObjectsByteData, dataIdx);
                    dataIdx += size_int;

                    // parameter object name (string)
                    if (nameLength > 0)
                    {
                        ReadOnlySpan<byte> nameSpan = sourceSpan.Slice(dataIdx, nameLength);
                        poPack.name = Encoding.ASCII.GetString(nameSpan);
                        dataIdx += nameLength;
                    }
                    else
                        poPack.name = string.Empty;

                    // number of parameters (int)
                    int numParameters = BitConverter.ToInt32(m_parameterObjectsByteData, dataIdx);
                    dataIdx += size_int;

                    // parameter types (int[])
                    poPack.pTypes = new int[numParameters];
                    if (numParameters > 0)
                    {
                        int byteLength = numParameters * size_int;
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                        MemoryMarshal.Cast<byte, int>(byteSlice).CopyTo(poPack.pTypes);
                        dataIdx += byteLength;
                    }

                    // parameter RPC properties (bool[])
                    poPack.pRPC = new bool[numParameters];
                    if (numParameters > 0)
                    {
                        // bool is 1 byte per element in this structure
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, numParameters);
                        MemoryMarshal.Cast<byte, bool>(byteSlice).CopyTo(poPack.pRPC);
                        dataIdx += numParameters;
                    }

                    // parameter names (string[])
                    poPack.pNames = new string[numParameters];
                    for (int i = 0; i < numParameters; i++)
                    {
                        // Read individual string length header
                        int strLength = BitConverter.ToInt32(m_parameterObjectsByteData, dataIdx);
                        dataIdx += size_int;

                        if (strLength > 0)
                        {
                            ReadOnlySpan<byte> strSpan = sourceSpan.Slice(dataIdx, strLength);
                            poPack.pNames[i] = Encoding.ASCII.GetString(strSpan);
                            dataIdx += strLength;
                        }
                        else
                            poPack.pNames[i] = string.Empty;
                    }

                    parameterObjectList.Add(poPack);
                }

                return parameterObjectList;
            }

            private List<GaussianSpaltPackage> convertGaussianSplatByteStream()
            {
                if (m_gaussianSplatByteData == null || m_gaussianSplatByteData.Length == 0)
                    return new List<GaussianSpaltPackage>();

                // Konservative Schätzung der Kapazität, um internes Listen-Resizing zu vermeiden
                int estimatedCapacity = m_gaussianSplatByteData.Length / 4096; // 4KB als grober Richtwert pro Paket
                List<GaussianSpaltPackage> gaussianSplatList = new List<GaussianSpaltPackage>(Math.Max(4, estimatedCapacity));

                ReadOnlySpan<byte> sourceSpan = m_gaussianSplatByteData;
                int dataIdx = 0;

                while (dataIdx < sourceSpan.Length)
                {
                    GaussianSpaltPackage gsPack = new GaussianSpaltPackage();

                    // 1. Die 11 Integer-Werte via BinaryPrimitives auslesen
                    gsPack.version = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;
                    gsPack.count = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;
                    gsPack.shNcount = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;
                    gsPack.shNbands = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;
                    gsPack.meansLSize = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;
                    gsPack.meansUSize = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;
                    gsPack.scalesSize = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;
                    gsPack.quadsSize = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;
                    gsPack.sh0Size = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;
                    gsPack.shNcentroidsSize = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;
                    gsPack.shNlablesSize = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;

                    // 2. Float-Arrays (min, max, codebooks) via MemoryMarshal einlesen
                    // min (3 floats = 12 Bytes)
                    gsPack.min = new float[3];
                    MemoryMarshal.Cast<byte, float>(sourceSpan.Slice(dataIdx, 12)).CopyTo(gsPack.min);
                    dataIdx += 12;

                    // max (3 floats = 12 Bytes)
                    gsPack.max = new float[3];
                    MemoryMarshal.Cast<byte, float>(sourceSpan.Slice(dataIdx, 12)).CopyTo(gsPack.max);
                    dataIdx += 12;

                    // scalesCodebook (256 floats = 1024 Bytes)
                    gsPack.scalesCodebook = new float[256];
                    MemoryMarshal.Cast<byte, float>(sourceSpan.Slice(dataIdx, 1024)).CopyTo(gsPack.scalesCodebook);
                    dataIdx += 1024;

                    // sh0Codebook (256 floats = 1024 Bytes)
                    gsPack.sh0Codebook = new float[256];
                    MemoryMarshal.Cast<byte, float>(sourceSpan.Slice(dataIdx, 1024)).CopyTo(gsPack.sh0Codebook);
                    dataIdx += 1024;

                    // shNcodebook (256 floats = 1024 Bytes)
                    gsPack.shNcodebook = new float[256];
                    MemoryMarshal.Cast<byte, float>(sourceSpan.Slice(dataIdx, 1024)).CopyTo(gsPack.shNcodebook);
                    dataIdx += 1024;

                    // 3. Die Byte-Arrays (Nutzdaten) zurückkopieren
                    gsPack.c_meansL = new byte[gsPack.meansLSize];
                    sourceSpan.Slice(dataIdx, gsPack.meansLSize).CopyTo(gsPack.c_meansL);
                    dataIdx += gsPack.meansLSize;

                    gsPack.c_meansU = new byte[gsPack.meansUSize];
                    sourceSpan.Slice(dataIdx, gsPack.meansUSize).CopyTo(gsPack.c_meansU);
                    dataIdx += gsPack.meansUSize;

                    gsPack.c_scales = new byte[gsPack.scalesSize];
                    sourceSpan.Slice(dataIdx, gsPack.scalesSize).CopyTo(gsPack.c_scales);
                    dataIdx += gsPack.scalesSize;

                    gsPack.c_quads = new byte[gsPack.quadsSize];
                    sourceSpan.Slice(dataIdx, gsPack.quadsSize).CopyTo(gsPack.c_quads);
                    dataIdx += gsPack.quadsSize;

                    gsPack.c_sh0 = new byte[gsPack.sh0Size];
                    sourceSpan.Slice(dataIdx, gsPack.sh0Size).CopyTo(gsPack.c_sh0);
                    dataIdx += gsPack.sh0Size;

                    gsPack.c_shNcentroids = new byte[gsPack.shNcentroidsSize];
                    sourceSpan.Slice(dataIdx, gsPack.shNcentroidsSize).CopyTo(gsPack.c_shNcentroids);
                    dataIdx += gsPack.shNcentroidsSize;

                    gsPack.c_shNlables = new byte[gsPack.shNlablesSize];
                    sourceSpan.Slice(dataIdx, gsPack.shNlablesSize).CopyTo(gsPack.c_shNlables);
                    dataIdx += gsPack.shNlablesSize;

                    gaussianSplatList.Add(gsPack);
                }

                return gaussianSplatList;
            }

            //!
            //! The function deserialises a texture byte stream and stores it into the texture list.
            //!
            //! @param sceneData The scene data for checking the tyture type.
            //!
            private List<TexturePackage> convertTexturesByteStream(ref SceneData sceneData)
            {
                if (m_texturesByteData == null || m_texturesByteData.Length == 0)
                    return new List<TexturePackage>();

                // Conservative capacity estimation to prevent continuous inner list resizing overhead
                // Using a baseline rough estimate of 64KB per texture package
                int estimatedCapacity = m_texturesByteData.Length / 65536;
                List<TexturePackage> textureList = new List<TexturePackage>(Math.Max(4, estimatedCapacity));

                // Create a global span over the source data for fast and safe slicing
                ReadOnlySpan<byte> sourceSpan = m_texturesByteData;
                int dataIdx = 0;

                // Iterate through the byte stream until the end is reached
                while (dataIdx < sourceSpan.Length)
                {
                    TexturePackage texPack = new TexturePackage();

                    // Read metadata integers safely via BinaryPrimitives (Little Endian forced)
                    texPack.width = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;

                    texPack.height = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;

                    int formatValue = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;
                    texPack.format = (TextureFormat)formatValue;

                    int numValues = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;
                    texPack.colorMapDataSize = numValues;

                    // Extract and copy raw pixel byte data safely
                    if (numValues > 0)
                    {
                        texPack.colorMapData = new byte[numValues];
                        sourceSpan.Slice(dataIdx, numValues).CopyTo(texPack.colorMapData);
                        dataIdx += numValues;
                    }
                    else
                        texPack.colorMapData = Array.Empty<byte>(); // Avoid empty heap allocations

                    textureList.Add(texPack);
                }

                return textureList;
            }

            //!
            //! The function deserialises a character byte stream and stores it into the character list.
            //!
            private List<CharacterPackage> convertCharacterByteStream()
            {

                if (m_characterByteData == null || m_characterByteData.Length == 0)
                    return new List<CharacterPackage>();

                // Conservative capacity estimation to avoid continuous inner list resizing overhead
                // Using a baseline rough estimate of 2KB per character package (skeleton/bones data)
                int estimatedCapacity = m_characterByteData.Length / 2048;
                List<CharacterPackage> characterList = new List<CharacterPackage>(Math.Max(4, estimatedCapacity));

                // Create a global span over the source data for high-performance slicing
                ReadOnlySpan<byte> sourceSpan = m_characterByteData;
                int dataIdx = 0;

                // Iterate through the byte stream until the end is reached
                while (dataIdx < sourceSpan.Length)
                {
                    CharacterPackage characterPack = new CharacterPackage();

                    // Get bone mapping size safely via BinaryPrimitives (int)
                    characterPack.bMSize = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;

                    // Get skeleton mapping size safely via BinaryPrimitives (int)
                    characterPack.sSize = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;

                    // Get root dag path safely via BinaryPrimitives (int)
                    characterPack.characterRootId = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;

                    // Get bone mapping (int[])
                    if (characterPack.bMSize > 0)
                    {
                        characterPack.boneMapping = new int[characterPack.bMSize];
                        int byteLength = characterPack.bMSize * 4;
                        MemoryMarshal.Cast<byte, int>(sourceSpan.Slice(dataIdx, byteLength)).CopyTo(characterPack.boneMapping);
                        dataIdx += byteLength;
                    }
                    else
                        characterPack.boneMapping = Array.Empty<int>();

                    // Get skeleton mapping (int[])
                    if (characterPack.sSize > 0)
                    {
                        characterPack.skeletonMapping = new int[characterPack.sSize];
                        int byteLength = characterPack.sSize * 4;
                        MemoryMarshal.Cast<byte, int>(sourceSpan.Slice(dataIdx, byteLength)).CopyTo(characterPack.skeletonMapping);
                        dataIdx += byteLength;
                    }
                    else
                        characterPack.skeletonMapping = Array.Empty<int>();

                    // Get skeleton bone positions (float[])
                    int posElements = characterPack.sSize * 3;
                    if (posElements > 0)
                    {
                        characterPack.bonePosition = new float[posElements];
                        int byteLength = posElements * 4;
                        MemoryMarshal.Cast<byte, float>(sourceSpan.Slice(dataIdx, byteLength)).CopyTo(characterPack.bonePosition);
                        dataIdx += byteLength;
                    }
                    else
                        characterPack.bonePosition = Array.Empty<float>();

                    // Get skeleton bone rotations (float[])
                    int rotElements = characterPack.sSize * 4;
                    if (rotElements > 0)
                    {
                        characterPack.boneRotation = new float[rotElements];
                        int byteLength = rotElements * 4;
                        MemoryMarshal.Cast<byte, float>(sourceSpan.Slice(dataIdx, byteLength)).CopyTo(characterPack.boneRotation);
                        dataIdx += byteLength;
                    }
                    else
                        characterPack.boneRotation = Array.Empty<float>();

                    // Get skeleton bone scales (float[])
                    int scaleElements = characterPack.sSize * 3;
                    if (scaleElements > 0)
                    {
                        characterPack.boneScale = new float[scaleElements];
                        int byteLength = scaleElements * 4;
                        MemoryMarshal.Cast<byte, float>(sourceSpan.Slice(dataIdx, byteLength)).CopyTo(characterPack.boneScale);
                        dataIdx += byteLength;
                    }
                    else
                        characterPack.boneScale = Array.Empty<float>();

                    characterList.Add(characterPack);
                }

                return characterList;

            }

            //!
            //! The function deserialises a mesh byte stream and stores it into the object list.
            //!
            private List<ObjectPackage> convertObjectsByteStream()
            {
                if (m_objectsByteData == null || m_objectsByteData.Length == 0)
                    return new List<ObjectPackage>();

                // Conservative capacity estimation to prevent continuous inner list resizing overhead
                // Assuming an average baseline of roughly 32KB per object mesh package
                int estimatedCapacity = m_objectsByteData.Length / 32768;
                List<ObjectPackage> objectList = new List<ObjectPackage>(Math.Max(4, estimatedCapacity));

                // Create a global span over the source data for fast and safe slicing
                ReadOnlySpan<byte> sourceSpan = m_objectsByteData;
                int dataIdx = 0;

                // Use a safe boundary check matching the exact header size requirements
                while (dataIdx < sourceSpan.Length)
                {
                    ObjectPackage objPack = new ObjectPackage();

                    // Vertices (Forced Little Endian reading via BinaryPrimitives)
                    int numVertices = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;
                    objPack.vSize = numVertices;

                    int vertexElements = numVertices * 3;
                    if (vertexElements > 0)
                    {
                        objPack.vertices = new float[vertexElements];
                        int byteLength = vertexElements * 4;
                        MemoryMarshal.Cast<byte, float>(sourceSpan.Slice(dataIdx, byteLength)).CopyTo(objPack.vertices);
                        dataIdx += byteLength;
                    }
                    else
                        objPack.vertices = Array.Empty<float>();

                    // Indices
                    int numIndices = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;
                    objPack.iSize = numIndices;

                    if (numIndices > 0)
                    {
                        objPack.indices = new int[numIndices];
                        int byteLength = numIndices * 4;
                        MemoryMarshal.Cast<byte, int>(sourceSpan.Slice(dataIdx, byteLength)).CopyTo(objPack.indices);
                        dataIdx += byteLength;
                    }
                    else
                        objPack.indices = Array.Empty<int>();

                    // Normals
                    int numNormals = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;
                    objPack.nSize = numNormals;

                    int normalElements = numNormals * 3;
                    if (normalElements > 0)
                    {
                        objPack.normals = new float[normalElements];
                        int byteLength = normalElements * 4;
                        MemoryMarshal.Cast<byte, float>(sourceSpan.Slice(dataIdx, byteLength)).CopyTo(objPack.normals);
                        dataIdx += byteLength;
                    }
                    else
                        objPack.normals = Array.Empty<float>();

                    // Uvs
                    int numUvs = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;
                    objPack.uvSize = numUvs;

                    int uvElements = numUvs * 2;
                    if (uvElements > 0)
                    {
                        objPack.uvs = new float[uvElements];
                        int byteLength = uvElements * 4;
                        MemoryMarshal.Cast<byte, float>(sourceSpan.Slice(dataIdx, byteLength)).CopyTo(objPack.uvs);
                        dataIdx += byteLength;
                    }
                    else
                        objPack.uvs = Array.Empty<float>();

                    // Bone Weights
                    int numBoneWeights = BinaryPrimitives.ReadInt32LittleEndian(sourceSpan.Slice(dataIdx, 4));
                    dataIdx += 4;
                    objPack.bWSize = numBoneWeights;

                    int boneWeightElements = numBoneWeights * 4;
                    if (boneWeightElements > 0)
                    {
                        objPack.boneWeights = new float[boneWeightElements];
                        int byteLength = boneWeightElements * 4;
                        MemoryMarshal.Cast<byte, float>(sourceSpan.Slice(dataIdx, byteLength)).CopyTo(objPack.boneWeights);
                        dataIdx += byteLength;
                    }
                    else
                        objPack.boneWeights = Array.Empty<float>();

                    // Bone Indices
                    int boneIndexElements = objPack.bWSize * 4;
                    if (boneIndexElements > 0)
                    {
                        objPack.boneIndices = new int[boneIndexElements];
                        int byteLength = boneIndexElements * 4;
                        MemoryMarshal.Cast<byte, int>(sourceSpan.Slice(dataIdx, byteLength)).CopyTo(objPack.boneIndices);
                        dataIdx += byteLength;
                    }
                    else
                        objPack.boneIndices = Array.Empty<int>();

                    objectList.Add(objPack);
                }

                return objectList;
            }

            //!
            //! Function that concatinates all serialised TRACER nodes to a byte array.
            //!
            private void getNodesByteArray(List<SceneManager.SceneNode> nodeList)
            {
                if (nodeList == null || nodeList.Count == 0)
                {
                    m_nodesByteData = Array.Empty<byte>();
                    return;
                }

                // 1. Use a MemoryStream to write binary data fluidly and safely without pointer pinning
                using (MemoryStream stream = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    foreach (SceneManager.SceneNode node in nodeList)
                    {
                        if (node == null) continue;

                        NodeType nodeType;

                        // Use pattern matching to determine the legacy binary header type enum
                        if (node is SceneManager.SceneNodeSkinnedGeo) nodeType = NodeType.SKINNEDMESH;
                        else if (node is SceneManager.SceneNodeGeo) nodeType = NodeType.GEO;
                        else if (node is SceneManager.SceneNodeLight) nodeType = NodeType.LIGHT;
                        else if (node is SceneManager.SceneNodeCam) nodeType = NodeType.CAMERA;
                        else if (node is SceneManager.SceneNodeCharacter) nodeType = NodeType.CHARACTER;
                        else nodeType = NodeType.GROUP;

                        // Write the 4-byte NodeType header
                        writer.Write((int)nodeType);

                        // Serialize the class flatly and byte-perfect using the interface
                        if (node is IBinarySerializable serializable)
                            serializable.Serialize(writer);
                    }

                    // Extract the final optimized byte array exactly ONCE
                    m_nodesByteData = stream.ToArray();
                }
            }

            //!
            //! Function that concatinates all serialised TRACER meshes to a byte array.
            //!
            //! @param objectList The list that contains the serialised meshes to be concatinated.
            //!
            private void getObjectsByteArray(List<ObjectPackage> objectList)
            {
                if (objectList == null || objectList.Count == 0)
                {
                    m_objectsByteData = Array.Empty<byte>();
                    return;
                }

                // calculate size
                int totalSize = 0;
                foreach (var objPack in objectList)
                {
                    totalSize += 5 * sizeof(int) +
                                 objPack.vSize * 3 * sizeof(float) +
                                 objPack.iSize * sizeof(int) +
                                 objPack.nSize * 3 * sizeof(float) +
                                 objPack.uvSize * 2 * sizeof(float) +
                                 objPack.bWSize * 4 * sizeof(float) +
                                 objPack.bWSize * 4 * sizeof(int);
                }

                m_objectsByteData = new byte[totalSize];

                // span access to target array
                Span<byte> targetSpan = m_objectsByteData;
                int dstIdx = 0;

                foreach (ObjectPackage objPack in objectList)
                {
                    // --- VERTICES ---
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, sizeof(int)), objPack.vSize);
                    dstIdx += sizeof(int);

                    int vBytes = objPack.vSize * 3 * sizeof(float);
                    ReadOnlySpan<float> vSpan = objPack.vertices;
                    MemoryMarshal.AsBytes(vSpan).CopyTo(targetSpan.Slice(dstIdx, vBytes));
                    dstIdx += vBytes;

                    // --- INDICES ---
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, sizeof(int)), objPack.iSize);
                    dstIdx += sizeof(int);

                    int iBytes = objPack.iSize * sizeof(int);
                    ReadOnlySpan<int> iSpan = objPack.indices;
                    MemoryMarshal.AsBytes(iSpan).CopyTo(targetSpan.Slice(dstIdx, iBytes));
                    dstIdx += iBytes;

                    // --- NORMALS ---
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, sizeof(int)), objPack.nSize);
                    dstIdx += sizeof(int);

                    int nBytes = objPack.nSize * 3 * sizeof(float);
                    ReadOnlySpan<float> nSpan = objPack.normals;
                    MemoryMarshal.AsBytes(nSpan).CopyTo(targetSpan.Slice(dstIdx, nBytes));
                    dstIdx += nBytes;

                    // --- UVS ---
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, sizeof(int)), objPack.uvSize);
                    dstIdx += sizeof(int);

                    int uvBytes = objPack.uvSize * 2 * sizeof(float);
                    ReadOnlySpan<float> uvSpan = objPack.uvs;
                    MemoryMarshal.AsBytes(uvSpan).CopyTo(targetSpan.Slice(dstIdx, uvBytes));
                    dstIdx += uvBytes;

                    // --- BONE WEIGHTS ---
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, sizeof(int)), objPack.bWSize);
                    dstIdx += sizeof(int);

                    int bWBytes = objPack.bWSize * 4 * sizeof(float);
                    ReadOnlySpan<float> bWSpan = objPack.boneWeights;
                    MemoryMarshal.AsBytes(bWSpan).CopyTo(targetSpan.Slice(dstIdx, bWBytes));
                    dstIdx += bWBytes;

                    // --- BONE INDICES ---
                    int bIBytes = objPack.bWSize * 4 * sizeof(int);
                    ReadOnlySpan<int> bISpan = objPack.boneIndices;
                    MemoryMarshal.AsBytes(bISpan).CopyTo(targetSpan.Slice(dstIdx, bIBytes));
                    dstIdx += bIBytes;
                }
            }

            //!
            //! Function that concatinates all serialised TRACER skinned meshes to a byte array.
            //!
            //! @param characterList The list that contains the serialised skinned meshes to be concatinated.
            //!
            private void getCharacterByteArray(List<CharacterPackage> characterList)
            {
                if (characterList == null || characterList.Count == 0)
                {
                    m_characterByteData = Array.Empty<byte>();
                    return;
                }

                // Calculate the total required size upfront
                int totalSize = 0;
                int fixedHeaderSize = 3 * SceneDataHandler.size_int; // 3 ints for headers

                foreach (CharacterPackage chrPack in characterList)
                {
                    // Calculate sizes based on actual primitive arrays using MemoryMarshal
                    int boneMappingBytes = (chrPack.boneMapping?.Length ?? 0) * SceneDataHandler.size_int;
                    int skeletonMappingBytes = (chrPack.skeletonMapping?.Length ?? 0) * SceneDataHandler.size_int;
                    int bonePositionBytes = (chrPack.bonePosition?.Length ?? 0) * SceneDataHandler.size_float;
                    int boneRotationBytes = (chrPack.boneRotation?.Length ?? 0) * SceneDataHandler.size_float;
                    int boneScaleBytes = (chrPack.boneScale?.Length ?? 0) * SceneDataHandler.size_float;

                    totalSize += fixedHeaderSize +
                                 boneMappingBytes +
                                 skeletonMappingBytes +
                                 bonePositionBytes +
                                 boneRotationBytes +
                                 boneScaleBytes;
                }

                // Allocate the final array exactly ONCE
                m_characterByteData = new byte[totalSize];
                Span<byte> targetSpan = m_characterByteData;
                int dstIdx = 0;

                // Write data directly into the final array
                foreach (CharacterPackage chrPack in characterList)
                {
                    // bone mapping size (int)
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), chrPack.bMSize);
                    dstIdx += SceneDataHandler.size_int;

                    // skeleton mapping size (int)
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), chrPack.sSize);
                    dstIdx += SceneDataHandler.size_int;

                    // root dag _id (int)
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), chrPack.characterRootId);
                    dstIdx += SceneDataHandler.size_int;

                    // bone mapping (int[])
                    if (chrPack.boneMapping != null && chrPack.boneMapping.Length > 0)
                    {
                        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(chrPack.boneMapping.AsSpan());
                        bytes.CopyTo(targetSpan.Slice(dstIdx, bytes.Length));
                        dstIdx += bytes.Length;
                    }

                    // skeleton mapping (int[])
                    if (chrPack.skeletonMapping != null && chrPack.skeletonMapping.Length > 0)
                    {
                        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(chrPack.skeletonMapping.AsSpan());
                        bytes.CopyTo(targetSpan.Slice(dstIdx, bytes.Length));
                        dstIdx += bytes.Length;
                    }

                    // skeleton bone positions (float[])
                    if (chrPack.bonePosition != null && chrPack.bonePosition.Length > 0)
                    {
                        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(chrPack.bonePosition.AsSpan());
                        bytes.CopyTo(targetSpan.Slice(dstIdx, bytes.Length));
                        dstIdx += bytes.Length;
                    }

                    // skeleton bone rotations (float[])
                    if (chrPack.boneRotation != null && chrPack.boneRotation.Length > 0)
                    {
                        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(chrPack.boneRotation.AsSpan());
                        bytes.CopyTo(targetSpan.Slice(dstIdx, bytes.Length));
                        dstIdx += bytes.Length;
                    }

                    // skeleton bone scales (float[])
                    if (chrPack.boneScale != null && chrPack.boneScale.Length > 0)
                    {
                        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(chrPack.boneScale.AsSpan());
                        bytes.CopyTo(targetSpan.Slice(dstIdx, bytes.Length));
                        dstIdx += bytes.Length;
                    }
                }
            }

            //!
            //! Function that concatinates all TRACER Gaussuian Splat objects to a byte array.
            //!
            //! @param characterList The list that contains the serialised skinned meshes to be concatinated.
            //!
            private void getGaussianSplatByteArray(List<GaussianSpaltPackage> gaussianSplatList)
            {
                if (gaussianSplatList == null || gaussianSplatList.Count == 0)
                {
                    m_gaussianSplatByteData = Array.Empty<byte>();
                    return;
                }

                // calculate size
                int totalSize = 0;
                foreach (var gsPack in gaussianSplatList)
                {
                    totalSize += sizeof(int) * 11 +                  // all TryWriteBytes (11)
                                 sizeof(float) * 3 +                  // min
                                 sizeof(float) * 3 +                  // max
                                 sizeof(float) * 256 +                // scalesCodebook
                                 sizeof(float) * 256 +                // sh0Codebook
                                 sizeof(float) * 256 +                // shNcodebook
                                 gsPack.meansLSize +
                                 gsPack.meansUSize +
                                 gsPack.scalesSize +
                                 gsPack.quadsSize +
                                 gsPack.sh0Size +
                                 gsPack.shNcentroidsSize +
                                 gsPack.shNlablesSize;
                }

                m_gaussianSplatByteData = new byte[totalSize];

                Span<byte> targetSpan = m_gaussianSplatByteData;
                int dstIdx = 0;

                foreach (GaussianSpaltPackage gsPack in gaussianSplatList)
                {
                    // --- INTEGERS WRITE (TryWriteBytes) ---
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, sizeof(int)), gsPack.version);
                    dstIdx += sizeof(int);

                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, sizeof(int)), gsPack.count);
                    dstIdx += sizeof(int);

                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, sizeof(int)), gsPack.shNcount);
                    dstIdx += sizeof(int);

                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, sizeof(int)), gsPack.shNbands);
                    dstIdx += sizeof(int);

                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, sizeof(int)), gsPack.meansLSize);
                    dstIdx += sizeof(int);

                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, sizeof(int)), gsPack.meansUSize);
                    dstIdx += sizeof(int);

                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, sizeof(int)), gsPack.scalesSize);
                    dstIdx += sizeof(int);

                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, sizeof(int)), gsPack.quadsSize);
                    dstIdx += sizeof(int);

                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, sizeof(int)), gsPack.sh0Size);
                    dstIdx += sizeof(int);

                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, sizeof(int)), gsPack.shNcentroidsSize);
                    dstIdx += sizeof(int);

                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, sizeof(int)), gsPack.shNlablesSize);
                    dstIdx += sizeof(int);


                    // --- FLOATS COPY (MemoryMarshal) ---
                    // minimum
                    int minBytes = 3 * sizeof(float);
                    ReadOnlySpan<float> minSpan = gsPack.min;
                    MemoryMarshal.AsBytes(minSpan).CopyTo(targetSpan.Slice(dstIdx, minBytes));
                    dstIdx += minBytes;

                    // maximum
                    int maxBytes = 3 * sizeof(float);
                    ReadOnlySpan<float> maxSpan = gsPack.max;
                    MemoryMarshal.AsBytes(maxSpan).CopyTo(targetSpan.Slice(dstIdx, maxBytes));
                    dstIdx += maxBytes;

                    // scales codebook
                    int scBytes = 256 * sizeof(float);
                    ReadOnlySpan<float> scSpan = gsPack.scalesCodebook;
                    MemoryMarshal.AsBytes(scSpan).CopyTo(targetSpan.Slice(dstIdx, scBytes));
                    dstIdx += scBytes;

                    // sh0 codebook
                    int sh0Bytes = 256 * sizeof(float);
                    ReadOnlySpan<float> sh0Span = gsPack.sh0Codebook;
                    MemoryMarshal.AsBytes(sh0Span).CopyTo(targetSpan.Slice(dstIdx, sh0Bytes));
                    dstIdx += sh0Bytes;

                    // shN codebook
                    int shNBytes = 256 * sizeof(float);
                    ReadOnlySpan<float> shNSpan = gsPack.shNcodebook;
                    MemoryMarshal.AsBytes(shNSpan).CopyTo(targetSpan.Slice(dstIdx, shNBytes));
                    dstIdx += shNBytes;


                    // --- COMPRESSED BYTE DATA COPY ---
                    // compressed lower means
                    ReadOnlySpan<byte> meansLSpan = gsPack.c_meansL;
                    meansLSpan.CopyTo(targetSpan.Slice(dstIdx, gsPack.meansLSize));
                    dstIdx += gsPack.meansLSize;

                    // compressed upper means
                    ReadOnlySpan<byte> meansUSpan = gsPack.c_meansU;
                    meansUSpan.CopyTo(targetSpan.Slice(dstIdx, gsPack.meansUSize));
                    dstIdx += gsPack.meansUSize;

                    // compressed scales
                    ReadOnlySpan<byte> scalesSpan = gsPack.c_scales;
                    scalesSpan.CopyTo(targetSpan.Slice(dstIdx, gsPack.scalesSize));
                    dstIdx += gsPack.scalesSize;

                    // compressed quads
                    ReadOnlySpan<byte> quadsSpan = gsPack.c_quads;
                    quadsSpan.CopyTo(targetSpan.Slice(dstIdx, gsPack.quadsSize));
                    dstIdx += gsPack.quadsSize;

                    // compressed sh0s
                    ReadOnlySpan<byte> sh0sSpan = gsPack.c_sh0;
                    sh0sSpan.CopyTo(targetSpan.Slice(dstIdx, gsPack.sh0Size));
                    dstIdx += gsPack.sh0Size;

                    // compressed shn centroids
                    ReadOnlySpan<byte> centroidsSpan = gsPack.c_shNcentroids;
                    centroidsSpan.CopyTo(targetSpan.Slice(dstIdx, gsPack.shNcentroidsSize));
                    dstIdx += gsPack.shNcentroidsSize;

                    // compressed shN lables
                    ReadOnlySpan<byte> lablesSpan = gsPack.c_shNlables;
                    lablesSpan.CopyTo(targetSpan.Slice(dstIdx, gsPack.shNlablesSize));
                    dstIdx += gsPack.shNlablesSize;
                }
            }

            //!
            //! Function that concatinates all serialised TRACER textures to a byte array.
            //!
            //! @param textureList The list that contains the serialised textures to be concatinated.
            //!
            private void getTexturesByteArray(List<TexturePackage> textureList)
            {
                if (textureList == null || textureList.Count == 0)
                {
                    m_texturesByteData = Array.Empty<byte>();
                    return;
                }

                // Calculate the total required size upfront
                int totalSize = 0;
                int headerSize = 4 * SceneDataHandler.size_int; // 4 ints per texture

                foreach (TexturePackage texPack in textureList)
                    totalSize += headerSize + texPack.colorMapDataSize;

                // Allocate the final array exactly ONCE
                m_texturesByteData = new byte[totalSize];

                // Create a span for high-performance and safe memory access
                Span<byte> targetSpan = m_texturesByteData;
                int dstIdx = 0;

                // Write data directly into the final array
                foreach (TexturePackage texPack in textureList)
                {
                    // width
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), texPack.width);
                    dstIdx += SceneDataHandler.size_int;

                    // height
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), texPack.height);
                    dstIdx += SceneDataHandler.size_int;

                    // format
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), (int)texPack.format);
                    dstIdx += SceneDataHandler.size_int;

                    // data size
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), texPack.colorMapDataSize);
                    dstIdx += SceneDataHandler.size_int;

                    // pixel data (Direct copy without temporary arrays)
                    ReadOnlySpan<byte> pixelSpan = new ReadOnlySpan<byte>(texPack.colorMapData, 0, texPack.colorMapDataSize);
                    pixelSpan.CopyTo(targetSpan.Slice(dstIdx, texPack.colorMapDataSize));
                    dstIdx += texPack.colorMapDataSize;
                }
            }

            //!
            //! Function that concatinates all serialised TRACER materials to a byte array.
            //!
            //! @param materialList The list that contains the serialised materials to be concatinated.
            //!
            private void getMaterialsByteArray(List<MaterialPackage> materialList)
            {
                if (materialList == null || materialList.Count == 0)
                {
                    m_materialsByteData = Array.Empty<byte>();
                    return;
                }

                // Calculate the total required size upfront to avoid allocations in the loop
                int totalSize = 0;
                int fixedHeaderSize = 8 * SceneDataHandler.size_int;

                foreach (MaterialPackage matPack in materialList)
                {
                    // Cache safe lengths for encoded strings (ASCII length equals character count)
                    int nameLength = matPack.name?.Length ?? 0;
                    int srcLength = matPack.src?.Length ?? 0;

                    totalSize += fixedHeaderSize +
                                 nameLength +
                                 srcLength +
                                 (matPack.textureIds?.Length ?? 0) * SceneDataHandler.size_int +
                                 (matPack.textureOffsets?.Length ?? 0) * SceneDataHandler.size_float +
                                 (matPack.textureScales?.Length ?? 0) * SceneDataHandler.size_float +
                                 (matPack.shaderConfig?.Length ?? 0) + // bool is 1 byte
                                 (matPack.shaderPropertyIds?.Length ?? 0) * SceneDataHandler.size_int +
                                 (matPack.shaderPropertyTypes?.Length ?? 0) * SceneDataHandler.size_int +
                                 (matPack.shaderProperties?.Length ?? 0);
                }

                // Allocate the final array exactly ONCE
                m_materialsByteData = new byte[totalSize];
                Span<byte> targetSpan = m_materialsByteData;
                int dstIdx = 0;

                foreach (MaterialPackage matPack in materialList)
                {
                    int nameLength = matPack.name?.Length ?? 0;
                    int srcLength = matPack.src?.Length ?? 0;

                    // type (int)
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), matPack.type);
                    dstIdx += SceneDataHandler.size_int;

                    // name length (int)
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), nameLength);
                    dstIdx += SceneDataHandler.size_int;

                    // name (string converted directly to target span without temporary byte array)
                    if (nameLength > 0)
                    {
                        Encoding.ASCII.GetBytes(matPack.name, 0, nameLength, m_materialsByteData, dstIdx);
                        dstIdx += nameLength;
                    }

                    // src length (int)
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), srcLength);
                    dstIdx += SceneDataHandler.size_int;

                    // src (string converted directly to target span)
                    if (srcLength > 0)
                    {
                        Encoding.ASCII.GetBytes(matPack.src, 0, srcLength, m_materialsByteData, dstIdx);
                        dstIdx += srcLength;
                    }

                    // matID (int)
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), matPack.materialID);
                    dstIdx += SceneDataHandler.size_int;

                    // size (int) for textureIds
                    int texIdsLength = matPack.textureIds?.Length ?? 0;
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), texIdsLength);
                    dstIdx += SceneDataHandler.size_int;

                    // textureIds (int[])
                    if (texIdsLength > 0)
                    {
                        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(matPack.textureIds.AsSpan());
                        bytes.CopyTo(targetSpan.Slice(dstIdx, bytes.Length));
                        dstIdx += bytes.Length;
                    }

                    // textureOffsets (float[])
                    int texOffsetsLength = matPack.textureOffsets?.Length ?? 0;
                    if (texOffsetsLength > 0)
                    {
                        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(matPack.textureOffsets.AsSpan());
                        bytes.CopyTo(targetSpan.Slice(dstIdx, bytes.Length));
                        dstIdx += bytes.Length;
                    }

                    // textureScales (float[])
                    int texScalesLength = matPack.textureScales?.Length ?? 0;
                    if (texScalesLength > 0)
                    {
                        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(matPack.textureScales.AsSpan());
                        bytes.CopyTo(targetSpan.Slice(dstIdx, bytes.Length));
                        dstIdx += bytes.Length;
                    }

                    // size shaderConfig (int)
                    int shaderConfigLength = matPack.shaderConfig?.Length ?? 0;
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), shaderConfigLength);
                    dstIdx += SceneDataHandler.size_int;

                    // shaderConfig (bool[])
                    if (shaderConfigLength > 0)
                    {
                        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(matPack.shaderConfig.AsSpan());
                        bytes.CopyTo(targetSpan.Slice(dstIdx, bytes.Length));
                        dstIdx += bytes.Length;
                    }

                    // size shader properties (int)
                    int propertyIdsLength = matPack.shaderPropertyIds?.Length ?? 0;
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), propertyIdsLength);
                    dstIdx += SceneDataHandler.size_int;


                    // shader property IDs (int[])
                    if (propertyIdsLength > 0)
                    {
                        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(matPack.shaderPropertyIds.AsSpan());
                        bytes.CopyTo(targetSpan.Slice(dstIdx, bytes.Length));
                        dstIdx += bytes.Length;
                    }

                    // shader property types (int[])
                    int propertyTypesLength = matPack.shaderPropertyTypes?.Length ?? 0;
                    if (propertyTypesLength > 0)
                    {
                        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(matPack.shaderPropertyTypes.AsSpan());
                        bytes.CopyTo(targetSpan.Slice(dstIdx, bytes.Length));
                        dstIdx += bytes.Length;
                    }

                    // size shaderProperties data (int)
                    int propertiesLength = matPack.shaderProperties?.Length ?? 0;
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), propertiesLength);
                    dstIdx += SceneDataHandler.size_int;

                    // shader property data (byte[])
                    if (propertiesLength > 0)
                    {
                        ReadOnlySpan<byte> bytes = new ReadOnlySpan<byte>(matPack.shaderProperties, 0, propertiesLength);
                        bytes.CopyTo(targetSpan.Slice(dstIdx, propertiesLength));
                        dstIdx += propertiesLength;
                    }
                }
            }

            //!
            //! Function that concatinates all serialised TRACER ParameterObjects to a byte array.
            //!
            //! @param parameterObjectList The list that contains the serialised materials to be concatinated.
            //!
            private void getParameterObjectsByteArray(List<ParameterObjectPackage> parameterObjectList)
            {
                if (parameterObjectList == null || parameterObjectList.Count == 0)
                {
                    m_parameterObjectsByteData = Array.Empty<byte>();
                    return;
                }

                // Calculate the total required size upfront
                int totalSize = 0;
                int fixedHeaderSize = 3 * SceneDataHandler.size_int;

                foreach (ParameterObjectPackage poPack in parameterObjectList)
                {
                    int nameLength = poPack.name?.Length ?? 0;
                    int pTypesLength = poPack.pTypes?.Length ?? 0;
                    int pRPCLength = poPack.pRPC?.Length ?? 0; // bool is 1 byte per element
                    int pNamesCount = poPack.pNames?.Length ?? 0;

                    // Calculate size for all strings in the pNames array
                    int pNamesCharsLength = 0;
                    if (poPack.pNames != null)
                        foreach (string s in poPack.pNames)
                            pNamesCharsLength += (s?.Length ?? 0) + SceneDataHandler.size_int;

                    totalSize += fixedHeaderSize +
                                 nameLength +
                                 pTypesLength * SceneDataHandler.size_int +
                                 pRPCLength +
                                 pNamesCharsLength;
                }

                m_parameterObjectsByteData = new byte[totalSize];
                Span<byte> targetSpan = m_parameterObjectsByteData;
                int dstIdx = 0;

                // Write data directly into the final array
                foreach (ParameterObjectPackage poPack in parameterObjectList)
                {
                    int nameLength = poPack.name?.Length ?? 0;
                    int pTypesLength = poPack.pTypes?.Length ?? 0;
                    int pRPCLength = poPack.pRPC?.Length ?? 0;

                    // _id (int)
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), poPack.id);
                    dstIdx += SceneDataHandler.size_int;

                    // name length (int)
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), nameLength);
                    dstIdx += SceneDataHandler.size_int;

                    // name (string)
                    if (nameLength > 0)
                    {
                        Encoding.ASCII.GetBytes(poPack.name, 0, nameLength, m_parameterObjectsByteData, dstIdx);
                        dstIdx += nameLength;
                    }

                    // nbr parameters (int)
                    BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), pTypesLength);
                    dstIdx += SceneDataHandler.size_int;

                    // parameter types (int[])
                    if (pTypesLength > 0)
                    {
                        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(poPack.pTypes.AsSpan());
                        bytes.CopyTo(targetSpan.Slice(dstIdx, bytes.Length));
                        dstIdx += bytes.Length;
                    }

                    // parameter RPC properties (bool[])
                    if (pRPCLength > 0)
                    {
                        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(poPack.pRPC.AsSpan());
                        bytes.CopyTo(targetSpan.Slice(dstIdx, bytes.Length));
                        dstIdx += bytes.Length;
                    }

                    // parameter names (string[])
                    if (poPack.pNames != null)
                    {
                        foreach (string s in poPack.pNames)
                        {
                            int sLength = s?.Length ?? 0;

                            // Write the individual string length header
                            BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, SceneDataHandler.size_int), sLength);
                            dstIdx += SceneDataHandler.size_int;

                            // Write the string characters directly into the correct buffer
                            if (sLength > 0)
                            {
                                Encoding.ASCII.GetBytes(s, 0, sLength, m_parameterObjectsByteData, dstIdx);
                                dstIdx += sLength;
                            }
                        }
                    }
                }
            }
        
        }
    }
}
