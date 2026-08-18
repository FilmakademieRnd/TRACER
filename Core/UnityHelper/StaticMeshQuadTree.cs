using System.Collections.Generic;
using UnityEngine;

public class StaticMeshQuadtree{
    // --- Datenstruktur zum Cachen der Meshes ---
    public class MeshData{
        public Transform transform;
        public Bounds bounds;
        public Mesh mesh;
        public Vector3[] vertices; 
        public int[] triangles;
    }

    private class QuadTreeNode{
        public Rect boundsXZ;
        public List<MeshData> meshes = new List<MeshData>();
        public QuadTreeNode[] children;
        public bool isLeaf = true;

        public QuadTreeNode(Rect bounds) { boundsXZ = bounds; }
    }

    private QuadTreeNode root;
    private int maxMeshesPerNode = 10;
    private int maxDepth = 5;

    /// <summary>
    /// Initialisiert den Quadtree mit allen statischen Renderern der Szene.
    /// </summary>
    public void BuildTree(){
        MeshRenderer[] allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<MeshData> allMeshData = new List<MeshData>();
        Bounds sceneBounds = new Bounds(Vector3.zero, Vector3.zero);

        bool first = true;

        Debug.Log($"StaticMeshQuadtree found {allRenderers.Length} meshrenderer.");

        foreach (var rend in allRenderers){
            // Gizmos oder dynamische Objekte ausschließen (z.B. über Layer oder Tag)
            // if (rend.gameObject.layer == LayerMask.NameToLayer("Gizmo")) 
            //     continue;

            MeshFilter filter = rend.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) 
                continue;

            Bounds b = rend.bounds;
            if (first) { 
                sceneBounds = b; 
                first = false; }
            else 
                sceneBounds.Encapsulate(b);

            allMeshData.Add(
                new MeshData{
                    transform = rend.transform, 
                    bounds = b, 
                    mesh = filter.sharedMesh,
                    vertices = filter.sharedMesh.vertices,
                    triangles = filter.sharedMesh.triangles
                });
        }

        // Erstelle die Root-Node basierend auf der gesamten XZ-Ausdehnung der Szene
        Rect rootRect = new Rect(sceneBounds.min.x, sceneBounds.min.z, sceneBounds.size.x, sceneBounds.size.z);
        root = new QuadTreeNode(rootRect);

        // Füge alle Meshes in den Tree ein
        foreach (var data in allMeshData){
            Insert(root, data, 0);
        }
        
        Debug.Log($"StaticMeshQuadtree built with {allMeshData.Count} meshes.");
    }

    private void Insert(QuadTreeNode node, MeshData data, int depth){
        Rect meshRect = new Rect(data.bounds.min.x, data.bounds.min.z, data.bounds.size.x, data.bounds.size.z);

        if (!node.boundsXZ.Overlaps(meshRect)) 
            return;

        if (node.isLeaf){
            node.meshes.Add(data);

            if (node.meshes.Count > maxMeshesPerNode && depth < maxDepth){
                Split(node);
                List<MeshData> oldMeshes = new List<MeshData>(node.meshes);
                node.meshes.Clear();

                foreach (var m in oldMeshes){
                    foreach (var child in node.children) Insert(child, m, depth + 1);
                }
            }
        }else{
            foreach (var child in node.children) 
                Insert(child, data, depth + 1);
        }
    }

    private void Split(QuadTreeNode node){
        node.isLeaf = false;
        node.children = new QuadTreeNode[4];
        float w = node.boundsXZ.width / 2f;
        float h = node.boundsXZ.height / 2f;
        float x = node.boundsXZ.x;
        float y = node.boundsXZ.y;

        node.children[0] = new QuadTreeNode(new Rect(x, y, w, h));
        node.children[1] = new QuadTreeNode(new Rect(x + w, y, w, h));
        node.children[2] = new QuadTreeNode(new Rect(x, y + h, w, h));
        node.children[3] = new QuadTreeNode(new Rect(x + w, y + h, w, h));
    }

    /// <summary>
    /// Findet das höchste Mesh unterhalb einer bestimmten Position.
    /// </summary>
    public float GetHeightOverGround(RayMeshUtility.Accuracy accuracy, Vector3 origin, float defaultGroundY = 0f){
        if (root == null) return defaultGroundY;

        List<MeshData> candidates = new List<MeshData>();
        Query(root, new Vector2(origin.x, origin.z), candidates);

        float highestGroundY = float.MinValue;
        bool foundAny = false;

        Ray downwardRay = new Ray(origin, Vector3.down);

        //Debug.Log("Found candidates "+candidates.Count);

        foreach (var data in candidates){
            // 1. Grober Check: Ist die Bounding Box überhaupt unter uns?
            if (!data.bounds.IntersectRay(downwardRay, out float distanceToBox)) continue;

            // 2. Präziser Check: Hier kommt eure RayMeshUtility ins Spiel!             
            switch (accuracy){
                case RayMeshUtility.Accuracy.BoundingBox:
                    if (data.bounds.IntersectRay(downwardRay, out float dist)){
                        float hitY = origin.y - dist;
                        if (hitY > highestGroundY && hitY <= origin.y){
                            highestGroundY = hitY;
                            foundAny = true;
                        }
                    }
                    break;
                case RayMeshUtility.Accuracy.NearestVertex:
                    if (RayMeshUtility.GetNearestVertexHit(downwardRay, data.transform, data.vertices, out Vector3 hitPosition)){
                        float hitY = hitPosition.y;
                        if (hitY > highestGroundY && hitY <= origin.y){
                            highestGroundY = hitY;
                            //Debug.Log("Found higher object "+data.transform.gameObject.name+" at "+hitY);
                            foundAny = true;
                        }
                    }
                    break;
                case RayMeshUtility.Accuracy.ExactMesh:
                    if (RayMeshUtility.GetExactTriangleHit(downwardRay, data.transform, data.vertices, data.triangles, out Vector3 hitPos)){
                        float hitY = hitPos.y;
                        if (hitY > highestGroundY && hitY <= origin.y){
                            highestGroundY = hitY;
                            foundAny = true;
                        }
                    }
                    break;
            }
        }

        //Debug.Log("Found any? "+foundAny+ " _ highestGroundY:"+highestGroundY);

        return foundAny ? highestGroundY : defaultGroundY;
    }

    private void Query(QuadTreeNode node, Vector2 point, List<MeshData> results){
        if (!node.boundsXZ.Contains(point)) 
            return;

        if (node.isLeaf){
            results.AddRange(node.meshes);
        }else{
            foreach (var child in node.children) 
                Query(child, point, results);
        }
    }
}