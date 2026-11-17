#if UNITY_EDITOR
using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression; // Required for unzipping the SOG container safely
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

public class SplatSceneImporter : EditorWindow
{
    private string m_sourceSogPath = "";
    private string m_targetSceneName = "SplatScene";
    private int m_shDegree = 1;

    [System.Serializable]
    private class SogMeansInfo
    {
        public float[] mins;
        public float[] maxs;
    }

    [System.Serializable]
    private class SogCodebookInfo
    {
        public float[] codebook;
    }

    [System.Serializable]
    private class SogShNInfo
    {
        public int count;
        public int bands;
        public float[] codebook;
    }

    [System.Serializable]
    private class SogJsonMetadata
    {
        public int version;
        public int count;
        public SogMeansInfo means;
        public SogCodebookInfo scales;
        public SogCodebookInfo sh0;
        public SogShNInfo shN;
    }

    [MenuItem("TRACER/Tools/Import SOG to TRACER Format")]
    public static void ShowWindow()
    {
        GetWindow<SplatSceneImporter>("Splat Importer");
    }

    void OnGUI()
    {
        GUILayout.Label("TRACER ZIP/SOG Gaussian Splat Converter Tool", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        m_sourceSogPath = EditorGUILayout.TextField("Source SOG File", m_sourceSogPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            m_sourceSogPath = EditorUtility.OpenFilePanel("Select Compressed SOG File", "", "sog");
        }
        EditorGUILayout.EndHorizontal();

        m_targetSceneName = EditorGUILayout.TextField("Target Scene Name", m_targetSceneName);
        m_shDegree = EditorGUILayout.IntSlider("SH Degree Mapping", m_shDegree, 0, 4);

        if (GUILayout.Button("Convert and Inject into TRACER Scene Files"))
        {
            ConvertSogToTracer();
        }
    }

    private void ConvertSogToTracer()
    {
        if (string.IsNullOrEmpty(m_sourceSogPath) || !File.Exists(m_sourceSogPath))
        {
            EditorUtility.DisplayDialog("Error", "Please select a valid source .sog file first!", "OK");
            return;
        }

        try
        {
            // --- STEP 1: DEPLOY THE HEADER FILE (Always cleared and overwritten) ---
            string headerFilePath = Path.Combine(Application.persistentDataPath, m_targetSceneName + ".header");
            byte[] headerBytes = new byte[8];
            System.Span<byte> headerSpan = headerBytes;
            float defaultLightIntensity = 1.0f;
            MemoryMarshal.Write(headerSpan.Slice(0, 4), ref defaultLightIntensity);
            headerSpan[4] = 0;  // senderID
            headerSpan[5] = 60; // frameRate
            headerSpan[6] = 0;  // padding byte 1
            headerSpan[7] = 0;  // padding byte 2
            File.WriteAllBytes(headerFilePath, headerBytes);

            // --- STEP 2: OPEN ZIP ARCHIVE AND EXTRACT INNER CHUNKS ---
            SogJsonMetadata meta = null;
            byte[] c_meansL = Array.Empty<byte>();
            byte[] c_meansU = Array.Empty<byte>();
            byte[] c_scales = Array.Empty<byte>();
            byte[] c_quats = Array.Empty<byte>();
            byte[] c_sh0 = Array.Empty<byte>();
            byte[] c_centroids = Array.Empty<byte>();
            byte[] c_labels = Array.Empty<byte>();

            using (ZipArchive archive = ZipFile.OpenRead(m_sourceSogPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // A. Parse the structured JSON metadata tracker descriptor file
                    if (entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        using (StreamReader reader = new StreamReader(entry.Open()))
                        {
                            string jsonText = reader.ReadToEnd();
                            meta = JsonUtility.FromJson<SogJsonMetadata>(jsonText);
                        }
                    }
                    // B. Extract the raw compressed WebP images matching the real spark file underscores nomenclature
                    else if (entry.Name.Equals("means_l.webp", StringComparison.OrdinalIgnoreCase)) c_meansL = ReadEntryBytes(entry);
                    else if (entry.Name.Equals("means_u.webp", StringComparison.OrdinalIgnoreCase)) c_meansU = ReadEntryBytes(entry);
                    else if (entry.Name.Equals("scales.webp", StringComparison.OrdinalIgnoreCase)) c_scales = ReadEntryBytes(entry);
                    else if (entry.Name.Equals("quats.webp", StringComparison.OrdinalIgnoreCase)) c_quats = ReadEntryBytes(entry);
                    else if (entry.Name.Equals("sh0.webp", StringComparison.OrdinalIgnoreCase)) c_sh0 = ReadEntryBytes(entry);
                    else if (entry.Name.Equals("shN_centroids.webp", StringComparison.OrdinalIgnoreCase)) c_centroids = ReadEntryBytes(entry);
                    else if (entry.Name.Equals("shN_labels.webp", StringComparison.OrdinalIgnoreCase)) c_labels = ReadEntryBytes(entry);
                }
            }

            if (meta == null)
            {
                throw new InvalidDataException("SOG archive error: Could not locate required .json metadata file descriptor.");
            }

            // --- STEP 3: ASSEMBLE SYNCHRONIZED 11-INTEGER BINARY STREAM PAYLOAD ---
            int headerSize = 11 * 4;
            int floatBlockSize = (3 + 3 + 256 + 256 + 256) * 4; // min, max, 3x codebooks
            int totalTargetSize = headerSize + floatBlockSize +
                                  c_meansL.Length + c_meansU.Length + c_scales.Length + c_quats.Length + c_sh0.Length +
                                  c_centroids.Length + c_labels.Length;

            byte[] compiledSplatBuffer = new byte[totalTargetSize];
            System.Span<byte> targetSpan = compiledSplatBuffer;

            // Write 11 metadata integers
            int tIdx = 0;
            BinaryPrimitives.WriteInt32LittleEndian(targetSpan.Slice(tIdx, 4), meta.version > 0 ? meta.version : 2); tIdx += 4;
            BinaryPrimitives.WriteInt32LittleEndian(targetSpan.Slice(tIdx, 4), meta.count); tIdx += 4;

            // FIXED: Read count and bands from the nested shN object!
            int shNCount = meta.shN != null ? meta.shN.count : 0;
            int shNBands = meta.shN != null ? meta.shN.bands : 0;
            BinaryPrimitives.WriteInt32LittleEndian(targetSpan.Slice(tIdx, 4), shNCount); tIdx += 4;
            BinaryPrimitives.WriteInt32LittleEndian(targetSpan.Slice(tIdx, 4), shNBands); tIdx += 4;

            BinaryPrimitives.WriteInt32LittleEndian(targetSpan.Slice(tIdx, 4), c_meansL.Length); tIdx += 4;
            BinaryPrimitives.WriteInt32LittleEndian(targetSpan.Slice(tIdx, 4), c_meansU.Length); tIdx += 4;
            BinaryPrimitives.WriteInt32LittleEndian(targetSpan.Slice(tIdx, 4), c_scales.Length); tIdx += 4;
            BinaryPrimitives.WriteInt32LittleEndian(targetSpan.Slice(tIdx, 4), c_quats.Length); tIdx += 4;
            BinaryPrimitives.WriteInt32LittleEndian(targetSpan.Slice(tIdx, 4), c_sh0.Length); tIdx += 4;
            BinaryPrimitives.WriteInt32LittleEndian(targetSpan.Slice(tIdx, 4), c_centroids.Length); tIdx += 4;
            BinaryPrimitives.WriteInt32LittleEndian(targetSpan.Slice(tIdx, 4), c_labels.Length); tIdx += 4;

            // Pack Float Blocks safely via MemoryMarshal from the nested means object!
            float[] minArr = (meta.means?.mins != null && meta.means.mins.Length >= 3) ? meta.means.mins : new float[] { -1f, -1f, -1f };
            MemoryMarshal.AsBytes((ReadOnlySpan<float>)minArr).CopyTo(targetSpan.Slice(tIdx, 12)); tIdx += 12;

            float[] maxArr = (meta.means?.maxs != null && meta.means.maxs.Length >= 3) ? meta.means.maxs : new float[] { 1f, 1f, 1f };
            MemoryMarshal.AsBytes((ReadOnlySpan<float>)maxArr).CopyTo(targetSpan.Slice(tIdx, 12)); tIdx += 12;

            // Extract real codebooks from the nested JSON objects!
            float[] scCB = EnsureCBSize(meta.scales?.codebook);
            float[] sh0CB = EnsureCBSize(meta.sh0?.codebook);
            float[] shNCB = EnsureCBSize(meta.shN?.codebook);

            MemoryMarshal.AsBytes((ReadOnlySpan<float>)scCB).CopyTo(targetSpan.Slice(tIdx, 1024)); tIdx += 1024;
            MemoryMarshal.AsBytes((ReadOnlySpan<float>)sh0CB).CopyTo(targetSpan.Slice(tIdx, 1024)); tIdx += 1024;
            MemoryMarshal.AsBytes((ReadOnlySpan<float>)shNCB).CopyTo(targetSpan.Slice(tIdx, 1024)); tIdx += 1024;

            // Blit copy inner extracted raw WebP imagery byte arrays straight to target span blocks
            c_meansL.CopyTo(compiledSplatBuffer, tIdx); tIdx += c_meansL.Length;
            c_meansU.CopyTo(compiledSplatBuffer, tIdx); tIdx += c_meansU.Length;
            c_scales.CopyTo(compiledSplatBuffer, tIdx); tIdx += c_scales.Length;
            c_quats.CopyTo(compiledSplatBuffer, tIdx); tIdx += c_quats.Length;
            c_sh0.CopyTo(compiledSplatBuffer, tIdx); tIdx += c_sh0.Length;
            c_centroids.CopyTo(compiledSplatBuffer, tIdx); tIdx += c_centroids.Length;
            c_labels.CopyTo(compiledSplatBuffer, tIdx); tIdx += c_labels.Length;

            string splatsDestPath = Path.Combine(Application.persistentDataPath, m_targetSceneName + ".splats");
            File.WriteAllBytes(splatsDestPath, compiledSplatBuffer);

            // --- STEP 4: FRESHLY OVERWRITE THE NODES FILE (Byte-Symmetrical with convertNodesByteStream) ---
            string nodesFilePath = Path.Combine(Application.persistentDataPath, m_targetSceneName + ".nodes");
            List<byte> modernNodesBuffer = new List<byte>();
            int targetingGsNodeId = 0;

            // Total layout: NodeType (4) + Base Node fields (112) + Extended NodeGS fields (12) = 128 bytes
            byte[] nodeBlock = new byte[128];
            System.Span<byte> nodeSpan = nodeBlock;
            int nIdx = 0;

            // 1. GLOBAL ENGINE HEADER FIELD: NodeType (4 bytes mapping reader.ReadInt32() inside convertNodesByteStream)
            // CRITICAL: Must be the numeric integer value matching your "NodeType.SPLAT" enum tracker!
            // Assuming 6 if GROUP=0, CHARACTER=1, GEO=2, SKINNEDMESH=3, LIGHT=4, CAMERA=5, SPLAT=6. 
            // Change the 6 below to your exact enum integer value if your internal sequence differs!
            int numericSplatTypeID = 6;
            BinaryPrimitives.WriteInt32LittleEndian(nodeSpan.Slice(nIdx, 4), numericSplatTypeID);
            nIdx += 4;

            // 2. BASE NODE FIELD: editable (4 bytes mapping reader.ReadInt32() inside Populate)
            BinaryPrimitives.WriteInt32LittleEndian(nodeSpan.Slice(nIdx, 4), 1); // true
            nIdx += 4;

            // 3. BASE NODE FIELD: childCount (4 bytes)
            BinaryPrimitives.WriteInt32LittleEndian(nodeSpan.Slice(nIdx, 4), 0);
            nIdx += 4;

            // 4. BASE NODE FIELD: position (3x float = 12 bytes)
            Vector3 zeroPos = Vector3.zero;
            MemoryMarshal.Write(nodeSpan.Slice(nIdx, 12), ref zeroPos);
            nIdx += 12;

            // 5. BASE NODE FIELD: scale (3x float = 12 bytes)
            Vector3 oneScale = Vector3.one;
            MemoryMarshal.Write(nodeSpan.Slice(nIdx, 12), ref oneScale);
            nIdx += 12;

            // 6. BASE NODE FIELD: rotation (4x float = 16 bytes)
            Quaternion identityRot = Quaternion.identity;
            MemoryMarshal.Write(nodeSpan.Slice(nIdx, 16), ref identityRot);
            nIdx += 16;

            // 7. BASE NODE FIELD: name (64 bytes - completes the 112 bytes Base layout seamlessly!)
            string pureFileName = Path.GetFileNameWithoutExtension(m_sourceSogPath);
            System.Span<byte> nameFieldSpan = nodeSpan.Slice(nIdx, 64);
            nameFieldSpan.Clear(); // Fill clean with trailing \0 bytes

            if (!string.IsNullOrEmpty(pureFileName))
            {
                if (pureFileName.Length > 63) pureFileName = pureFileName.Substring(0, 63);
                System.Text.Encoding.ASCII.GetBytes(pureFileName, nameFieldSpan);
            }
            nIdx += 64; // Base Node payload is now exactly 112 bytes long!

            // --- EXTENDED NODEGS FIELDS (Read inside your polymorphic SceneNodeSplat.Populate) ---
            // 8. Extended field: gsId
            BinaryPrimitives.WriteInt32LittleEndian(nodeSpan.Slice(nIdx, 4), targetingGsNodeId);
            nIdx += 4;

            // 9. Extended field: shDegree
            BinaryPrimitives.WriteInt32LittleEndian(nodeSpan.Slice(nIdx, 4), m_shDegree);
            nIdx += 4;

            // 10. Extended field: Padding / Custom flags (4 bytes to match 128-byte block alignment safety)
            BinaryPrimitives.WriteInt32LittleEndian(nodeSpan.Slice(nIdx, 4), 0);
            nIdx += 4;

            // Dump the structural synchronized format array straight onto persistent disk storage
            modernNodesBuffer.AddRange(nodeBlock);
            File.WriteAllBytes(nodesFilePath, modernNodesBuffer.ToArray());
            EditorUtility.DisplayDialog("Success", $"Clean Scene Zip/SOG Import Complete!\n\nAsset '{pureFileName}' integrated.\nSplats: {meta.count}\nSH Bands: {meta.shN}", "OK");
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Conversion Failed", $"Critical SOG container zip unzip failure: {ex.Message}", "OK");
        }
    }
    private byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            using (Stream s = entry.Open())
            {
                s.CopyTo(ms);
            }
            return ms.ToArray();
        }
    }
    private float[] EnsureCBSize(float[] source)
    {
        if (source != null && source.Length == 256) return source;
        float[] fallback = new float[256];
        if (source != null) Array.Copy(source, fallback, Math.Min(source.Length, 256));
        return fallback;
    }
}
#endif