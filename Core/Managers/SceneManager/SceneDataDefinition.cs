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

//! @file "SceneDataDefinition.cs"
//! @brief definition of TRACER scene data structure
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 11.03.2022

using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace tracer
{
    public partial class SceneManager : Manager
    {
        public static string[] shaderKeywords = { "_NORMALMAP", "_ALPHATEST_ON", "_ALPHABLEND_ON", "_ALPHAPREMULTIPLY_ON", "_EMISSION", "_PARALLAXMAP", "_DETAIL_MULX2", "_METALLICGLOSSMAP", "_SPECGLOSSMAP" };

        //!
        //! Enumeration defining TRACER node Types.
        //!
        public enum NodeType { GROUP, GEO, LIGHT, CAMERA, SKINNEDMESH, CHARACTER }

        //!
        //! Enumeration defining TRACER light Types.
        //!
        public enum LightType { SPOT, DIRECTIONAL, POINT, AREA, DISC, SUN }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct Float3
        {
            public float x, y, z;

            // Praktischer Indexer, damit dein restlicher Code float[index] weiterhin nutzen kann!
            public float this[int index]
            {
                get => index == 0 ? x : index == 1 ? y : z;
                set { if (index == 0) x = value; else if (index == 1) y = value; else z = value; }
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct Float4
        {
            public float x, y, z, w;

            public float this[int index]
            {
                get => index == 0 ? x : index == 1 ? y : index == 2 ? z : w;
                set { if (index == 0) x = value; else if (index == 1) y = value; else if (index == 2) z = value; else w = value; }
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct Byte64
        {
            // Erstellt exakt 64 Bytes sequentiellen Speicher
            private fixed byte data[64];

            public byte this[int index]
            {
                get { unsafe { fixed (byte* p = data) return p[index]; } }
                set { unsafe { fixed (byte* p = data) p[index] = value; } }
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct Float1584
        {
            // Reserviert exakt 1584 * 4 Bytes im Speicher
            private unsafe fixed float data[1584];

            public float this[int index]
            {
                get { unsafe { fixed (float* p = data) return p[index]; } }
                set { unsafe { fixed (float* p = data) p[index] = value; } }
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct Int99
        {
            // Reserviert exakt 99 * 4 Bytes im Speicher
            private unsafe fixed int data[99];

            public int this[int index]
            {
                get { unsafe { fixed (int* p = data) return p[index]; } }
                set { unsafe { fixed (int* p = data) p[index] = value; } }
            }
        }
        
        public interface IBinarySerializable
        {
            int FileStructureSize { get; }
            void Populate(BinaryReader reader);
            void Serialize(BinaryWriter writer);
        }

        //!
        //! Data structure for serialising basic SceneNodes.
        //! The struct layout and array size for marshaling has to be fixed
        //! to be compatible with unmanaged code.
        //!
        [System.Serializable]
        public class SceneNode : IBinarySerializable
        {
            //! Flag that determines whether a node is editable or not. 
            public bool editable;
            //! The Number of childes the node have. 
            public int childCount;
            //! The position of the node in world space, stored as float array with the length of 3.
            public Float3 position;
            //! The scale of the node in world space, stored as float array with the length of 3.
            public Float3 scale;
            //! The rotation of the node in world space, stored as float array with the length of 4.
            public Float4 rotation;
            //! The name of the node, stored as byte array with the length of 64.
            public Byte64 name;

            // Basisgröße: bool(4) + childCount(4) + pos(12) + scale(12) + rot(16) + name(64) = 112
            public virtual int FileStructureSize => 112;

            public virtual void Populate(BinaryReader reader)
            {
                editable = reader.ReadInt32() != 0; // Mono-4-Byte-Bool
                childCount = reader.ReadInt32();

                position.x = reader.ReadSingle(); position.y = reader.ReadSingle(); position.z = reader.ReadSingle();
                scale.x = reader.ReadSingle(); scale.y = reader.ReadSingle(); scale.z = reader.ReadSingle();

                rotation.x = reader.ReadSingle(); rotation.y = reader.ReadSingle();
                rotation.z = reader.ReadSingle(); rotation.w = reader.ReadSingle();

                byte[] nameBytes = reader.ReadBytes(64);
                for (int i = 0; i < 64; i++) name[i] = nameBytes[i]; // Über Indexer
            }

            public virtual void Serialize(BinaryWriter writer)
            {
                writer.Write(editable ? 1 : 0); // Legacy Mono 4-byte boolean
                writer.Write(childCount);

                writer.Write(position.x); writer.Write(position.y); writer.Write(position.z);
                writer.Write(scale.x); writer.Write(scale.y); writer.Write(scale.z);

                writer.Write(rotation.x); writer.Write(rotation.y);
                writer.Write(rotation.z); writer.Write(rotation.w);

                for (int i = 0; i < 64; i++) writer.Write(name[i]);
            }
        };

        //!
        //! Data structure for serialising basic character SceneNodes.
        //! The struct layout and array size for marshaling has to be fixed
        //! to be compatible with unmanaged code.
        //!
        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Auto)]
        public class SceneNodeCharacter : SceneNode
        {
        }

        //!
        //! Data structure for serialising SceneNodes representing Unity
        //! game objects containing 3d scene components like meshes and materials.
        //!
        [System.Serializable]
        public class SceneNodeGeo : SceneNode
        {
            public int geoId;
            public int materialId;
            public Float4 color;

            // Basis(112) + geoId(4) + matId(4) + color(16) = 136
            public override int FileStructureSize => 136;

            public override void Populate(BinaryReader reader)
            {
                base.Populate(reader); // Liest die ersten 112 Bytes
                geoId = reader.ReadInt32();
                materialId = reader.ReadInt32();

                color.x = reader.ReadSingle(); color.y = reader.ReadSingle();
                color.z = reader.ReadSingle(); color.w = reader.ReadSingle();
            }

            public override void Serialize(BinaryWriter writer)
            {
                base.Serialize(writer); // Writes the 112 bytes base
                writer.Write(geoId);
                writer.Write(materialId);

                writer.Write(color.x); writer.Write(color.y);
                writer.Write(color.z); writer.Write(color.w);
            }
        }

        //!
        //! Data structure for serialising SceneNodes representing Unity
        //! game objects containing skinned mesh components.
        //!
        [System.Serializable]
        public class SceneNodeSkinnedGeo : SceneNodeGeo
        {
            //! The length of the array storing the bind poses.
            public int bindPoseLength;
            //! The ID for referencing the associated character root.
            public int characterRootID;
            //! The bounds of the skinned mesh in world space.
            public Float3 boundExtents;
            //! The center of the skinned mesh in world space.
            public Float3 boundCenter;
            //! The bind poses of the skinned mesh stored as 99 4x4 matrices.
            public Float1584 bindPoses;
            //! The bone IDs for referencing the associated skeleton bones.
            public Int99 skinnedMeshBoneIDs;

            // Berechnung: Basis Geo(136) + bindPoseLength(4) + characterRootID(4) 
            // + boundExtents(12) + boundCenter(12) + bindPoses(1584 * 4 = 6336) 
            // + skinnedMeshBoneIDs(99 * 4 = 396) = 136 + 6740 = 6876 Bytes
            public override int FileStructureSize => 6876;

            public override void Populate(BinaryReader reader)
            {
                base.Populate(reader);
                bindPoseLength = reader.ReadInt32();
                characterRootID = reader.ReadInt32();

                boundExtents.x = reader.ReadSingle();
                boundExtents.y = reader.ReadSingle();
                boundExtents.z = reader.ReadSingle();

                boundCenter.x = reader.ReadSingle();
                boundCenter.y = reader.ReadSingle();
                boundCenter.z = reader.ReadSingle();

                for (int i = 0; i < 1584; i++)
                    bindPoses[i] = reader.ReadSingle();

                for (int i = 0; i < 99; i++)
                    skinnedMeshBoneIDs[i] = reader.ReadInt32();
            }

            public override void Serialize(BinaryWriter writer)
            {
                base.Serialize(writer);

                writer.Write(bindPoseLength);
                writer.Write(characterRootID);

                writer.Write(boundExtents.x);
                writer.Write(boundExtents.y);
                writer.Write(boundExtents.z);

                writer.Write(boundCenter.x);
                writer.Write(boundCenter.y);
                writer.Write(boundCenter.z);

                for (int i = 0; i < 1584; i++)
                    writer.Write(bindPoses[i]);

                // 5. Write bone IDs (Fixed loop for 99 ints)
                for (int i = 0; i < 99; i++)
                    writer.Write(skinnedMeshBoneIDs[i]);
            }
        }

        //!
        //!
        //!
        [System.Serializable]
        public class SceneNodeGS : SceneNode
        {
            //! The ID for referencing the associated geometry data.
            public int gsId;
            public int shDegree;
            public int sortMode;

            // Berechnung: Basis SceneNode(112) + gsId(4) + shDegree(4) + sortMode(4) = 124 Bytes
            public override int FileStructureSize => 124;

            public override void Populate(BinaryReader reader)
            {
                base.Populate(reader);

                gsId = reader.ReadInt32();
                shDegree = reader.ReadInt32();
                sortMode = reader.ReadInt32();
            }

            public override void Serialize(BinaryWriter writer)
            {
                base.Serialize(writer);

                writer.Write(gsId);
                writer.Write(shDegree);
                writer.Write(sortMode);
            }
        }

        //!
        //! Data structure for serialising SceneNodes representing Unity
        //! game objects containing light components.
        //!
        [System.Serializable]
        public class SceneNodeLight : SceneNode
        {
            public LightType lightType;
            public float intensity;
            public float angle;
            public float range;
            public Float3 color;

            // Basis(112) + lightType(4) + intensity(4) + angle(4) + range(4) + RGB-Farbkanäle(12) = 140
            public override int FileStructureSize => 140;

            public override void Populate(BinaryReader reader)
            {
                base.Populate(reader);
                lightType = (LightType)reader.ReadInt32();
                intensity = reader.ReadSingle();
                angle = reader.ReadSingle();
                range = reader.ReadSingle();

                // Die gelöste RGB-Falle: Nur 3 Floats aus der Datei lesen!
                color.x = reader.ReadSingle();
                color.y = reader.ReadSingle();
                color.z = reader.ReadSingle();
            }

            public override void Serialize(BinaryWriter writer)
            {
                base.Serialize(writer); // Writes the 112 bytes base
                writer.Write((int)lightType);
                writer.Write(intensity);
                writer.Write(angle);
                writer.Write(range);

                // The solved RGB trap: Write ONLY 3 floats to file for legacy desktop compatibility!
                writer.Write(color.x); // R
                writer.Write(color.y); // G
                writer.Write(color.z); // B
            }
        }

        //!
        //! Data structure for serialising SceneNodes representing Unity
        //! game objects containing camera components.
        //!
        [System.Serializable]
        public class SceneNodeCam : SceneNode
        {
            //! The field of view of the camera. 
            public float fov;
            //! The aspect ratio of the camera. 
            public float aspect;
            //! The near clipping plane of the camera.
            public float near;
            //! The far clipping plane of the camera.
            public float far;
            //! The focal distance of the camera. 
            public float focalDist;
            //! The aperture of the camera. 
            public float aperture;

            // Berechnung: Basis SceneNode(112) + 6 * float(24) = 136 Bytes
            public override int FileStructureSize => 136;

            public override void Populate(BinaryReader reader)
            {
                // 1. Zuerst die 112 Bytes der Basisklasse einlesen
                base.Populate(reader);

                // 2. Die spezifischen Kamera-Daten nahtlos dahinter auslesen
                fov = reader.ReadSingle();
                aspect = reader.ReadSingle();
                near = reader.ReadSingle();
                far = reader.ReadSingle();
                focalDist = reader.ReadSingle();
                aperture = reader.ReadSingle();
            }

            public override void Serialize(BinaryWriter writer)
            {
                // 1. Write the 112 bytes from the base class first
                base.Serialize(writer);

                // 2. Write the specific camera data seamlessly right after
                writer.Write(fov);
                writer.Write(aspect);
                writer.Write(near);
                writer.Write(far);
                writer.Write(focalDist);
                writer.Write(aperture);
            }
        }

        //!
        //! Data structure for serialising the TRACER header,
        //! containing scene relevant informations.
        //!
        [System.Serializable]
        public class TracerHeader : IBinarySerializable
        {
            //! Global factor for light intensity scaling. 
            public float lightIntensityFactor;
            //! The client ID from the scene sender, used as SceneObject's sceneID.
            public byte senderID;
            //! The current framerate in fps.
            public byte frameRate;

            // File layout size: float(4) + byte(1) + byte(1) + legacy 4-byte-alignment padding(2) = 8 bytes
            public int FileStructureSize => 8;

            public void Populate(BinaryReader reader)
            {
                lightIntensityFactor = reader.ReadSingle();
                senderID = reader.ReadByte();
                frameRate = reader.ReadByte();

                // Skip the 2 padding bytes at the end of the header to keep the data stream synchronized
                reader.ReadBytes(2);
            }
            public void Serialize(BinaryWriter writer)
            {
                writer.Write(lightIntensityFactor);
                writer.Write(senderID);
                writer.Write(frameRate);
                writer.Write(new byte[2]); // 2 empty bytes for compatibility with old format!
            }
        }

        //!
        //! TRACER structure used to hold mesh data.
        //! This sructure is used for saving to disk or sending via network.
        //!
        public class ObjectPackage
        {
            //! The number of vertices in one object package.
            public int vSize;
            //! The number of indices in one object package.
            public int iSize;
            //! The number of normals in one object package.
            public int nSize;
            //! The number of UVs in one object package.
            public int uvSize;
            //! The number of blend weights in one object package.
            public int bWSize;
            //! The additionally stored Unity mesh.
            public Mesh mesh;
            //! The array of floats storing the vertex positions as x,y,z.
            public float[] vertices;
            //! The array of ints storing the indices.
            public int[] indices;
            //! The array of floats storing the normals as x,y,z.
            public float[] normals;
            //! The array of floats storing the UVs as x,y.
            public float[] uvs;
            //! The array of floats storing the bone weights as x,y,z,w per vertex.
            public float[] boneWeights;
            //! The array of ints storing the bone indices.
            public int[] boneIndices;
        };

        //!
        //! TRACER structure used to hold additional skinned mesh data.
        //! This sructure is used for saving to disk or sending via network.
        //!
        public class CharacterPackage
        {
            //! The size of the bone mapping array.
            public int bMSize;
            //! The size of the skeleton array.
            public int sSize;
            //! The object ID of the root of the character in the scenegraph.
            public int characterRootId;
            //! The array of IDs for referencing the associated bone objects.
            public int[] boneMapping;
            //! The array of IDs for referencing the sceleton objects.
            public int[] skeletonMapping;
            //! The array of bone positions for this character as vec3[].
            public float[] bonePosition;
            //! The array of bone rotations for this character as vec4[].
            public float[] boneRotation;
            //! The array of bone scales for this character as vec3[].
            public float[] boneScale;
        };

        //!
        //! GS
        //!
        public class GaussianSpaltPackage
        {
            public int version;
            public int count;
            public int shNcount;
            public int shNbands;
            public int meansLSize;
            public int meansUSize;
            public int scalesSize;
            public int quadsSize;
            public int sh0Size;
            public int shNcentroidsSize;
            public int shNlablesSize;
            public float[] min; // 3x
            public float[] max; // 3x
            public float[] scalesCodebook; // 256x
            public float[] sh0Codebook; // 256x
            public float[] shNcodebook; // 256x
            public byte[] c_meansL;
            public byte[] c_meansU;
            public byte[] c_scales;
            public byte[] c_quads;
            public byte[] c_sh0;
            public byte[] c_shNcentroids;
            public byte[] c_shNlables;
        }

        //!
        //! TRACER structure used to hold texture data.
        //! This sructure is used for saving to disk or sending via network.
        //!
        public class TexturePackage
        {
            //! The size of the texture in bytes.
            public int colorMapDataSize;
            //! The width of the texture.
            public int width;
            //! The height of the texture.
            public int height;
            //! The Unity texture format.
            public TextureFormat format;
            //! The additionally stored Unity texture.
            public Texture texture;
            //! The array of bytes storing the raw texture data. 
            public byte[] colorMapData;
        };

        //!
        //! TRACER structure used to hold material data.
        //! This sructure is used for saving to disk or sending via network.
        //!
        public class MaterialPackage
        {
            //! The type of the material. 0=Unity, 1=External
            public int type;
            //! The name of the material.
            public string name;
            //! The Unity resource name the material uses to refrence its shaders.
            public string src;
            //! The material ID for instancing.
            public int materialID;
            //! The IDs for referencing the associated texture data.
            public int[] textureIds;
            //! The textures UV offset as Vec2.
            public float[] textureOffsets;
            //! The textures UV scale as Vec2.
            public float[] textureScales;
            //! Available shader configuration flags.
            //! [0] "_NORMALMAP"
            //! [1] "_ALPHATEST_ON"
            //! [2] "_ALPHABLEND_ON"
            //! [3] "_ALPHAPREMULTIPLY_ON"
            //! [4] "_EMISSION"
            //! [5] "_PARALLAXMAP"
            //! [6] "_DETAIL_MULX2"
            //! [7] "_METALLICGLOSSMAP"
            //! [8] "_SPECGLOSSMAP"
            public bool[] shaderConfig;
            //! Available shader parameter IDs ( ID, type (not part of this array) )
            //! [0]	"_Color, Color"	
            //! [1]	"_MainTex, Texture"	
            //! [2]	"_Cutoff, float"	
            //! [3]	"_Glossiness, float"
            //! [4]	"_GlossMapScale, float"	
            //! [5]	"_SmoothnessTextureChannel, int"	
            //! [6]	"_Metallic, float"	
            //! [7]	"_MetallicGlossMap, Texture"	
            //! [8]	"_SpecularHighlights, int"
            //! [9]	"_GlossyReflections, int"	
            //! [10] "_BumpScale, int"	
            //! [11] "_BumpMap, Texture"	
            //! [12] "_Parallax, float"	
            //! [13] "_ParallaxMap, Texture"	
            //! [14] "_OcclusionStrength, float"	
            //! [15] "_OcclusionMap, Texture"	
            //! [16] "_EmissionColor, Color"	
            //! [17] "_EmissionMap, Texture"	
            //! [18] "_DetailMask, Texture"	
            //! [19] "_DetailAlbedoMap, Texture"
            //! [20] "_DetailNormalMapScale, int"
            //! [21] "_DetailNormalMap, Texture"
            //! [22] "_UVSec, int"
            //! [23] "_Mode, int"
            //! [24] "_SrcBlend, int"
            //! [25] "_DstBlend, int"
            //! [26] "_ZWrite, int"
            public int[] shaderPropertyIds;
            //! Available shader parameter types
            //! [0]	"Color"	
            //! [1]	"Vector4"	
            //! [2]	"int"	
            //! [3]	"float"
            //! [4]	"Texture"
            public int[] shaderPropertyTypes;
            //! All available shader parameter values as a byte stream.
            public byte[] shaderProperties;
        };
        //!
        //! TRACER structure used to hold material data.
        //! This sructure is used for saving to disk or sending via network.
        //!
        public class ParameterObjectPackage
        {
            //! The _id of the parameter object
            public int id;
            //! The name of the parameter object.
            public string name;
            //// List os Parameters
            //! The types of the parameters inside the parameter object.
            public int[] pTypes;
            //! Flag determines wether a parameter is an RPC parameter.
            public bool[] pRPC;
            //! The names of the parameters inside the parameter object.
            public string[] pNames;
        };
    }
}
