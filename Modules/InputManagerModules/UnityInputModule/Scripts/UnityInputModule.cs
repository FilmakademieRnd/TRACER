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

//! @file "UnityInputModule.cs"
//! @brief implementation of TRACER input features from Unity
//! all raised events from unitys input are implemented here and will call the specific InputManager's events
//! @author Thomas Krüger
//! @version 0
//! @date 31.03.2026

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;  //one has to add this to "Assembly Definition Reference" in the "ModulesAssembly"

namespace tracer{
    //!
    //! implementation of TRACER camera navigation
    //!
    public class UnityInputModule : InputManagerModule{

        public const float MAX_DOUBLECLICK_GAP = 0.25f;

        #region VARIABLES
        //!
        //! The generated Unity input class defining all available user inputs.
        //!
        private Inputs m_inputs;

        //!
        //! The latest main input position (primary touch, mouse pos)
        //!
        private Vector2 m_pos;
        
        //!
        //! the position buffer for further calculations (two finger camera manipulation e.g. zoom, ...)
        //!
        private InputManager.SeparateBufferClass m_posBuffer;

        //!
        //! timer for our last click to check if we have a double-click
        //!
        private float m_lastClickTime;

        //!
        //! the layer we hit with our primary input, to determine a valid double click (hit the same layer)
        //!
        private InputManager.LayerToOperate primaryInputLayerHit = InputManager.LayerToOperate.world;
        
        // [REVIEW] create a class for hit object and hit pos ?

        //!
        //! the object we hit in our last layer-to-operate evaluation (do not execute multiple times)
        //!
        private GameObject m_uiGameObjectWeHit, m_gameObjectWeHit, m_worldGameObjectWeHit;
        //!
        //! the world position were a hit occured
        //!
        private Vector3 m_worldHitPos;
        //!
        //! We create a custom action entirely in code, no Asset required, checking for ANY input
        //!
        private InputAction anyInputAction;
        //!
        //! reference to tthe UIManager
        //!
        private UIManager uiManager;
        //!
        //! a reference to the mainCam to not search by tag via Camera.main
        //!
        private Camera mainCam;

        #endregion


        #region MODULE SETUP

        //!
        //! Constructor.
        //!
        //! @param name Name of this module.
        //! @param manager Reference to our Manager our class inherits from
        //!
        public UnityInputModule(string name, Manager manager) : base(name, manager){

        }


        //! 
        //! Function called when Unity initializes the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Init(object sender, EventArgs e){
            
            uiManager = core.getManager<UIManager>();
            mainCam = Camera.main;

            //enable input
            m_inputs = new Inputs();
            m_inputs.VPETMap.Enable();

            //add listener
            m_inputs.VPETMap.Position.performed             += ProcessPositionInput;
            m_inputs.VPETMap.OnPrimaryInputClick.performed  += ProcessPrimaryInputClick;
            //trigger "any input detected"
            SetupAnyInputAction();
            anyInputAction.performed                        += ProcessAnyInput;

            //variable init
            m_posBuffer = new InputManager.SeparateBufferClass();
        }

        //!
        //! setup the unity input action via code
        //!
        private void SetupAnyInputAction() {
            anyInputAction = new InputAction(type: InputActionType.Button);
            anyInputAction.AddBinding("/*/<button>");       // 1. Catch every keyboard key, gamepad button, or joystick button
            anyInputAction.AddBinding("<Pointer>/press");   // 2. Catch mouse clicks, pen taps, and touchscreen presses
            //maybe also add joystick/mouse movement?
            anyInputAction.Enable();                        // The action must be enabled to start listening to the hardware
        }

        //!
        //! Destructor, cleaning up event registrations. 
        //!
        public override void Dispose(){
            base.Dispose();

            // Unsubscribe
            m_inputs.VPETMap.Position.performed             -= ProcessPositionInput;
            m_inputs.VPETMap.OnPrimaryInputClick.performed  -= ProcessPrimaryInputClick;
            //clean the unity any-input action
            // Always clean up dynamic actions to prevent memory leaks
            if (anyInputAction != null){
                anyInputAction.performed                    -= ProcessAnyInput;
                anyInputAction.Disable();
                anyInputAction.Dispose();
            }
        }

        #endregion

        #region GENERAL
        //!
        //! tracks the positions of our primary input (primary touch, mouse pos)
        //! and writes them into a buffer to allow further calculations (delta, speed, etc)
        //!
        private void ProcessPositionInput(InputAction.CallbackContext obj){ 
            // Get the position
            m_pos = m_inputs.VPETMap.Position.ReadValue<Vector2>();
            //necessary to set pos within the input module? -> NO ONLY WRAPPER

            m_posBuffer.SetBufferOnce(m_pos);
            // Update buffer
            m_posBuffer.OverrideBuffer(m_pos);
        }
        //!
        //! call ProcessInputDetected in the manager
        //! which is currently used to execute IDExtractorModule
        //!
        private void ProcessAnyInput(InputAction.CallbackContext obj) {
            manager.ProcessInputDetected(m_pos);
        }

        //!
        //! Input click/touch
        //! mapped to primary touch and left mouse click as tap interaction (below 0.2s, no hold)
        //! ignores 2d ui hits (they have their own event)
        //!
        private void ProcessPrimaryInputClick(InputAction.CallbackContext c){

            //Do not execute if we processed _ANY_ other input (possible? one finger drag, quick pinch-zoom)
            SetLayerToOperate(m_pos);

            //TODO: make this 'layerWeAre' globally available
            //  then utilize within Dual/Triple Move (Drag/Pinch) Input
            //layer may get overwritten from further input
            //e.g. touch pinch may use 2d (timeline) or world (cam zoom) if we dont want objects to get scaled by this
            //call and set on IM before anything else
            //InputManager.InputLayerType layerWeAreAt = DetermineLayerWeHit();
            //? maybe check all layer and send all information (2dui hit, 3dui hit, selectable hit)

            if (WasDoubleClick()){
                ProcessDoubleClickInput(c);
                return;
            }

            SetLastClickTime();
            
            switch (manager.layerToOperate){
                case InputManager.LayerToOperate.ui2d:
                    manager.ProcessPrimaryInteract(m_pos, m_uiGameObjectWeHit);
                    break;
                case InputManager.LayerToOperate.ui3d:
                    manager.ProcessPrimaryInteract(m_pos, m_gameObjectWeHit);
                    break;
                case InputManager.LayerToOperate.selectable:
                    manager.ProcessPrimaryInteract(m_pos, m_gameObjectWeHit);
                    //if we store hitWorldObject and selectableObject seperate within InputEventHandlerArgs
                    //we could use the below function in seperate module and addlistener to InputManager events!
                    if (RayMeshUtility.GetHitPointPrecise(mainCam.ScreenPointToRay(m_pos), m_worldGameObjectWeHit, RayMeshUtility.Accuracy.NearestVertex, out m_worldHitPos)){
                        UnityHitVisualizerHelper.Spawn(m_worldHitPos, Color.green, 0.15f);
                    }
                    break;
                case InputManager.LayerToOperate.world:
                    manager.ProcessPrimaryInteract(m_pos, m_worldGameObjectWeHit);
                    if (RayMeshUtility.GetHitPointPrecise(mainCam.ScreenPointToRay(m_pos), m_worldGameObjectWeHit, RayMeshUtility.Accuracy.ExactMesh, out m_worldHitPos)){
                        UnityHitVisualizerHelper.Spawn(m_worldHitPos, Color.green, 0.15f);
                    }
                    break;
            }

            //--- DEBUG
            Debug.Log("<color=yellow>primary input click</color>");
            Ray debugRay = mainCam.ScreenPointToRay(m_pos);
            Debug.DrawRay(debugRay.origin, debugRay.direction*100, Color.yellow, 2f);
            //---------- END DEBUG
        }

        //!
        //! our own double click check via primary click/tap
        //!
        private void ProcessDoubleClickInput(InputAction.CallbackContext c){
            
            //manager.ProcessFocus(SceneObject, pos) -> check if Focus is allowed -> invoke
            //  invoke will be executed within e.g. CameraNavigationModule, SelectionModule
            //or manager ProcessDoubleClick... ?

            ResetLastClickTime();
            
            //--- DEBUG
            Debug.Log("<color=yellow>primary input double-click</color>");
            Ray debugRay = mainCam.ScreenPointToRay(m_pos);
            Debug.DrawRay(debugRay.origin, debugRay.direction*100, Color.yellow, 2f);
            //---------- END DEBUG


        }
        #endregion

        #region HELPER

        //!
        //! use (input) position to check what layershould be used/would be hit
        //! @param pos position we should use to check, mostly the input, maybe a mid-point
        //!
        private void SetLayerToOperate(Vector2 pos){

            if(Is2dUiElement(pos)){
                manager.SetLayerToOperate(InputManager.LayerToOperate.ui2d);
            }else if(Is3dUiElement(pos)){
                manager.SetLayerToOperate(InputManager.LayerToOperate.ui3d);
            }else if (IsSelectable(pos) || IsSelectableAtPixel(pos)) {
                manager.SetLayerToOperate(InputManager.LayerToOperate.selectable);
            }else{
                manager.SetLayerToOperate(InputManager.LayerToOperate.world);
                
                object nonSceneObject = uiManager.GetWorldObjectAtPixel((int)pos.x, (int)pos.y);
                if(nonSceneObject != null)
                    m_worldGameObjectWeHit = nonSceneObject as GameObject;
            }

            switch (manager.layerToOperate){
                case InputManager.LayerToOperate.ui2d:
                    //Since we use Unity's Canvas and UI-Elements for Events, we straight skip any input hitting any of these
                    Debug.Log("layer to operate set <color=grey>@2d ui</color>");
                    break;
                case InputManager.LayerToOperate.ui3d:
                    Debug.Log("layer to operate set <color=grey>@3d world ui</color>");
                    break;
                case InputManager.LayerToOperate.selectable:
                    Debug.Log("layer to operate set <color=grey>@selectable</color>");
                    break;
                case InputManager.LayerToOperate.world:
                    Debug.Log("layer to operate set <color=grey>@world (hit nothing)</color>");
                    break;
            }
        }

        //!
        //! returns true if pos is over any UI element
        //! (it goes over all raycaster in the scene - ideally that would be GraphicRaycaster from the 2D UI)
        //!
        //! @param pos position of the click/tap
        //!
        private bool Is2dUiElement(Vector2 pos){
            PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current){ position = pos };
            List<RaycastResult> m_raycastList = new List<RaycastResult>(5);
            EventSystem.current.RaycastAll(eventDataCurrentPosition, m_raycastList);
            if(m_raycastList.Count > 0) {
                m_uiGameObjectWeHit = m_raycastList[0].gameObject;
                m_worldGameObjectWeHit = null;
                return true;
            }
            return false;
        }

        //!
        //! returns true if pos is over any 3D manipulator object (layerMask 0 for Default)
        //! [REVIEW] should be seperate 3D-UI Layer
        //!
        private bool Is3dUiElement(Vector2 pos){
            bool hitObject = Physics.Raycast(mainCam.ScreenPointToRay(pos), out RaycastHit hitInfo, Mathf.Infinity, 1 << 0);
            if (hitObject) {
                m_gameObjectWeHit = hitInfo.transform.gameObject;
                m_worldGameObjectWeHit = null;
                m_worldHitPos = hitInfo.point;
                return true;
            }
            return false;
        }

        //!
        //! returns true if pos is over any 3d selectable object
        //! [REVIEW] will never hit, since there are no colliders (despite 3d ui gizmos) in the scene!
        //!
        private bool IsSelectable(Vector2 pos){
            int layerMask = 1 << 5; //layer 5 (UI)
            layerMask = ~layerMask; //all but UI

            if (Physics.Raycast(mainCam.ScreenPointToRay(pos), out RaycastHit hitInfo, Mathf.Infinity, layerMask)){
                m_worldGameObjectWeHit = hitInfo.transform.gameObject;
                SceneObject sceneObject = m_worldGameObjectWeHit.GetComponent<SceneObject>();
                m_worldHitPos = hitInfo.point;
                if (sceneObject) {
                    m_gameObjectWeHit = sceneObject.gameObject;
                    return true;
                }
                sceneObject = m_worldGameObjectWeHit.GetComponentInParent<SceneObject>();
                if (sceneObject) {
                    m_gameObjectWeHit = sceneObject.gameObject;
                    return true;
                }
            }
            return false;
        }

        //!
        //! returns true if pos is over any 3d selectable object 
        //! (uses color array which gets created via rtx)
        //!
        private bool IsSelectableAtPixel(Vector2 pos) {
            SceneObject foundSO = uiManager.GetSelectableAtPixel((int)pos.x, (int)pos.y);
            if (foundSO) {
                m_worldGameObjectWeHit = foundSO.gameObject;
                m_gameObjectWeHit = foundSO.gameObject;
                return true;
            }
            return false;
        }

        //!
        //! sets current primary input click time and layer for further double-click checks
        //! TODO: remove layertype and put into other function!
        private void SetLastClickTime(){
            primaryInputLayerHit = manager.layerToOperate;
            m_lastClickTime = Time.time; 
        }

        //!
        //! reset the time we use to check for double click in various cases
        //! e.g. if we executed a double click or performed other inputs
        //!
        private void ResetLastClickTime(){
            m_lastClickTime = 0; 
        }

        //!
        //! make a double click check (time within gap, same layer as before)
        //! todo: we could also add a position-delta to check
        //!
        private bool WasDoubleClick(){
            if(primaryInputLayerHit != manager.layerToOperate)   //if layer is different, reset time - no double-click!
                ResetLastClickTime();
            return Time.time - m_lastClickTime < MAX_DOUBLECLICK_GAP; 
        }
        #endregion

        /*
        # region MOUSE

        # endregion

        # region KEYBOARD

        # endregion

        # region TOUCH

        # endregion

        # region CONTROLLER

        # endregion
        */

    }
}

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