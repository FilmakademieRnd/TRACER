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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

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
                int headerSize = Marshal.SizeOf(sceneData.header);
                m_headerByteData = new byte[headerSize];

                IntPtr ptr = Marshal.AllocHGlobal(headerSize);
                try
                {
                    // Struktur in den unverwalteten Speicher kopieren
                    Marshal.StructureToPtr(sceneData.header, ptr, true);

                    // Daten DIREKT in das bestehende m_headerByteData kopieren (0 Byte GC-Müll!)
                    Marshal.Copy(ptr, m_headerByteData, 0, headerSize);
                }
                finally
                {
                    // Speicher auf jeden Fall wieder freigeben (Verhindert Memory Leaks)
                    Marshal.FreeHGlobal(ptr);
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
                int offset = 0;
                return ByteArrayToStructure<TracerHeader>(m_headerByteData, ref offset);
            }

            //!
            //! The function deserialises a node byte stream and stores it into the sceneNode list.
            //!
            private List<SceneNode> convertNodesByteStream()
            {
                List<SceneManager.SceneNode> nodeList = new List<SceneManager.SceneNode>();

                if (m_nodesByteData == null || m_nodesByteData.Length == 0)
                {
                    return nodeList;
                }

                int dataIdx = 0;
                int nodeCounter = 0; // Zähler hinzufügen

                // Iterate through the byte stream until the end is reached
                while (dataIdx < m_nodesByteData.Length)
                {
                    int currentHeaderIdx = dataIdx;
                    // Read the node type header (int)
                    NodeType nodeType = (NodeType)BitConverter.ToInt32(m_nodesByteData, dataIdx);
                    dataIdx += size_int;

                    Debug.Log($"[Parser] Node #{nodeCounter} an Byte-Offset {currentHeaderIdx}: Typ = {nodeType}");

                    SceneManager.SceneNode node;
                    nodeCounter++;

                    // Parse the exact structure based on the node type and upcast it to SceneNode
                    switch (nodeType)
                    {
                        case NodeType.GROUP:
                            node = SceneDataHandler.ByteArrayToStructure<SceneManager.SceneNode>(m_nodesByteData, ref dataIdx);
                            break;

                        case NodeType.CHARACTER:
                            node = SceneDataHandler.ByteArrayToStructure<SceneManager.SceneNodeCharacter>(m_nodesByteData, ref dataIdx);
                            break;

                        case NodeType.GEO:
                            node = SceneDataHandler.ByteArrayToStructure<SceneManager.SceneNodeGeo>(m_nodesByteData, ref dataIdx);
                            break;

                        case NodeType.SKINNEDMESH:
                            node = SceneDataHandler.ByteArrayToStructure<SceneManager.SceneNodeSkinnedGeo>(m_nodesByteData, ref dataIdx);
                            break;

                        case NodeType.LIGHT:
                            node = SceneDataHandler.ByteArrayToStructure<SceneManager.SceneNodeLight>(m_nodesByteData, ref dataIdx);
                            break;

                        case NodeType.CAMERA:
                            node = SceneDataHandler.ByteArrayToStructure<SceneManager.SceneNodeCam>(m_nodesByteData, ref dataIdx);
                            break;

                        default:
                            // Fallback for safety to prevent infinite loops on corrupt data
                            throw new InvalidOperationException($"Unknown node type encountered during deserialization: {nodeType}");
                    }

                    nodeList.Add(node);
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
                {
                    return parameterObjectList;
                }

                // Create a global span over the source data for fast and safe slicing
                ReadOnlySpan<byte> sourceSpan = m_parameterObjectsByteData;
                int dataIdx = 0;

                // Iterate through the byte stream until the end is reached
                while (dataIdx < sourceSpan.Length)
                {
                    ParameterObjectPackage poPack = new ParameterObjectPackage();

                    // 1. Get _id (int)
                    poPack.id = BitConverter.ToInt32(m_parameterObjectsByteData, dataIdx);
                    dataIdx += size_int;

                    // 2. Get parameter object name length (int)
                    int nameLength = BitConverter.ToInt32(m_parameterObjectsByteData, dataIdx);
                    dataIdx += size_int;

                    // 3. Get parameter object name (string)
                    if (nameLength > 0)
                    {
                        ReadOnlySpan<byte> nameSpan = sourceSpan.Slice(dataIdx, nameLength);
                        poPack.name = Encoding.ASCII.GetString(nameSpan);
                        dataIdx += nameLength;
                    }
                    else
                    {
                        poPack.name = string.Empty;
                    }

                    // 4. Get number of parameters (int)
                    int numParameters = BitConverter.ToInt32(m_parameterObjectsByteData, dataIdx);
                    dataIdx += size_int;

                    // 5. Get parameter types (int[])
                    poPack.pTypes = new int[numParameters];
                    if (numParameters > 0)
                    {
                        int byteLength = numParameters * size_int;
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                        MemoryMarshal.Cast<byte, int>(byteSlice).CopyTo(poPack.pTypes);
                        dataIdx += byteLength;
                    }

                    // 6. Get parameter RPC properties (bool[])
                    poPack.pRPC = new bool[numParameters];
                    if (numParameters > 0)
                    {
                        // bool is 1 byte per element in this structure
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, numParameters);
                        MemoryMarshal.Cast<byte, bool>(byteSlice).CopyTo(poPack.pRPC);
                        dataIdx += numParameters;
                    }

                    // 7. Get parameter names (string[])
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
                        {
                            poPack.pNames[i] = string.Empty;
                        }
                    }

                    parameterObjectList.Add(poPack);
                }

                return parameterObjectList;
            }

            private List<GaussianSpaltPackage> convertGaussianSplatByteStream(ref SceneData sceneData)
            {
                if (m_gaussianSplatByteData == null || m_gaussianSplatByteData.Length == 0)
                {
                    return new List<GaussianSpaltPackage>();
                }

                // Konservative Schätzung der Kapazität, um internes Listen-Resizing zu vermeiden
                int estimatedCapacity = m_gaussianSplatByteData.Length / 4096; // 4KB als grober Richtwert pro Paket
                List<GaussianSpaltPackage> gaussianSplatList = new List<GaussianSpaltPackage>(Math.Max(4, estimatedCapacity));

                ReadOnlySpan<byte> sourceSpan = m_gaussianSplatByteData;
                int dataIdx = 0;

                while (dataIdx < sourceSpan.Length)
                {
                    GaussianSpaltPackage gsPack = new GaussianSpaltPackage();

                    // 1. Die 10 Integer-Werte via BinaryPrimitives auslesen
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
                List<TexturePackage> textureList = new List<TexturePackage>();

                if (m_texturesByteData == null || m_texturesByteData.Length == 0)
                {
                    return textureList;
                }

                // Create a global span over the source data for fast and safe slicing
                ReadOnlySpan<byte> sourceSpan = m_texturesByteData;
                int dataIdx = 0;

                // Iterate through the byte stream until the end is reached
                while (dataIdx < sourceSpan.Length)
                {
                    TexturePackage texPack = new TexturePackage();

                    // Get width (int)
                    texPack.width = BitConverter.ToInt32(m_texturesByteData, dataIdx);
                    dataIdx += size_int;

                    // Get height (int)
                    texPack.height = BitConverter.ToInt32(m_texturesByteData, dataIdx);
                    dataIdx += size_int;

                    // Get format (TextureFormat enum from int)
                    int formatValue = BitConverter.ToInt32(m_texturesByteData, dataIdx);
                    dataIdx += size_int;
                    texPack.format = (TextureFormat)formatValue;

                    // Get pixel data size (int)
                    int numValues = BitConverter.ToInt32(m_texturesByteData, dataIdx);
                    dataIdx += size_int;
                    texPack.colorMapDataSize = numValues;

                    // Get pixel data (byte[])
                    texPack.colorMapData = new byte[numValues];
                    if (numValues > 0)
                    {
                        ReadOnlySpan<byte> pixelSlice = sourceSpan.Slice(dataIdx, numValues);
                        pixelSlice.CopyTo(texPack.colorMapData);
                        dataIdx += numValues;
                    }

                    textureList.Add(texPack);
                }

                return textureList;
            }

            //!
            //! The function deserialises a character byte stream and stores it into the character list.
            //!
            private List<CharacterPackage> convertCharacterByteStream()
            {
                List<CharacterPackage> characterList = new List<CharacterPackage>();

                if (m_characterByteData == null || m_characterByteData.Length == 0)
                {
                    return characterList;
                }

                // Create a global span over the source data for high-performance slicing
                ReadOnlySpan<byte> sourceSpan = m_characterByteData;
                int dataIdx = 0;

                // Iterate through the byte stream until the end is reached
                while (dataIdx < sourceSpan.Length)
                {
                    CharacterPackage characterPack = new CharacterPackage();

                    // 1. Get bone mapping size (int)
                    characterPack.bMSize = BitConverter.ToInt32(m_characterByteData, dataIdx);
                    dataIdx += size_int;

                    // 2. Get skeleton mapping size (int)
                    characterPack.sSize = BitConverter.ToInt32(m_characterByteData, dataIdx);
                    dataIdx += size_int;

                    // 3. Get root dag path (int)
                    characterPack.characterRootId = BitConverter.ToInt32(m_characterByteData, dataIdx);
                    dataIdx += size_int;

                    // 4. Get bone mapping (int[])
                    characterPack.boneMapping = new int[characterPack.bMSize];
                    if (characterPack.bMSize > 0)
                    {
                        int byteLength = characterPack.bMSize * size_int;
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                        MemoryMarshal.Cast<byte, int>(byteSlice).CopyTo(characterPack.boneMapping);
                        dataIdx += byteLength;
                    }

                    // 5. Get skeleton mapping (int[])
                    characterPack.skeletonMapping = new int[characterPack.sSize];
                    if (characterPack.sSize > 0)
                    {
                        int byteLength = characterPack.sSize * size_int;
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                        MemoryMarshal.Cast<byte, int>(byteSlice).CopyTo(characterPack.skeletonMapping);
                        dataIdx += byteLength;
                    }

                    // 6. Get skeleton bone positions (float[])
                    int posElements = characterPack.sSize * 3;
                    characterPack.bonePosition = new float[posElements];
                    if (posElements > 0)
                    {
                        int byteLength = posElements * size_float;
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                        MemoryMarshal.Cast<byte, float>(byteSlice).CopyTo(characterPack.bonePosition);
                        dataIdx += byteLength;
                    }

                    // 7. Get skeleton bone rotations (float[])
                    int rotElements = characterPack.sSize * 4;
                    characterPack.boneRotation = new float[rotElements];
                    if (rotElements > 0)
                    {
                        int byteLength = rotElements * size_float;
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                        MemoryMarshal.Cast<byte, float>(byteSlice).CopyTo(characterPack.boneRotation);
                        dataIdx += byteLength;
                    }

                    // 8. Get skeleton bone scales (float[])
                    int scaleElements = characterPack.sSize * 3;
                    characterPack.boneScale = new float[scaleElements];
                    if (scaleElements > 0)
                    {
                        int byteLength = scaleElements * size_float;
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                        MemoryMarshal.Cast<byte, float>(byteSlice).CopyTo(characterPack.boneScale);
                        dataIdx += byteLength;
                    }

                    characterList.Add(characterPack);
                }

                return characterList;
            }

            //!
            //! The function deserialises a mesh byte stream and stores it into the object list.
            //!
            private List<ObjectPackage> convertObjectsByteStream()
            {
                List<ObjectPackage> objectList = new List<ObjectPackage>();

                if (m_objectsByteData == null || m_objectsByteData.Length == 0)
                {
                    return objectList;
                }

                // Create a global span over the source data for fast slicing
                ReadOnlySpan<byte> sourceSpan = m_objectsByteData;
                int dataIdx = 0;

                // Use a safe boundary check matching the exact header size requirements
                while (dataIdx < sourceSpan.Length)
                {
                    ObjectPackage objPack = new ObjectPackage();

                    // 1. Get Vertices
                    int numVertices = BitConverter.ToInt32(m_objectsByteData, dataIdx);
                    dataIdx += size_int;
                    objPack.vSize = numVertices;

                    int vertexElements = numVertices * 3;
                    objPack.vertices = new float[vertexElements];
                    if (vertexElements > 0)
                    {
                        int byteLength = vertexElements * size_float;
                        // Slice the required bytes and cast them directly into the target float array
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                        MemoryMarshal.Cast<byte, float>(byteSlice).CopyTo(objPack.vertices);
                        dataIdx += byteLength;
                    }

                    // 2. Get Indices
                    int numIndices = BitConverter.ToInt32(m_objectsByteData, dataIdx);
                    dataIdx += size_int;
                    objPack.iSize = numIndices;

                    objPack.indices = new int[numIndices];
                    if (numIndices > 0)
                    {
                        int byteLength = numIndices * size_int;
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                        MemoryMarshal.Cast<byte, int>(byteSlice).CopyTo(objPack.indices);
                        dataIdx += byteLength;
                    }

                    // 3. Get Normals
                    int numNormals = BitConverter.ToInt32(m_objectsByteData, dataIdx);
                    dataIdx += size_int;
                    objPack.nSize = numNormals;

                    int normalElements = numNormals * 3;
                    objPack.normals = new float[normalElements];
                    if (normalElements > 0)
                    {
                        int byteLength = normalElements * size_float;
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                        MemoryMarshal.Cast<byte, float>(byteSlice).CopyTo(objPack.normals);
                        dataIdx += byteLength;
                    }

                    // 4. Get Uvs
                    int numUvs = BitConverter.ToInt32(m_objectsByteData, dataIdx);
                    dataIdx += size_int;
                    objPack.uvSize = numUvs;

                    int uvElements = numUvs * 2;
                    objPack.uvs = new float[uvElements];
                    if (uvElements > 0)
                    {
                        int byteLength = uvElements * size_float;
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                        MemoryMarshal.Cast<byte, float>(byteSlice).CopyTo(objPack.uvs);
                        dataIdx += byteLength;
                    }

                    // 5. Get Bone Weights
                    int numBoneWeights = BitConverter.ToInt32(m_objectsByteData, dataIdx);
                    dataIdx += size_int;
                    objPack.bWSize = numBoneWeights;

                    int boneWeightElements = numBoneWeights * 4;
                    objPack.boneWeights = new float[boneWeightElements];
                    if (boneWeightElements > 0)
                    {
                        int byteLength = boneWeightElements * size_float;
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                        MemoryMarshal.Cast<byte, float>(byteSlice).CopyTo(objPack.boneWeights);
                        dataIdx += byteLength;
                    }

                    // 6. Get Bone Indices
                    int boneIndexElements = objPack.bWSize * 4;
                    objPack.boneIndices = new int[boneIndexElements];
                    if (boneIndexElements > 0)
                    {
                        int byteLength = boneIndexElements * size_int;
                        ReadOnlySpan<byte> byteSlice = sourceSpan.Slice(dataIdx, byteLength);
                        MemoryMarshal.Cast<byte, int>(byteSlice).CopyTo(objPack.boneIndices);
                        dataIdx += byteLength;
                    }

                    objectList.Add(objPack);
                }

                return objectList;
            }

            //!
            //!
            //!
            private List<GaussianSpaltPackage> convertGaussianSplatByteStream()
            {
                List<GaussianSpaltPackage> gaussianSplatList = new List<GaussianSpaltPackage>();

                int dataIdx = 0;
                while (dataIdx < m_gaussianSplatByteData.Length - 1)
                {
                    GaussianSpaltPackage gsPack = new GaussianSpaltPackage();

                    // tbc... [SEIM]
                }

                return gaussianSplatList;
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

                // Cache the sizes of the structures upfront to avoid calling Marshal.SizeOf inside loops
                int typeHeaderSize = SceneDataHandler.size_int;
                int sizeNode = Marshal.SizeOf<SceneManager.SceneNode>();
                int sizeGeo = Marshal.SizeOf<SceneManager.SceneNodeGeo>();
                int sizeSkinned = Marshal.SizeOf<SceneManager.SceneNodeSkinnedGeo>();
                int sizeLight = Marshal.SizeOf<SceneManager.SceneNodeLight>();
                int sizeCam = Marshal.SizeOf<SceneManager.SceneNodeCam>();
                int sizeChar = Marshal.SizeOf<SceneManager.SceneNodeCharacter>();

                // 1. Calculate the total required size upfront
                int totalSize = 0;
                foreach (SceneManager.SceneNode node in nodeList)
                {
                    totalSize += typeHeaderSize; // Every node writes an int for NodeType

                    if (node is SceneManager.SceneNodeGeo) totalSize += sizeGeo;
                    else if (node is SceneManager.SceneNodeSkinnedGeo) totalSize += sizeSkinned;
                    else if (node is SceneManager.SceneNodeLight) totalSize += sizeLight;
                    else if (node is SceneManager.SceneNodeCam) totalSize += sizeCam;
                    else if (node is SceneManager.SceneNodeCharacter) totalSize += sizeChar;
                    else totalSize += sizeNode;
                }

                // 2. Allocate the final array exactly ONCE
                m_nodesByteData = new byte[totalSize];
                Span<byte> targetSpan = m_nodesByteData;
                int dstIdx = 0;

                // Pin the managed target array momentarily to get safe native pointers for all operations
                GCHandle handle = GCHandle.Alloc(m_nodesByteData, GCHandleType.Pinned);

                try
                {
                    IntPtr basePtr = handle.AddrOfPinnedObject();

                    // 3. Write data directly into the final array
                    foreach (SceneManager.SceneNode node in nodeList)
                    {
                        NodeType nodeType;
                        int structureSize;
                        object targetStructure;

                        // Use high-performance Pattern Matching instead of GetType() and Convert.ChangeType
                        if (node is SceneManager.SceneNodeGeo nodeGeo)
                        {
                            nodeType = NodeType.GEO;
                            structureSize = sizeGeo;
                            targetStructure = nodeGeo;
                        }
                        else if (node is SceneManager.SceneNodeSkinnedGeo nodeSkinnedGeo)
                        {
                            nodeType = NodeType.SKINNEDMESH;
                            structureSize = sizeSkinned;
                            targetStructure = nodeSkinnedGeo;
                        }
                        else if (node is SceneManager.SceneNodeLight nodeLight)
                        {
                            nodeType = NodeType.LIGHT;
                            structureSize = sizeLight;
                            targetStructure = nodeLight;
                        }
                        else if (node is SceneManager.SceneNodeCam nodeCam)
                        {
                            nodeType = NodeType.CAMERA;
                            structureSize = sizeCam;
                            targetStructure = nodeCam;
                        }
                        else if (node is SceneManager.SceneNodeCharacter nodeChar)
                        {
                            nodeType = NodeType.CHARACTER;
                            structureSize = sizeChar;
                            targetStructure = nodeChar;
                        }
                        else
                        {
                            nodeType = NodeType.GROUP;
                            structureSize = sizeNode;
                            targetStructure = node;
                        }

                        // Write node type (int)
                        BitConverter.TryWriteBytes(targetSpan.Slice(dstIdx, typeHeaderSize), (int)nodeType);
                        dstIdx += typeHeaderSize;

                        // Marshal the structure directly into the correct position within the final array
                        IntPtr dstPtr = basePtr + dstIdx;
                        Marshal.StructureToPtr(targetStructure, dstPtr, false);
                        dstIdx += structureSize;
                    }
                }
                finally
                {
                    // Always release the handle to unpin the array and avoid memory leaks
                    handle.Free();
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

                // 1. Die exakte Gesamtgröße ALLER Objekte im Vorfeld berechnen
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

                // 2. Nur EINMAL Speicher für alle Daten allokieren (0% GC-Druck)
                m_objectsByteData = new byte[totalSize];

                // 3. Einen Span auf das Ziel-Array legen für ultraschnellen Speicherzugriff
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

                // 1. Calculate the total required size upfront
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

                // 2. Allocate the final array exactly ONCE
                m_characterByteData = new byte[totalSize];
                Span<byte> targetSpan = m_characterByteData;
                int dstIdx = 0;

                // 3. Write data directly into the final array
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

                // 1. Exakte Gesamtgröße ALLER Gaussian-Splat-Pakete im Vorfeld berechnen
                int totalSize = 0;
                foreach (var gsPack in gaussianSplatList)
                {
                    totalSize += sizeof(int) * 10 +                  // Alle TryWriteBytes (10 Stück)
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
                {
                    totalSize += headerSize + texPack.colorMapDataSize;
                }

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

            //! 
            //! Template function for deserialising byte streams into arbitrary structures. 
            //! 
            //! @param bytearray The byte stream to be deserialised.
            //! @param offset The offset in bytes used to interate over the array.
            //! 
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static T ByteArrayToStructure<T>(byte[] bytearray, ref int offset) 
            {
                int size = Marshal.SizeOf(typeof(T));

                IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.Copy(bytearray, offset, ptr, size);

                    T str = (T)Marshal.PtrToStructure(ptr, typeof(T));

                    offset += size;
                    return str;
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }

            //!
            //! Template function for concatination of arrays. 
            //!
            //! @param first The array field to be appended to.
            //! @param arrays The arrays to be append.
            //!
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static T[] Concat<T>(T[] first, params T[] array)
            {
                T[] result = new T[first.Length + array.Length];

                Buffer.BlockCopy(first, 0, result, 0, first.Length);
                Buffer.BlockCopy(array, 0, result, first.Length, array.Length);

                return result;
            }
        
        }
    }
}
