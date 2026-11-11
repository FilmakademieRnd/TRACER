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


//! @file "StaticMeshQuadtree.cs"
//! @brief Performance Helper for Raycasts
//! @author Thomas Krüger
//! @version 1
//! @date 10.11.2026
//! @changed doxygen style documentation

using System.Collections.Generic;
using UnityEngine;

public class StaticMeshQuadtree{
    //!
    //! class for data-structure to cache meshes
    //! @note: for this to work, our meshes HAVE TO enable read/write!
    //!
    public class MeshData{
        public Transform transform; //cached transform
        public Bounds bounds;       //cached renderer bounds
        public Mesh mesh;           //cached shared mesh
        public Vector3[] vertices;  //cached vertices of shared mesh
        public int[] triangles;     //cached triangles of shared mesh
    }

    //!
    //! class our QuadTree to improve performance  for raycasting against the scene
    //!
    private class QuadTreeNode{
        public Rect boundsXZ;   //bounds containing meshes of this node (starting at scene bounds)
        public List<MeshData> meshes = new List<MeshData>();    //meshes for this node
        public QuadTreeNode[] children; //children nodes this node refers to
        public bool isLeaf = true;  //is this node a leaf? a leaf has no children

        public QuadTreeNode(Rect bounds) { boundsXZ = bounds; } //constructor
    }
    //!
    //! the root of our QuadTree to quickly find the corrent meshes to query again
    //!
    private QuadTreeNode root;
    //!
    //! settings of how many meshes a node can have
    //!
    private readonly int maxMeshesPerNode = 10;
    //!
    //! set how depth a node is allowed to be. if reached, it just adds meshes and do not split anymore
    //!
    private int maxDepth = 5;

    //!
    //! class for cached mesh values (see `cache` dictionary)
    //!
    private class MeshArrays { public Vector3[] v; public int[] t; }
    //!
    //! mesh dictionary used as cache
    //!
    private Dictionary<Mesh, MeshArrays> cache = new Dictionary<Mesh, MeshArrays>();

    //! 
    //! buffer to reuse, early-out, no duplicate IntersectRay
    //!
    private readonly List<MeshData> queryBuffer = new List<MeshData>();

    //!
    //! initialize the QuadTree with all rend erer of the scene
    //! @note: these renderers HAVE TO BE static, because we cache the data, objects that move will be queried wrong against!
    //!
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

            Mesh sm = filter.sharedMesh;
            if (!sm.isReadable) {
                Debug.LogWarning("Mesh of '"+rend.gameObject.name+"' `isReadable` not set! Queries will never hit them!");
                continue;
            }

            if (!cache.TryGetValue(sm, out MeshArrays potentialCachedMeshArray)){
                potentialCachedMeshArray = new MeshArrays { v = sm.vertices, t = sm.triangles };
                cache[sm] = potentialCachedMeshArray;
            }
            // MeshData: vertices = arr.v, triangles = arr.t

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
                    mesh = sm,
                    vertices = potentialCachedMeshArray.v,
                    triangles = potentialCachedMeshArray.t
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

    //!
    //! insert the mesh data into the correct node in our quad tree
    //!
    //! @param node our current quad tree node we are at
    //! @param data the mesh data we want to insert into a node
    //! @param depth current depth of this call
    //!
    private void Insert(QuadTreeNode node, MeshData data, int depth){
        Rect meshRect = new Rect(data.bounds.min.x, data.bounds.min.z, data.bounds.size.x, data.bounds.size.z);

        if (!OverlapsInclusive(node.boundsXZ, meshRect)) 
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

    //!
    //! split this node to have 4 child nodes
    //!
    //! @param node our current quad tree node we are at
    //!
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

    //!
    //! finds the highest point on meshes at the given position
    //!
    //! @param accuracy the accuracy we want to use when searching for the hightest point
    //! @param origin the position to use the check at
    //! @param defaultGroundY our ground (lowest) y position
    //!
    public float GetHeightOverGround(RayMeshUtility.Accuracy accuracy, Vector3 origin, float defaultGroundY = 0f){
        if (root == null) 
            return defaultGroundY;

        queryBuffer.Clear();
        Query(root, new Vector2(origin.x, origin.z), queryBuffer);

        float highest = float.NegativeInfinity;
        Ray ray = new Ray(origin, Vector3.down);

        foreach (var data in queryBuffer){
            
            if (data.bounds.max.y <= highest)   // can't beat current best
                continue;              
            
            if (!data.bounds.IntersectRay(ray, out float boxDist)) 
                continue;

            float hitY;
            switch (accuracy){
                case RayMeshUtility.Accuracy.BoundingBox:
                    hitY = origin.y - boxDist; break;
                case RayMeshUtility.Accuracy.NearestVertex:
                    if (!RayMeshUtility.GetNearestVertexHit(ray, data.transform, data.vertices, out var p1)) 
                        continue;
                    hitY = p1.y; break;
                case RayMeshUtility.Accuracy.ExactMesh:
                default:
                    if (!RayMeshUtility.GetExactTriangleHit(ray, data.transform, data.vertices, data.triangles, out var p2))
                        continue;
                    hitY = p2.y; break;
            }
            if (hitY > highest && hitY <= origin.y) 
                highest = hitY;
        }
        return float.IsNegativeInfinity(highest) ? defaultGroundY : highest;
    }

    //!
    //! find matching nodes in our quadtree for a position
    //!
    //! @param node our current quad tree node we check at
    //! @param point the position we want to check at
    //! @param results the list of MeshData we found
    //!
    private void Query(QuadTreeNode node, Vector2 point, List<MeshData> results){
        if (!ContainsInclusive(node.boundsXZ, point)) 
            return;

        if (node.isLeaf){
            results.AddRange(node.meshes);
        }else{
            foreach (var child in node.children) 
                Query(child, point, results);
        }
    }

    //!
    //! helper script  to have an inclusive check for overlaps instead the given .Overlap
    //! 
    //! @param a Rectangle A
    //! @param b Rectangle B
    //! 
    private static bool OverlapsInclusive(Rect a, Rect b) =>
        a.xMin <= b.xMax && a.xMax >= b.xMin && a.yMin <= b.yMax && a.yMax >= b.yMin;
    
    //!
    //! helper script  to have an inclusive check for contains instead the given .Contains
    //! 
    //! @param r Rectangle to check within
    //! @param p Vector2 point to check at
    //! 
    private static bool ContainsInclusive(Rect r, Vector2 p) =>
        p.x >= r.xMin && p.x <= r.xMax && p.y >= r.yMin && p.y <= r.yMax;
}