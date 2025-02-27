﻿/*
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
                public List<SceneNode> nodeList;
                public List<ObjectPackage> objectList;
                public List<CharacterPackage> characterList;
                public List<TexturePackage> textureList;
                public List<MaterialPackage> materialList;
                public List<ParameterObjectPackage> parameterObjectList;

                public SceneData()
                {
                    header = new TracerHeader();
                    nodeList = new List<SceneNode>();
                    objectList = new List<ObjectPackage>();
                    characterList = new List<CharacterPackage>();
                    textureList = new List<TexturePackage>();
                    materialList = new List<MaterialPackage>();
                    parameterObjectList = new List<ParameterObjectPackage>();
                }

                ~SceneData() { clear(); }

                public void clear()
                {
                    nodeList.Clear();
                    objectList.Clear();
                    characterList.Clear();
                    textureList.Clear();
                    materialList.Clear();
                    parameterObjectList.Clear();
                }
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
            //! The list containing the serialised header.
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
                m_nodesByteData = new byte[0];
                m_objectsByteData = new byte[0];
                m_characterByteData = new byte[0];
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
            //! @param sceneData the scruct containing the TRACER scene data.
            //!
            public void setSceneData(ref SceneData sceneData)
            {
                // create byte arrays and clear buffers
                m_headerByteData = StructureToByteArray(sceneData.header);

                getNodesByteArray(ref sceneData.nodeList);
                sceneData.nodeList.Clear();

                getObjectsByteArray(ref sceneData.objectList);
                sceneData.objectList.Clear();

                getCharacterByteArray(ref sceneData.characterList);
                sceneData.characterList.Clear();

                getTexturesByteArray(ref sceneData.textureList);
                sceneData.textureList.Clear();

                getMaterialsByteArray(ref sceneData.materialList);
                sceneData.materialList.Clear();

                getParameterObjectsByteArray(ref sceneData.parameterObjectList);
                sceneData.parameterObjectList.Clear();
            }

            //!
            //! The function deserialises a header byte stream and stores it as scene defaults.
            //!
            private TracerHeader convertHeaderByteStream()
            {
                int offset = 0;
                return ByteArrayToStructure<TracerHeader>(ref m_headerByteData, ref offset);
            }

            //!
            //! The function deserialises a node byte stream and stores it into the sceneNode list.
            //!
            private List<SceneNode> convertNodesByteStream()
            {
                List<SceneNode> nodeList = new List<SceneNode>();

                int dataIdx = 0;
                while (dataIdx < m_nodesByteData.Length - 1)
                {
                    SceneNode node = new SceneNode();

                    NodeType nodeType = (NodeType)BitConverter.ToInt32(m_nodesByteData, dataIdx);
                    dataIdx += size_int;

                    switch (nodeType)
                    {
                        case NodeType.GROUP:
                            node = SceneDataHandler.ByteArrayToStructure<SceneNode>(ref m_nodesByteData, ref dataIdx);
                            break;
                        case NodeType.CHARACTER:
                            node = SceneDataHandler.ByteArrayToStructure<SceneNodeCharacter>(ref m_nodesByteData, ref dataIdx);
                            break;
                        case NodeType.GEO:
                            node = SceneDataHandler.ByteArrayToStructure<SceneNodeGeo>(ref m_nodesByteData, ref dataIdx);
                            break;
                        case NodeType.SKINNEDMESH:
                            node = SceneDataHandler.ByteArrayToStructure<SceneNodeSkinnedGeo>(ref m_nodesByteData, ref dataIdx);
                            break;
                        case NodeType.LIGHT:
                            node = SceneDataHandler.ByteArrayToStructure<SceneNodeLight>(ref m_nodesByteData, ref dataIdx);
                            break;
                        case NodeType.CAMERA:
                            node = SceneDataHandler.ByteArrayToStructure<SceneNodeCam>(ref m_nodesByteData, ref dataIdx);
                            break;
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

                int dataIdx = 0;
                while (dataIdx < m_materialsByteData.Length - 1)
                {

                    MaterialPackage matPack = new MaterialPackage();

                    // get type
                    int intValue = BitConverter.ToInt32(m_materialsByteData, dataIdx);
                    matPack.type = intValue;
                    dataIdx += size_int;

                    // get material name length
                    intValue = BitConverter.ToInt32(m_materialsByteData, dataIdx);
                    dataIdx += size_int;

                    // get material name
                    ReadOnlySpan<byte> nameByte = new ReadOnlySpan<byte>(m_materialsByteData, dataIdx, intValue);
                    matPack.name = Encoding.ASCII.GetString(nameByte);
                    dataIdx += intValue;

                    // get material src length
                    intValue = BitConverter.ToInt32(m_materialsByteData, dataIdx);
                    dataIdx += size_int;

                    // get material src
                    nameByte = new ReadOnlySpan<byte>(m_materialsByteData, dataIdx, intValue);
                    matPack.src = Encoding.ASCII.GetString(nameByte);
                    dataIdx += intValue;

                    // matID
                    matPack.materialID = BitConverter.ToInt32(m_materialsByteData, dataIdx);
                    dataIdx += size_int;

                    // size textureIds, textureNameIds, textureOffsets/2 (Vec2), textureScales/2 (Vec2)
                    intValue = BitConverter.ToInt32(m_materialsByteData, dataIdx);
                    dataIdx += size_int;

                    // textureIds
                    dataIdx = deserialize(m_materialsByteData, out matPack.textureIds, dataIdx, intValue);

                    // textureOffsets
                    dataIdx = deserialize(m_materialsByteData, out matPack.textureOffsets, dataIdx, intValue * 2);

                    // textureScales
                    dataIdx = deserialize(m_materialsByteData, out matPack.textureScales, dataIdx, intValue * 2);


                    if (matPack.type != 1)
                    {
                        // size shaderConfig
                        intValue = BitConverter.ToInt32(m_materialsByteData, dataIdx);
                        dataIdx += size_int;

                        // shaderConfig
                        dataIdx = deserialize(m_materialsByteData, out matPack.shaderConfig, dataIdx, intValue);

                        // size shaderProperties
                        intValue = BitConverter.ToInt32(m_materialsByteData, dataIdx);
                        dataIdx += size_int;

                        // shader property IDs
                        dataIdx = deserialize(m_materialsByteData, out matPack.shaderPropertyIds, dataIdx, intValue);

                        // shader property types
                        dataIdx = deserialize(m_materialsByteData, out matPack.shaderPropertyTypes, dataIdx, intValue);

                        // size shaderProperties data
                        intValue = BitConverter.ToInt32(m_materialsByteData, dataIdx);
                        dataIdx += size_int;

                        // shader property data
                        matPack.shaderProperties = new byte[intValue];
                        Buffer.BlockCopy(m_materialsByteData, dataIdx, matPack.shaderProperties, 0, intValue);
                        dataIdx += intValue;
                    }

                    materialList.Add(matPack);
                }
                return materialList;
            }

            //!
            //! Helper for deserialization of bool lists
            //!
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int deserialize(in byte[] srcData, out bool[] dstData, int srcOffset, int length)
            {
                dstData = new bool[length];
                for (int i = 0; i < length; i++)
                {
                    dstData[i] = BitConverter.ToBoolean(srcData, srcOffset);
                    srcOffset++;
                }
                return srcOffset;
            }

            //!
            //! Helper for deserialization of int lists
            //!
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int deserialize(in byte[] srcData, out int[] dstData, int srcOffset, int length)
            {
                dstData = new int[length];
                for (int i = 0; i < length; i++)
                {
                    dstData[i] = BitConverter.ToInt32(srcData, srcOffset);
                    srcOffset += size_int;
                }
                return srcOffset;
            }

            //!
            //! Helper for deserialization of float lists
            //!
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int deserialize(in byte[] srcData, out float[] dstData, int srcOffset, int length)
            {
                dstData = new float[length];
                for (int i = 0; i < length; i++)
                {
                    dstData[i] = BitConverter.ToSingle(srcData, srcOffset);
                    srcOffset += size_float;
                }
                return srcOffset;
            }

            //!
            //! Helper for deserialization of string lists
            //!
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int deserialize(in ReadOnlySpan<byte> srcData, out string[] dstData, int srcOffset, int length)
            {
                dstData = new string[length];
                for (int i = 0; i < length; i++)
                {
                    int jLength = MemoryMarshal.Read<int>(srcData.Slice(srcOffset));
                    srcOffset += size_int;

                    dstData[i] = Encoding.ASCII.GetString(srcData.Slice(srcOffset, jLength));
                    srcOffset += jLength;
                }
                return srcOffset;
            }

            //!
            //! The function deserialises a ParameterObjects byte stream and stores it into the ParameterObjects list.
            //!
            private List<ParameterObjectPackage> convertParameterObjectsByteStream()
            {
                List<ParameterObjectPackage> parameterObjectList = new List<ParameterObjectPackage>();

                int dataIdx = 0;
                while (dataIdx < m_parameterObjectsByteData.Length - 1)
                {
                    ParameterObjectPackage poPack = new ParameterObjectPackage();

                    // get _id
                    int intValue = BitConverter.ToInt32(m_parameterObjectsByteData, dataIdx);
                    poPack.id = intValue;
                    dataIdx += size_int;

                    // get parameter object name length
                    intValue = BitConverter.ToInt32(m_parameterObjectsByteData, dataIdx);
                    dataIdx += size_int;

                    // get parameter object name
                    ReadOnlySpan<byte> nameByte = new ReadOnlySpan<byte>(m_parameterObjectsByteData, dataIdx, intValue);
                    poPack.name = Encoding.ASCII.GetString(nameByte);
                    dataIdx += intValue;

                    // get nbr of parameters 
                    intValue = BitConverter.ToInt32(m_parameterObjectsByteData, dataIdx);
                    dataIdx += size_int;

                    // parameter types
                    dataIdx = deserialize(m_parameterObjectsByteData, out poPack.pTypes, dataIdx, intValue);
                   
                    // parameter is RPC
                    dataIdx = deserialize(m_parameterObjectsByteData, out poPack.pRPC, dataIdx, intValue);
                    
                    // parameter names
                    dataIdx = deserialize(m_parameterObjectsByteData, out poPack.pNames, dataIdx, intValue);

                    parameterObjectList.Add(poPack);
                }

                return parameterObjectList;
            }

            //!
            //! The function deserialises a texture byte stream and stores it into the texture list.
            //!
            //! @param sceneData The scene data for checking the tyture type.
            //!
            private List<TexturePackage> convertTexturesByteStream(ref SceneData sceneData)
            {
                List<TexturePackage> textureList = new List<TexturePackage>();
                int dataIdx = 0;

                while (dataIdx < m_texturesByteData.Length - 1)
                {
                    TexturePackage texPack = new TexturePackage();

                    int intValue = BitConverter.ToInt32(m_texturesByteData, dataIdx);
                    dataIdx += size_int;
                    texPack.width = intValue;
                    intValue = BitConverter.ToInt32(m_texturesByteData, dataIdx);
                    dataIdx += size_int;
                    texPack.height = intValue;
                    intValue = BitConverter.ToInt32(m_texturesByteData, dataIdx);
                    dataIdx += size_int;
                    texPack.format = (TextureFormat)intValue;

                    // pixel data
                    int numValues = BitConverter.ToInt32(m_texturesByteData, dataIdx);
                    dataIdx += size_int;
                    texPack.colorMapDataSize = numValues;
                    texPack.colorMapData = new byte[numValues];
                    Buffer.BlockCopy(m_texturesByteData, dataIdx, texPack.colorMapData, 0, numValues);
                    dataIdx += numValues;

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

                int dataIdx = 0;

                while (dataIdx < m_characterByteData.Length - 1)
                {
                    CharacterPackage characterPack = new CharacterPackage();

                    // get bone Mapping size
                    characterPack.bMSize = BitConverter.ToInt32(m_characterByteData, dataIdx);
                    dataIdx += size_int;

                    // get bone Mapping size
                    characterPack.sSize = BitConverter.ToInt32(m_characterByteData, dataIdx);
                    dataIdx += size_int;

                    // get root dag path
                    characterPack.characterRootId = BitConverter.ToInt32(m_characterByteData, dataIdx);
                    dataIdx += size_int;

                    // get bone mapping
                    characterPack.boneMapping = new int[characterPack.bMSize];
                    for (int i = 0; i < characterPack.bMSize; i++)
                    {
                        characterPack.boneMapping[i] = BitConverter.ToInt32(m_characterByteData, dataIdx);
                        dataIdx += size_int;
                    }

                    //get skeleton mapping
                    characterPack.skeletonMapping = new int[characterPack.sSize];
                    for (int i = 0; i < characterPack.sSize; i++)
                    {
                        characterPack.skeletonMapping[i] = BitConverter.ToInt32(m_characterByteData, dataIdx);
                        dataIdx += size_int;
                    }

                    //get skeleton bone postions
                    characterPack.bonePosition = new float[characterPack.sSize * 3];
                    for (int i = 0; i < characterPack.sSize; i++)
                    {
                        characterPack.bonePosition[i * 3] = BitConverter.ToSingle(m_characterByteData, dataIdx);
                        dataIdx += size_float;
                        characterPack.bonePosition[i * 3 + 1] = BitConverter.ToSingle(m_characterByteData, dataIdx);
                        dataIdx += size_float;
                        characterPack.bonePosition[i * 3 + 2] = BitConverter.ToSingle(m_characterByteData, dataIdx);
                        dataIdx += size_float;
                    }

                    //get skeleton bone rotations
                    characterPack.boneRotation = new float[characterPack.sSize * 4];
                    for (int i = 0; i < characterPack.sSize; i++)
                    {
                        characterPack.boneRotation[i * 4] = BitConverter.ToSingle(m_characterByteData, dataIdx);
                        dataIdx += size_float;
                        characterPack.boneRotation[i * 4 + 1] = BitConverter.ToSingle(m_characterByteData, dataIdx);
                        dataIdx += size_float;
                        characterPack.boneRotation[i * 4 + 2] = BitConverter.ToSingle(m_characterByteData, dataIdx);
                        dataIdx += size_float;
                        characterPack.boneRotation[i * 4 + 3] = BitConverter.ToSingle(m_characterByteData, dataIdx);
                        dataIdx += size_float;
                    }

                    //get skeleton bone scales
                    characterPack.boneScale = new float[characterPack.sSize * 3];
                    for (int i = 0; i < characterPack.sSize; i++)
                    {
                        characterPack.boneScale[i * 3] = BitConverter.ToSingle(m_characterByteData, dataIdx);
                        dataIdx += size_float;
                        characterPack.boneScale[i * 3 + 1] = BitConverter.ToSingle(m_characterByteData, dataIdx);
                        dataIdx += size_float;
                        characterPack.boneScale[i * 3 + 2] = BitConverter.ToSingle(m_characterByteData, dataIdx);
                        dataIdx += size_float;
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

                int dataIdx = 0;
                while (dataIdx < m_objectsByteData.Length - 1)
                {
                    ObjectPackage objPack = new ObjectPackage();

                    // get vertices
                    int numValues = BitConverter.ToInt32(m_objectsByteData, dataIdx);
                    dataIdx += size_int;
                    objPack.vSize = numValues;
                    objPack.vertices = new float[numValues * 3];
                    for (int i = 0; i < numValues * 3; i++)
                    {
                        objPack.vertices[i] = BitConverter.ToSingle(m_objectsByteData, dataIdx);
                        dataIdx += size_float;
                    }


                    // get indices
                    numValues = BitConverter.ToInt32(m_objectsByteData, dataIdx);
                    dataIdx += size_int;
                    objPack.iSize = numValues;
                    objPack.indices = new int[numValues];
                    for (int i = 0; i < numValues; i++)
                    {
                        objPack.indices[i] = BitConverter.ToInt32(m_objectsByteData, dataIdx);
                        dataIdx += size_int;
                    }

                    // get normals
                    numValues = BitConverter.ToInt32(m_objectsByteData, dataIdx);
                    dataIdx += size_int;
                    objPack.nSize = numValues;
                    objPack.normals = new float[numValues * 3];
                    for (int i = 0; i < numValues * 3; i++)
                    {
                        objPack.normals[i] = BitConverter.ToSingle(m_objectsByteData, dataIdx);
                        dataIdx += size_float;
                    }

                    // get uvs
                    numValues = BitConverter.ToInt32(m_objectsByteData, dataIdx);
                    dataIdx += size_int;
                    objPack.uvSize = numValues;
                    objPack.uvs = new float[numValues * 2];
                    for (int i = 0; i < numValues * 2; i++)
                    {
                        objPack.uvs[i] = BitConverter.ToSingle(m_objectsByteData, dataIdx);
                        dataIdx += size_float;
                    }

                    // get boneWeights
                    numValues = BitConverter.ToInt32(m_objectsByteData, dataIdx);
                    dataIdx += size_int;
                    objPack.bWSize = numValues;
                    objPack.boneWeights = new float[numValues * 4];
                    for (int i = 0; i < numValues * 4; i++)
                    {
                        objPack.boneWeights[i] = BitConverter.ToSingle(m_objectsByteData, dataIdx);
                        dataIdx += size_float;
                    }

                    // get boneIndices
                    objPack.boneIndices = new int[objPack.bWSize * 4];
                    for (int i = 0; i < objPack.bWSize * 4; i++)
                    {
                        objPack.boneIndices[i] = BitConverter.ToInt32(m_objectsByteData, dataIdx);
                        dataIdx += size_int;
                    }

                    objectList.Add(objPack);
                }
                return objectList;
            }

            //!
            //! Function that concatinates all serialised TRACER nodes to a byte array.
            //!
            private void getNodesByteArray(ref List<SceneManager.SceneNode> nodeList)
            {
                m_nodesByteData = new byte[0];
                foreach (SceneManager.SceneNode node in nodeList)
                {
                    byte[] nodeBinary;
                    byte[] nodeTypeBinary;
                    if (node.GetType() == typeof(SceneManager.SceneNodeGeo))
                    {
                        nodeTypeBinary = BitConverter.GetBytes((int)NodeType.GEO);
                        SceneManager.SceneNodeGeo nodeGeo = (SceneManager.SceneNodeGeo)Convert.ChangeType(node, typeof(SceneManager.SceneNodeGeo));
                        nodeBinary = StructureToByteArray(nodeGeo);
                    }
                    else if (node.GetType() == typeof(SceneManager.SceneNodeSkinnedGeo))
                    {
                        nodeTypeBinary = BitConverter.GetBytes((int)NodeType.SKINNEDMESH);
                        SceneManager.SceneNodeSkinnedGeo nodeskinnedGeo = (SceneManager.SceneNodeSkinnedGeo)Convert.ChangeType(node, typeof(SceneManager.SceneNodeSkinnedGeo));
                        nodeBinary = StructureToByteArray(nodeskinnedGeo);
                    }
                    else if (node.GetType() == typeof(SceneManager.SceneNodeLight))
                    {
                        nodeTypeBinary = BitConverter.GetBytes((int)NodeType.LIGHT);
                        SceneManager.SceneNodeLight nodeLight = (SceneManager.SceneNodeLight)Convert.ChangeType(node, typeof(SceneManager.SceneNodeLight));
                        nodeBinary = StructureToByteArray(nodeLight);
                    }
                    else if (node.GetType() == typeof(SceneManager.SceneNodeCam))
                    {
                        nodeTypeBinary = BitConverter.GetBytes((int)NodeType.CAMERA);
                        SceneManager.SceneNodeCam nodeCam = (SceneManager.SceneNodeCam)Convert.ChangeType(node, typeof(SceneManager.SceneNodeCam));
                        nodeBinary = StructureToByteArray(nodeCam);
                    }
                    else if (node.GetType() == typeof(SceneManager.SceneNodeCharacter))
                    {
                        nodeTypeBinary = BitConverter.GetBytes((int)NodeType.CHARACTER);
                        SceneManager.SceneNodeCharacter nodeChar = (SceneManager.SceneNodeCharacter)Convert.ChangeType(node, typeof(SceneManager.SceneNodeCharacter));
                        nodeBinary = StructureToByteArray(nodeChar);
                    }
                    else
                    {
                        nodeTypeBinary = BitConverter.GetBytes((int)NodeType.GROUP);
                        nodeBinary = StructureToByteArray(node);
                    }

                    // concate arrays
                    m_nodesByteData = Concat<byte>(m_nodesByteData, nodeTypeBinary);
                    m_nodesByteData = Concat<byte>(m_nodesByteData, nodeBinary);
                }
            }

            //!
            //! Function that concatinates all serialised TRACER meshes to a byte array.
            //!
            //! @param objectList The list that contains the serialised meshes to be concatinated.
            //!
            private void getObjectsByteArray(ref List<ObjectPackage> objectList)
            {
                m_objectsByteData = new byte[0];

                foreach (ObjectPackage objPack in objectList)
                {
                    byte[] objByteData = new byte[5 * SceneDataHandler.size_int +
                                                        objPack.vSize * 3 * SceneDataHandler.size_float +
                                                        objPack.iSize * SceneDataHandler.size_int +
                                                        objPack.nSize * 3 * SceneDataHandler.size_float +
                                                        objPack.uvSize * 2 * SceneDataHandler.size_float +
                                                        objPack.bWSize * 4 * SceneDataHandler.size_float +
                                                        objPack.bWSize * 4 * SceneDataHandler.size_int];
                    int dstIdx = 0;
                    // vertices
                    BitConverter.TryWriteBytes(new Span<byte>(objByteData, dstIdx, SceneDataHandler.size_int), objPack.vSize);
                    dstIdx += SceneDataHandler.size_int;
                    Buffer.BlockCopy(objPack.vertices, 0, objByteData, dstIdx, objPack.vSize * 3 * SceneDataHandler.size_float);
                    dstIdx += objPack.vSize * 3 * SceneDataHandler.size_float;
                    // indices
                    BitConverter.TryWriteBytes(new Span<byte>(objByteData, dstIdx, SceneDataHandler.size_int), objPack.iSize);
                    dstIdx += SceneDataHandler.size_int;
                    Buffer.BlockCopy(objPack.indices, 0, objByteData, dstIdx, objPack.iSize * SceneDataHandler.size_int);
                    dstIdx += objPack.iSize * SceneDataHandler.size_int;
                    // normals
                    BitConverter.TryWriteBytes(new Span<byte>(objByteData, dstIdx, SceneDataHandler.size_int), objPack.nSize);
                    dstIdx += SceneDataHandler.size_int;
                    Buffer.BlockCopy(objPack.normals, 0, objByteData, dstIdx, objPack.nSize * 3 * SceneDataHandler.size_float);
                    dstIdx += objPack.nSize * 3 * SceneDataHandler.size_float;
                    // uvs
                    BitConverter.TryWriteBytes(new Span<byte>(objByteData, dstIdx, SceneDataHandler.size_int), objPack.uvSize);
                    dstIdx += SceneDataHandler.size_int;
                    Buffer.BlockCopy(objPack.uvs, 0, objByteData, dstIdx, objPack.uvSize * 2 * SceneDataHandler.size_float);
                    dstIdx += objPack.uvSize * 2 * SceneDataHandler.size_float;
                    // bone weights
                    BitConverter.TryWriteBytes(new Span<byte>(objByteData, dstIdx, SceneDataHandler.size_int), objPack.bWSize);
                    dstIdx += SceneDataHandler.size_int;
                    Buffer.BlockCopy(objPack.boneWeights, 0, objByteData, dstIdx, objPack.bWSize * 4 * SceneDataHandler.size_float);
                    dstIdx += objPack.bWSize * 4 * SceneDataHandler.size_float;
                    // bone indices
                    Buffer.BlockCopy(objPack.boneIndices, 0, objByteData, dstIdx, objPack.bWSize * 4 * SceneDataHandler.size_int);
                    dstIdx += objPack.bWSize * 4 * SceneDataHandler.size_int;

                    // concate
                    m_objectsByteData = Concat<byte>(m_objectsByteData, objByteData);
                }
            }

            //!
            //! Function that concatinates all serialised TRACER skinned meshes to a byte array.
            //!
            //! @param characterList The list that contains the serialised skinned meshes to be concatinated.
            //!
            private void getCharacterByteArray(ref List<CharacterPackage> characterList)
            {
                m_characterByteData = new byte[0];
                foreach (CharacterPackage chrPack in characterList)
                {
                    byte[] characterByteData = new byte[SceneDataHandler.size_int * 3 +
                                                    chrPack.boneMapping.Length * SceneDataHandler.size_int +
                                                    chrPack.skeletonMapping.Length * SceneDataHandler.size_int +
                                                    chrPack.sSize * SceneDataHandler.size_float * 10];
                    int dstIdx = 0;
                    // bone mapping size
                    BitConverter.TryWriteBytes(new Span<byte>(characterByteData, dstIdx, SceneDataHandler.size_int), chrPack.bMSize);
                    dstIdx += SceneDataHandler.size_int;

                    // skeleton mapping size
                    BitConverter.TryWriteBytes(new Span<byte>(characterByteData, dstIdx, SceneDataHandler.size_int), chrPack.sSize);
                    dstIdx += SceneDataHandler.size_int;

                    // root dag _id
                    BitConverter.TryWriteBytes(new Span<byte>(characterByteData, dstIdx, SceneDataHandler.size_int), chrPack.characterRootId);
                    dstIdx += SceneDataHandler.size_int;

                    // bone mapping
                    Buffer.BlockCopy(chrPack.boneMapping, 0, characterByteData, dstIdx, chrPack.bMSize * SceneDataHandler.size_int);
                    dstIdx += chrPack.bMSize * SceneDataHandler.size_int;

                    // skeleton mapping
                    Buffer.BlockCopy(chrPack.skeletonMapping, 0, characterByteData, dstIdx, chrPack.sSize * SceneDataHandler.size_int);
                    dstIdx += chrPack.sSize * SceneDataHandler.size_int;

                    //skelton bone positions
                    Buffer.BlockCopy(chrPack.bonePosition, 0, characterByteData, dstIdx, chrPack.sSize * 3 * SceneDataHandler.size_float);
                    dstIdx += chrPack.sSize * 3 * SceneDataHandler.size_float;

                    //skelton bone rotations
                    Buffer.BlockCopy(chrPack.boneRotation, 0, characterByteData, dstIdx, chrPack.sSize * 4 * SceneDataHandler.size_float);
                    dstIdx += chrPack.sSize * 4 * SceneDataHandler.size_float;

                    //skelton bone scales
                    Buffer.BlockCopy(chrPack.boneScale, 0, characterByteData, dstIdx, chrPack.sSize * 3 * SceneDataHandler.size_float);
                    dstIdx += chrPack.sSize * 3 * SceneDataHandler.size_float;

                    // concatenating character byte data to serialised character array
                    m_characterByteData = Concat<byte>(m_characterByteData, characterByteData);
                }
            }

            //!
            //! Function that concatinates all serialised TRACER textures to a byte array.
            //!
            //! @param textureList The list that contains the serialised textures to be concatinated.
            //!
            private void getTexturesByteArray(ref List<TexturePackage> textureList)
            {
                m_texturesByteData = new byte[0];

                foreach (TexturePackage texPack in textureList)
                {
                    byte[] texByteData = new byte[4 * SceneDataHandler.size_int + texPack.colorMapDataSize];
                    int dstIdx = 0;
                    // width
                    BitConverter.TryWriteBytes(new Span<byte>(texByteData, dstIdx, SceneDataHandler.size_int), texPack.width);
                    dstIdx += SceneDataHandler.size_int;
                    // height
                    BitConverter.TryWriteBytes(new Span<byte>(texByteData, dstIdx, SceneDataHandler.size_int), texPack.height);
                    dstIdx += SceneDataHandler.size_int;
                    // format
                    BitConverter.TryWriteBytes(new Span<byte>(texByteData, dstIdx, SceneDataHandler.size_int), (int)texPack.format);
                    dstIdx += SceneDataHandler.size_int;
                    // data size
                    BitConverter.TryWriteBytes(new Span<byte>(texByteData, dstIdx, SceneDataHandler.size_int), (int)texPack.colorMapDataSize);
                    dstIdx += SceneDataHandler.size_int;
                    // pixel data
                    Buffer.BlockCopy(texPack.colorMapData, 0, texByteData, dstIdx, texPack.colorMapDataSize);
                    dstIdx += texPack.colorMapDataSize;

                    // concate
                    m_texturesByteData = Concat<byte>(m_texturesByteData, texByteData);
                }
            }

            //!
            //! Function that concatinates all serialised TRACER materials to a byte array.
            //!
            //! @param materialList The list that contains the serialised materials to be concatinated.
            //!
            private void getMaterialsByteArray(ref List<MaterialPackage> materialList)
            {
                m_materialsByteData = new byte[0];

                foreach (MaterialPackage matPack in materialList)
                {
                    byte[] matByteData = new byte[
                        8 * SceneDataHandler.size_int +
                        matPack.name.Length +
                        matPack.src.Length +
                        matPack.textureIds.Length * SceneDataHandler.size_int +
                        matPack.textureOffsets.Length * SceneDataHandler.size_float +
                        matPack.textureScales.Length * SceneDataHandler.size_float +
                        matPack.shaderConfig.Length +
                        matPack.shaderPropertyIds.Length * SceneDataHandler.size_int +
                        matPack.shaderPropertyTypes.Length * SceneDataHandler.size_int +
                        matPack.shaderProperties.Length];

                    int dstIdx = 0;

                    // type (int)
                    dstIdx = serialize<byte>(BitConverter.GetBytes(matPack.type), ref matByteData, dstIdx);

                    // name length (int)
                    dstIdx = serialize<byte>(BitConverter.GetBytes(matPack.name.Length), ref matByteData, dstIdx);

                    // name (byte[])
                    dstIdx = serialize<byte>(Encoding.ASCII.GetBytes(matPack.name), ref matByteData, dstIdx);

                    // src length (int)
                    dstIdx = serialize<byte>(BitConverter.GetBytes(matPack.src.Length), ref matByteData, dstIdx);

                    // src (string)
                    dstIdx = serialize<byte>(Encoding.ASCII.GetBytes(matPack.src), ref matByteData, dstIdx);

                    // matID (int)
                    dstIdx = serialize<byte>(BitConverter.GetBytes(matPack.materialID), ref matByteData, dstIdx);

                    // size (int) for textureIds, textureOffsets/2 (Vec2), textureScales/2 (Vec2)
                    dstIdx = serialize<byte>(BitConverter.GetBytes(matPack.textureIds.Length), ref matByteData, dstIdx);

                    // textureIds (int[])
                    dstIdx = serialize<int>(matPack.textureIds, ref matByteData, dstIdx);

                    // textureOffsets (float[])
                    dstIdx = serialize<float>(matPack.textureOffsets, ref matByteData, dstIdx);

                    // textureScales (float[])
                    dstIdx = serialize<float>(matPack.textureScales, ref matByteData, dstIdx);

                    // size shaderConfig (int)
                    dstIdx = serialize<byte>(BitConverter.GetBytes(matPack.shaderConfig.Length), ref matByteData, dstIdx);

                    // shaderConfig (bool[])
                    dstIdx = serialize<bool>(matPack.shaderConfig, ref matByteData, dstIdx);

                    // size shader properties (int)
                    dstIdx = serialize<byte>(BitConverter.GetBytes(matPack.shaderPropertyIds.Length), ref matByteData, dstIdx);

                    // shader property IDs (int[])
                    dstIdx = serialize<int>(matPack.shaderPropertyIds, ref matByteData, dstIdx);

                    // shader property types (int[])
                    dstIdx = serialize<int>(matPack.shaderPropertyTypes, ref matByteData, dstIdx);

                    // size shaderProperties data (int)
                    dstIdx = serialize<byte>(BitConverter.GetBytes(matPack.shaderProperties.Length), ref matByteData, dstIdx);

                    // shader property data (byte[])
                    dstIdx = serialize<byte>(matPack.shaderProperties, ref matByteData, dstIdx);


                    // concate
                    m_materialsByteData = Concat<byte>(m_materialsByteData, matByteData);
                }
            }

            //!
            //! Function that concatinates all serialised TRACER ParameterObjects to a byte array.
            //!
            //! @param parameterObjectList The list that contains the serialised materials to be concatinated.
            //!
            private void getParameterObjectsByteArray(ref List<ParameterObjectPackage> parameterObjectList)
            {
                m_parameterObjectsByteData = new byte[0];


                foreach (ParameterObjectPackage poPack in parameterObjectList)
                {
                    // count characters in srings
                    int charLength = 0;
                    foreach (string s in poPack.pNames)
                        charLength += s.Length;

                    byte[] poByteData = new byte[
                        3 * SceneDataHandler.size_int +
                        poPack.name.Length +
                        poPack.pTypes.Length * SceneDataHandler.size_int +
                        poPack.pRPC.Length * SceneDataHandler.size_float +
                        poPack.pNames.Length * SceneDataHandler.size_int +
                        charLength];
                    
                    int dstIdx = 0;
                    
                    // _id (int)
                    dstIdx = serialize<byte>(BitConverter.GetBytes(poPack.id), ref poByteData, dstIdx);

                    // name length (int)
                    dstIdx = serialize<byte>(BitConverter.GetBytes(poPack.name.Length), ref poByteData, dstIdx);

                    // name (byte[])
                    dstIdx = serialize<byte>(Encoding.ASCII.GetBytes(poPack.name), ref poByteData, dstIdx);

                    // nbr parameters (int)
                    dstIdx = serialize<byte>(BitConverter.GetBytes(poPack.pTypes.Length), ref poByteData, dstIdx);

                    // prameter types (int[])
                    dstIdx = serialize<int>(poPack.pTypes, ref poByteData, dstIdx);

                    // prameter RPC properties (bool[])
                    dstIdx = serialize<bool>(poPack.pRPC, ref poByteData, dstIdx);

                    // prameter names (string[])
                    dstIdx = serialize<string>(poPack.pNames, ref poByteData, dstIdx);

                    // concate
                    m_parameterObjectsByteData = Concat<byte>(m_parameterObjectsByteData, poByteData);
                }
            }

            //!
            //! Function that serializes an arbitrary formated array into a byte array.
            //!
            //! @param srcData The arbitrary source data array.
            //! @param dstData The byte formated destination array.
            //! @param dstIdx The the destination intex within the destination array.
            //! @return The new destination index after copying the source data.
            //!
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int serialize<T>(T[] srcData, ref byte[] dstData, int dstIdx)
            {
                int typeSize = Marshal.SizeOf(typeof(T));
                if (typeof(T) == typeof(bool))
                    typeSize = 1;
                Buffer.BlockCopy(srcData, 0, dstData, dstIdx, srcData.Length * typeSize);
                return dstIdx + srcData.Length * typeSize;
            }

            //!
            //! Function that serializes an array of strings into a byte array.
            //!
            //! @param srcData The source string array.
            //! @param dstData The byte formated destination array.
            //! @param dstIdx The the destination intex within the destination array.
            //! @return The new destination index after copying the source data.
            //!
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int serialize(string[] srcData, ref byte[] dstData, int dstIdx)
            {
                for (int i = 0; i < srcData.Length; i++)
                {
                    int sLength = srcData.Length;

                    Buffer.BlockCopy(BitConverter.GetBytes(sLength), 0, dstData, dstIdx, size_int);
                    dstIdx += size_int;
                    Buffer.BlockCopy(Encoding.ASCII.GetBytes(srcData[i]), 0, dstData, dstIdx, sLength);
                    dstIdx += sLength;
                }

                return dstIdx;
            }

            //! 
            //! Template function for deserialising byte streams into arbitrary structures. 
            //! 
            //! @param bytearray The byte stream to be deserialised.
            //! @param offset The offset in bytes used to interate over the array.
            //! 
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static T ByteArrayToStructure<T>(ref byte[] bytearray, ref int offset) where T : new()
            {
                T str = new T();

                int size = Marshal.SizeOf(str);
                IntPtr ptr = Marshal.AllocHGlobal(size);

                Marshal.Copy(bytearray, offset, ptr, size);

                str = (T)Marshal.PtrToStructure(ptr, str.GetType());
                Marshal.FreeHGlobal(ptr);

                offset += size;

                return str;
            }

            //!
            //! Template function for concatination of arrays. 
            //!
            //! @param first The array field to be appended to.
            //! @param arrays The arrays to be append.
            //!
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static T[] Concat<T>(T[] first, params T[][] arrays)
            {
                int length = first.Length;
                for (int i = 0; i < arrays.Length; i++)
                {
                    length += arrays[i].Length;
                }
                T[] result = new T[length];

                length = first.Length;
                Buffer.BlockCopy(first, 0, result, 0, first.Length);
                for (int i = 0; i < arrays.Length; i++)
                {
                    var array = arrays[i];
                    Buffer.BlockCopy(array, 0, result, length, array.Length);
                    length += array.Length;
                }
                return result;
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

            //! 
            //! Template function for serialising arbitrary structures in to byte streams.
            //! 
            //! @param obj The object to be serialised.
            //!
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte[] StructureToByteArray(object obj)
            {
                int size = Marshal.SizeOf(obj);
                byte[] arr = new byte[size];
                IntPtr ptr = Marshal.AllocHGlobal(size);

                Marshal.StructureToPtr(obj, ptr, true);
                Marshal.Copy(ptr, arr, 0, size);
                Marshal.FreeHGlobal(ptr);

                return arr;
            }
        }
    }
}
