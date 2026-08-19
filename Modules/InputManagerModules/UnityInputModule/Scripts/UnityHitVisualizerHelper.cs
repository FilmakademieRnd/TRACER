//! @file "UnityHitVisualizerHelper.cs"
//! @brief Helper to visualize things (hits) within Unity
//! @author Thomas Krüger
//! @version 0
//! @date 15.04.2026

using UnityEngine;

public class UnityHitVisualizerHelper : MonoBehaviour{
    public float lifetime = 0.5f;
    private float timer = 0f;
    private Vector3 initialScale;

    private void Start(){
        initialScale = transform.localScale;
    }

    private void Update(){
        timer += Time.deltaTime;
        float normalizedTime = timer / lifetime;

        if (normalizedTime >= 1f){
            Destroy(gameObject);
            return;
        }

        // Float upwards slightly
        //transform.position += Vector3.up * Time.deltaTime * 0.1f;

        // Ease-in shrink to zero
        float scaleMultiplier = 1f - (normalizedTime * normalizedTime); 
        transform.localScale = initialScale * scaleMultiplier;
    }

    // Call this static method from anywhere to spawn the visualizer
    public static void Spawn(Vector3 position, Color color, float size = 0.1f){
        
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * size;
        
        // Remove the collider so it doesn't block future raycasts!
        Destroy(sphere.GetComponent<Collider>());

        // Optional: Set color (will only work if the default material supports color setting)
        Renderer rend = sphere.GetComponent<Renderer>();
        if (rend != null){
            // Use a material property block to avoid creating material duplicates in memory
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);        // Standard/URP
            block.SetColor("_BaseColor", color);    // HDRP
            rend.SetPropertyBlock(block);
        }

        sphere.AddComponent<UnityHitVisualizerHelper>();
    }
}