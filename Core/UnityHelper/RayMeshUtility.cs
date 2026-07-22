using System;
using System.Collections.Generic;
using UnityEngine;

public static class RayMeshUtility{
    public enum Accuracy{
        BoundingBox,    // Fastest, least accurate
        NearestVertex,  // Medium speed, snaps to points
        ExactMesh       // Slowest, perfectly accurate
    }

    // A simple struct to sort our child meshes by how close their bounding box is
    private struct HitCandidate : IComparable<HitCandidate>{
        public MeshFilter filter;
        public float boundsDistance;

        public int CompareTo(HitCandidate other){
            return boundsDistance.CompareTo(other.boundsDistance);
        }
    }
    //!
    //! APPROACH 1: MOST EFFICIENT (Fast Hierarchy)
    //! Checks all children, sorts by closest bounding box, and stops at the FIRST valid mesh hit.
    //! Fast, but might pick the wrong mesh if two objects' bounding boxes heavily intersect.
    //!
    public static bool GetHitPointFast(Ray worldRay, GameObject rootTarget, Accuracy accuracy, out Vector3 hitPoint){
        hitPoint = Vector3.zero;
        MeshRenderer[] renderers = rootTarget.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return false;

        // 1. Bounding Box Pre-Pass
        List<HitCandidate> candidates = new List<HitCandidate>();
        for (int i = 0; i < renderers.Length; i++){
            if (renderers[i].bounds.IntersectRay(worldRay, out float dist)){
                MeshFilter filter = renderers[i].GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null){
                    candidates.Add(new HitCandidate { filter = filter, boundsDistance = dist });
                }
            }
        }

        if (candidates.Count == 0) return false;

        // 2. Sort by closest bounding box
        candidates.Sort();

        // 3. Check the meshes in order of closest bounding box. Stop at the first hit!
        for (int i = 0; i < candidates.Count; i++){
            if (CalculateHit(worldRay, candidates[i].filter, accuracy, out hitPoint)){
                return true; // We found a hit, stop looking!
            }
        }
        return false;
    }

    //!
    //! APPROACH 2: MOST PRECISE (Absolute Hierarchy)
    //! Checks all children whose bounding boxes are hit, calculates exact hits for ALL of them, 
    //! and returns the absolute mathematically closest point.
    //!
    public static bool GetHitPointPrecise(Ray worldRay, GameObject rootTarget, Accuracy accuracy, out Vector3 hitPoint){
        hitPoint = Vector3.zero;
        if(!rootTarget) return false;
        MeshRenderer[] renderers = rootTarget.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return false;

        float absoluteClosestDistance = float.MaxValue;
        bool foundAnyHit = false;

        for (int i = 0; i < renderers.Length; i++){
            // 1. Bounding Box Pre-Pass (Still crucial to skip meshes we completely miss)
            if (renderers[i].bounds.IntersectRay(worldRay, out float _)){
                MeshFilter filter = renderers[i].GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null){
                    // 2. Calculate the exact hit for this specific child
                    if (CalculateHit(worldRay, filter, accuracy, out Vector3 localHit)){
                        float distToHit = Vector3.Distance(worldRay.origin, localHit);
                        // 3. Keep track of the absolute closest point across all children
                        if (distToHit < absoluteClosestDistance){
                            absoluteClosestDistance = distToHit;
                            hitPoint = localHit;
                            foundAnyHit = true;
                        }
                    }
                }
            }
        }

        return foundAnyHit;
    }

    //!
    //! Core calculation where we have hit in world
    //!
    private static bool CalculateHit(Ray worldRay, MeshFilter filter, Accuracy accuracy, out Vector3 hitPoint){
        hitPoint = Vector3.zero;
        Transform objTransform = filter.transform;

        switch (accuracy){
            case Accuracy.BoundingBox:
                if (filter.GetComponent<Renderer>().bounds.IntersectRay(worldRay, out float dist)){
                    hitPoint = worldRay.GetPoint(dist);
                    return true;
                }
                return false;
            case Accuracy.NearestVertex:
                return GetNearestVertexHit(worldRay, objTransform, filter, out hitPoint);
            case Accuracy.ExactMesh:
                return GetExactTriangleHit(worldRay, objTransform, filter, out hitPoint);
        }
        return false;
    }

    private static bool GetNearestVertexHit(Ray worldRay, Transform objTransform, MeshFilter filter, out Vector3 hitPoint){
        hitPoint = Vector3.zero;
        if (filter == null || filter.sharedMesh == null) return false;

        // Transform the ray into local space so we don't have to transform every vertex!
        Ray localRay = new Ray(objTransform.InverseTransformPoint(worldRay.origin), objTransform.InverseTransformDirection(worldRay.direction));
        
        Vector3[] vertices = filter.sharedMesh.vertices;
        float closestDistance = float.MaxValue;
        Vector3 closestLocalVertex = Vector3.zero;
        bool found = false;

        for (int i = 0; i < vertices.Length; i++){
            Vector3 v = vertices[i];
            // Math magic: Distance from point to ray
            Vector3 cross = Vector3.Cross(localRay.direction, v - localRay.origin);
            float distToRay = cross.magnitude;

            if (distToRay < closestDistance){
                // Ensure the vertex is actually IN FRONT of the ray, not behind it
                if (Vector3.Dot(localRay.direction, v - localRay.origin) > 0){
                    closestDistance = distToRay;
                    closestLocalVertex = v;
                    found = true;
                }
            }
        }

        if (found){
            // Convert back to world space
            hitPoint = objTransform.TransformPoint(closestLocalVertex);
            return true;
        }
        return false;
    }

    private static bool GetExactTriangleHit(Ray worldRay, Transform objTransform, MeshFilter filter, out Vector3 hitPoint){
        hitPoint = Vector3.zero;
        if (filter == null || filter.sharedMesh == null) return false;

        Ray localRay = new Ray(objTransform.InverseTransformPoint(worldRay.origin), objTransform.InverseTransformDirection(worldRay.direction));
        
        Vector3[] vertices = filter.sharedMesh.vertices;
        int[] triangles = filter.sharedMesh.triangles;

        float closestHit = float.MaxValue;
        bool found = false;

        // Iterate through every triangle
        for (int i = 0; i < triangles.Length; i += 3){
            Vector3 v0 = vertices[triangles[i]];
            Vector3 v1 = vertices[triangles[i + 1]];
            Vector3 v2 = vertices[triangles[i + 2]];

            if (IntersectTriangle(localRay, v0, v1, v2, out float t)){
                if (t < closestHit){
                    closestHit = t;
                    found = true;
                }
            }
        }

        if (found){
            hitPoint = objTransform.TransformPoint(localRay.GetPoint(closestHit));
            return true;
        }
        return false;
    }

    #region FOR STATIC MESH QUADTREE
    // they already get cached vertices/triangles
    
    public static bool GetNearestVertexHit(Ray worldRay, Transform objTransform, Vector3[] vertices, out Vector3 hitPoint){
        hitPoint = Vector3.zero;
        if (vertices == null) return false;

        // Transform the ray into local space so we don't have to transform every vertex!
        Ray localRay = new Ray(objTransform.InverseTransformPoint(worldRay.origin), objTransform.InverseTransformDirection(worldRay.direction));
        
        float closestDistance = float.MaxValue;
        Vector3 closestLocalVertex = Vector3.zero;
        bool found = false;

        for (int i = 0; i < vertices.Length; i++){
            Vector3 v = vertices[i];
            // Math magic: Distance from point to ray
            Vector3 cross = Vector3.Cross(localRay.direction, v - localRay.origin);
            float distToRay = cross.magnitude;

            if (distToRay < closestDistance){
                // Ensure the vertex is actually IN FRONT of the ray, not behind it
                if (Vector3.Dot(localRay.direction, v - localRay.origin) > 0){
                    closestDistance = distToRay;
                    closestLocalVertex = v;
                    found = true;
                }
            }
        }

        if (found){
            // Convert back to world space
            hitPoint = objTransform.TransformPoint(closestLocalVertex);
            return true;
        }
        return false;
    }

    public static bool GetExactTriangleHit(Ray worldRay, Transform objTransform, Vector3[] vertices, int[] triangles, out Vector3 hitPoint){
        hitPoint = Vector3.zero;

        Ray localRay = new Ray(objTransform.InverseTransformPoint(worldRay.origin), objTransform.InverseTransformDirection(worldRay.direction));
        
        float closestHit = float.MaxValue;
        bool found = false;

        // Iterate through every triangle
        for (int i = 0; i < triangles.Length; i += 3){
            Vector3 v0 = vertices[triangles[i]];
            Vector3 v1 = vertices[triangles[i + 1]];
            Vector3 v2 = vertices[triangles[i + 2]];

            if (IntersectTriangle(localRay, v0, v1, v2, out float t)){
                if (t < closestHit){
                    closestHit = t;
                    found = true;
                }
            }
        }

        if (found){
            hitPoint = objTransform.TransformPoint(localRay.GetPoint(closestHit));
            return true;
        }
        return false;
    }
    #endregion

    // Standard Möller–Trumbore ray-triangle intersection
    private static bool IntersectTriangle(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, out float t){
        t = 0;
        const float EPSILON = 0.0000001f;
        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;
        Vector3 h = Vector3.Cross(ray.direction, edge2);
        float a = Vector3.Dot(edge1, h);

        if (a > -EPSILON && a < EPSILON) return false; // Ray is parallel to triangle

        float f = 1.0f / a;
        Vector3 s = ray.origin - v0;
        float u = f * Vector3.Dot(s, h);

        if (u < 0.0f || u > 1.0f) return false;

        Vector3 q = Vector3.Cross(s, edge1);
        float v = f * Vector3.Dot(ray.direction, q);

        if (v < 0.0f || u + v > 1.0f) return false;

        t = f * Vector3.Dot(edge2, q);
        return t > EPSILON;
    }
}