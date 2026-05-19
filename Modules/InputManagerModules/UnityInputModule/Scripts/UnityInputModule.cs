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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;  //one has to add this to "Assembly Definition Reference" in the "ModulesAssembly"

namespace tracer{
    //!
    //! implementation of TRACER camera navigation
    //!
    public class UnityInputModule : InputManagerModule{

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
        //! The latest main input delta (primary touch, mouse pos)
        //!
        private Vector2 m_delta;
        
        //!
        //! We create a custom action entirely in code, no Asset required, checking for ANY input
        //!
        private InputAction anyInputAction;

        //!
        //! a reference to the mainCam to not search by tag via Camera.main
        //!
        private Camera mainCam;

        public enum InteractionState { 
            Idle,           // Nothing is happening
            Evaluating,     // Pointer is down, waiting to see if it becomes Click, Drag, or Hold
            Dragging,       // Surpassed distance threshold (Holds are now denied)
            Holding,        // Surpassed time threshold (Drags are now denied)
            Pinching,       // Surpassed pinch delta (Drags/Holds denied)
            Rotating        // Surpassed rotation delta (Drags/Holds denied)
        }

        [Header("Interaction Thresholds")]
        public float dragDistanceThreshold = 15f; 
        public float clickTimeThreshold = 0.3f;
        public float holdTimeThreshold = 0.4f;
        public float doubleClickTimeThreshold = 0.35f;

        // Note: These values rely on your UI/Screen scale
        public float pinchDistanceThreshold = 5f; 
        public float rotateAngleThreshold = 2f;

        // TODO: put into InputManager & remove Unity dependency, so other modules could utilize it without referencing to other module
        // e.g. like the AttitudeModule!
        public class InputTracker{
            public InputManager.InputLevel Level;   //primary, secondary, tertiary
            public InteractionState State = InteractionState.Idle;  //see above
        
            public float TimeDown;
            public Vector2 StartPosition;
            public float LastClickTime = -100f; // Tracked for Double Click

            public InputTracker(InputManager.InputLevel level){ Level = level; }
            public void Reset(){ State = InteractionState.Idle; }
        }

        private InputTracker _primary   = new InputTracker(InputManager.InputLevel.Primary);
        private InputTracker _secondary = new InputTracker(InputManager.InputLevel.Secondary);
        private InputTracker _tertiary  = new InputTracker(InputManager.InputLevel.Tertiary);

        //to request at start, but not ongoing!
        private EvaluationHelper.OperationLayer layerDrag, layerHold, layerPinch, layerRotate = EvaluationHelper.OperationLayer.OTHER;

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
            
            mainCam = Camera.main;

            m_manager.core.updateEvent += OnCoreUpdateEvent;

            //enable input
            m_inputs = new Inputs();

            //add listener
            //trigger "any input detected"
            SetupAnyInputAction();
            anyInputAction.performed += ProcessAnyInput;

            // --- POSITION ---
            //m_inputs.VPETMap.Position.performed += ProcessPositionInput;

            // --- PRIMARY (1-Finger / Left Mouse) ---
            m_inputs.VPETMap.OnPrimaryInputClick.started += ctx => OnPointerDown(_primary);
            m_inputs.VPETMap.OnPrimaryInputClick.canceled += ctx => OnPointerUp(_primary);

            // --- SECONDARY (2-Fingers / Right Mouse) ---
            m_inputs.VPETMap.OnSecondaryInputClick.started += ctx => OnPointerDown(_secondary);
            m_inputs.VPETMap.OnSecondaryInputClick.canceled += ctx => OnPointerUp(_secondary);

            /*
            // --- TERTIARY (3-Fingers / Middle Mouse) ---
            m_inputs.VPETMap.OnTertiaryInputClick.started += ctx => OnPointerDown(_tertiary);
            m_inputs.VPETMap.OnTertiaryInputClick.canceled += ctx => OnPointerUp(_tertiary);

            // --- GESTURES (Scrollwheel, Triggers, Touch Pinch/Rotate) ---
            m_inputs.VPETMap.Pinch.performed += ProcessPinchInput;
            m_inputs.VPETMap.Pinch.canceled += ProcessPinchInput;
            
            m_inputs.VPETMap.Rotate.performed += ProcessRotateInput;
            m_inputs.VPETMap.Rotate.canceled += ProcessRotateInput;
            */

            m_inputs.VPETMap.Enable();

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

            m_manager.core.updateEvent -= OnCoreUpdateEvent;

            // Unsubscribe
            //m_inputs.VPETMap.Position.performed += ProcessPositionInput;

            // --- PRIMARY (1-Finger / Left Mouse) ---
            m_inputs.VPETMap.OnPrimaryInputClick.started -= ctx => OnPointerDown(_primary);
            m_inputs.VPETMap.OnPrimaryInputClick.canceled -= ctx => OnPointerUp(_primary);

            m_inputs.VPETMap.OnSecondaryInputClick.started -= ctx => OnPointerDown(_secondary);
            m_inputs.VPETMap.OnSecondaryInputClick.canceled -= ctx => OnPointerUp(_secondary);

            /*
            m_inputs.VPETMap.OnTertiaryInputClick.started -= ctx => OnPointerDown(_tertiary);
            m_inputs.VPETMap.OnTertiaryInputClick.canceled -= ctx => OnPointerUp(_tertiary);

            m_inputs.VPETMap.Pinch.performed -= ProcessPinchInput;
            m_inputs.VPETMap.Pinch.canceled -= ProcessPinchInput;
            
            m_inputs.VPETMap.Rotate.performed -= ProcessRotateInput;
            m_inputs.VPETMap.Rotate.canceled -= ProcessRotateInput;
            */
            
            //clean the unity any-input action
            // Always clean up dynamic actions to prevent memory leaks
            if (anyInputAction != null){
                anyInputAction.performed                    -= ProcessAnyInput;
                anyInputAction.Disable();
                anyInputAction.Dispose();
            }
        }

        #endregion

        #region PROCESSION

        //! obsolete - this event driven approach is not good for continous values
        //! tracks the positions of our primary input (primary touch, mouse pos)
        //! and writes them into a buffer to allow further calculations (delta, speed, etc)
        //!
        private void ProcessPositionInput(){ 
            // Get the position
            Vector2 newPos = m_inputs.VPETMap.Position.ReadValue<Vector2>();
            m_delta = newPos - m_pos;
            m_pos = newPos;
        }
        //!
        //! call ProcessInputDetected in the manager
        //! which is currently used to execute IDExtractorModule
        //!
        private void ProcessAnyInput(InputAction.CallbackContext obj) {
            //manager.ProcessInputDetected(m_pos);
            InputManager.InputData anyInputData = new() {
                Level = InputManager.InputLevel.Primary,
                State = InputManager.InputState.Ended,
                Position = m_pos,
                Delta = m_delta
            };
            manager.Publish(new InputManager.AnyInputEvent { Data = anyInputData });
        }

        // --- THE UPDATE LOOP (finite state machine) ---
        //!
        //! Callback from TRACER _core when Unity calls it's render update
        //!
        private void OnCoreUpdateEvent(object sender, EventArgs e){
            
            ProcessPositionInput();

            ProcessTracker(_primary);
            ProcessTracker(_secondary);
            ProcessTracker(_tertiary);
        }

        private void ProcessTracker(InputTracker tracker) {
            if (tracker.State == InteractionState.Idle || tracker.State == InteractionState.Pinching || tracker.State == InteractionState.Rotating) { return; }

            if (tracker.State == InteractionState.Dragging) {
                FireDragEvent(tracker.Level, InputManager.InputState.Ongoing);
                return;
            }
            
            if (tracker.State == InteractionState.Holding) {
                FireHoldEvent(tracker.Level, InputManager.InputState.Ongoing);
                return;
            }

            if (tracker.State == InteractionState.Evaluating) {
                float distanceFromStart = Vector2.Distance(tracker.StartPosition, m_pos);
                float timeHeld = Time.time - tracker.TimeDown;

                // Distance overrides Time (Drag denies Hold)
                if (distanceFromStart > dragDistanceThreshold) {
                    tracker.State = InteractionState.Dragging;
                    FireDragEvent(tracker.Level, InputManager.InputState.Started);
                } 
                // Time overrides Distance (Hold denies Drag)
                else if (timeHeld > holdTimeThreshold) {
                    tracker.State = InteractionState.Holding;
                    FireHoldEvent(tracker.Level, InputManager.InputState.Started);
                }
            }
        }

        private void ProcessPinchInput(InputAction.CallbackContext ctx) {
            float pinchDelta = ctx.ReadValue<float>();
            
            // Deadzone check
            if (Mathf.Abs(pinchDelta) < 0.01f && ctx.phase != InputActionPhase.Canceled) { return; }

            // Example: Map 2-finger pinch to Primary. (Adjust if your logic maps it to Secondary)
            InputTracker tracker = _primary; 

            if (ctx.phase == InputActionPhase.Canceled && tracker.State == InteractionState.Pinching) {
                FirePinchEvent(tracker.Level, InputManager.InputState.Ended, pinchDelta);
                tracker.Reset();
            } else {
                if (tracker.State == InteractionState.Evaluating || tracker.State == InteractionState.Dragging || tracker.State == InteractionState.Idle) {
                    tracker.State = InteractionState.Pinching;
                    FirePinchEvent(tracker.Level, InputManager.InputState.Started, pinchDelta);
                } else if (tracker.State == InteractionState.Pinching) {
                    FirePinchEvent(tracker.Level, InputManager.InputState.Ongoing, pinchDelta);
                }
            }
        }

        private void ProcessRotateInput(InputAction.CallbackContext ctx) {
            float rotateDelta = ctx.ReadValue<float>();
            
            if (Mathf.Abs(rotateDelta) < 0.01f && ctx.phase != InputActionPhase.Canceled) { return; }

            InputTracker tracker = _primary; // Map to primary or secondary depending on your scheme

            if (ctx.phase == InputActionPhase.Canceled && tracker.State == InteractionState.Rotating) {
                FireRotateEvent(tracker.Level, InputManager.InputState.Ended, rotateDelta);
                tracker.Reset();
            } else {
                if (tracker.State == InteractionState.Evaluating || tracker.State == InteractionState.Dragging || tracker.State == InteractionState.Idle) {
                    tracker.State = InteractionState.Rotating;
                    FireRotateEvent(tracker.Level, InputManager.InputState.Started, rotateDelta);
                } else if (tracker.State == InteractionState.Rotating) {
                    FireRotateEvent(tracker.Level, InputManager.InputState.Ongoing, rotateDelta);
                }
            }
        }
        #endregion

        #region UP/DOWN-PHASES

        private void OnPointerDown(InputTracker tracker) {
            // If we are currently pinching or rotating, deny starting a new click/drag evaluation
            if (tracker.State == InteractionState.Pinching || tracker.State == InteractionState.Rotating) { return; }

            // DEBUG
            // Debug.Log("<color=yellow>Primary Input Click Started</color>");
            // DebugPointer(tracker);
            // -----

            tracker.State = InteractionState.Evaluating;
            tracker.TimeDown = Time.time;
            tracker.StartPosition = m_pos;
        }

        private void OnPointerUp(InputTracker tracker) {
            if (tracker.State == InteractionState.Idle) { return; }

            if (tracker.State == InteractionState.Evaluating) {
                float duration = Time.time - tracker.TimeDown;
                
                if (duration <= clickTimeThreshold) {
                    if (Time.time - tracker.LastClickTime <= doubleClickTimeThreshold) {
                        FireDoubleClickEvent(tracker.Level);
                        tracker.LastClickTime = -100f; 
                    } else {
                        FireClickEvent(tracker.Level);
                        tracker.LastClickTime = Time.time; 
                    }
                }
            } else if (tracker.State == InteractionState.Dragging) {
                FireDragEvent(tracker.Level, InputManager.InputState.Ended);
            } else if (tracker.State == InteractionState.Holding) {
                FireHoldEvent(tracker.Level, InputManager.InputState.Ended);
            }

            // Pinch/Rotate cancels are handled in their own Process methods to support wheel/axis lifting
            if (tracker.State != InteractionState.Pinching && tracker.State != InteractionState.Rotating) {
                tracker.Reset();
            }
        }

        #endregion



        #region FIRE EVENTS

        // --- HELPER METHODS FOR FIRING EVENTS ---
        // TODO: put into InputManager
        private InputManager.InputData CreateData(InputManager.InputLevel level, InputManager.InputState state) {
            return new InputManager.InputData {
                Level = level,
                State = state,
                // Device = InputDeviceType.Touch, // no differentiation yet
                Position = m_pos,
                Delta = m_delta
                // could also add rotation? or utilize pos+delta for performance?
            };
        }

        private void FireClickEvent(InputManager.InputLevel level) {
            InputManager.InputData data = CreateData(level, InputManager.InputState.Ended);
            
            switch (EvaluationHelper.Instance.EvaluateOperationLayer(m_pos)){
                case EvaluationHelper.OperationLayer.UI2D:
                    manager.Publish(new InputManager.ClickUIEvent { Data = data });
                    break;
                case EvaluationHelper.OperationLayer.UI3D:
                case EvaluationHelper.OperationLayer.SCENEOBJECT:
                case EvaluationHelper.OperationLayer.OTHER:
                    manager.Publish(new InputManager.ClickOtherEvent { Data = data });
                    // possible further investigation for outcome "other"
                    // if (RayMeshUtility.GetHitPointPrecise(mainCam.ScreenPointToRay(m_pos), m_worldGameObjectWeHit, RayMeshUtility.Accuracy.ExactMesh, out m_worldHitPos)){
                    //     UnityHitVisualizerHelper.Spawn(m_worldHitPos, Color.green, 0.15f);
                    // }
                    break;
            }
        }

        private void FireDoubleClickEvent(InputManager.InputLevel level) {
            InputManager.InputData data = CreateData(level, InputManager.InputState.Ended);
            
            switch (EvaluationHelper.Instance.EvaluateOperationLayer(m_pos)){
                case EvaluationHelper.OperationLayer.UI2D:
                    manager.Publish(new InputManager.DoubleClickUIEvent { Data = data });
                    break;
                case EvaluationHelper.OperationLayer.UI3D:
                case EvaluationHelper.OperationLayer.SCENEOBJECT:
                case EvaluationHelper.OperationLayer.OTHER:
                    manager.Publish(new InputManager.DoubleClickOtherEvent { Data = data });
                    break;
            }
        }

        private void FireDragEvent(InputManager.InputLevel level, InputManager.InputState state) {
            InputManager.InputData data = CreateData(level, state);

            //Debug.Log("DRAG EVENT "+data.ToString());

            if(state == InputManager.InputState.Started) {
                layerDrag = EvaluationHelper.Instance.EvaluateOperationLayer(m_pos);
            }

            switch (layerDrag){
                case EvaluationHelper.OperationLayer.UI2D:
                    manager.Publish(new InputManager.DragUIEvent { Data = data });
                    break;
                case EvaluationHelper.OperationLayer.UI3D:
                case EvaluationHelper.OperationLayer.SCENEOBJECT:
                case EvaluationHelper.OperationLayer.OTHER:
                    manager.Publish(new InputManager.DragOtherEvent { Data = data });
                    break;
            }
        }

        private void FireHoldEvent(InputManager.InputLevel level, InputManager.InputState state) {
            InputManager.InputData data = CreateData(level, state);

            if(state == InputManager.InputState.Started) {
                layerHold = EvaluationHelper.Instance.EvaluateOperationLayer(m_pos);
            }

            switch (layerHold){
                case EvaluationHelper.OperationLayer.UI2D:
                    manager.Publish(new InputManager.HoldUIEvent { Data = data });
                    break;
                case EvaluationHelper.OperationLayer.UI3D:
                case EvaluationHelper.OperationLayer.SCENEOBJECT:
                case EvaluationHelper.OperationLayer.OTHER:
                    manager.Publish(new InputManager.HoldOtherEvent { Data = data });
                    break;
            }
        }

        private void FirePinchEvent(InputManager.InputLevel level, InputManager.InputState state, float pinchDelta) {
            InputManager.InputData data = CreateData(level, state);
            
            if(state == InputManager.InputState.Started) {
                layerPinch = EvaluationHelper.Instance.EvaluateOperationLayer(m_pos);
            }

            switch (layerPinch){
                case EvaluationHelper.OperationLayer.UI2D:
                    manager.Publish(new InputManager.PinchUIEvent { Data = data, PinchDistance = pinchDelta });
                    break;
                case EvaluationHelper.OperationLayer.UI3D:
                case EvaluationHelper.OperationLayer.SCENEOBJECT:
                case EvaluationHelper.OperationLayer.OTHER:
                    manager.Publish(new InputManager.PinchOtherEvent { Data = data, PinchDistance = pinchDelta });
                    break;
            }
        }

        private void FireRotateEvent(InputManager.InputLevel level, InputManager.InputState state, float rotateDelta) {
            InputManager.InputData data = CreateData(level, state);
            
            if(state == InputManager.InputState.Started) {
                layerRotate = EvaluationHelper.Instance.EvaluateOperationLayer(m_pos);
            }

            switch (layerRotate){
                case EvaluationHelper.OperationLayer.UI2D:
                    manager.Publish(new InputManager.TouchRotateUIEvent { Data = data, RotationAngle = rotateDelta });
                    break;
                case EvaluationHelper.OperationLayer.UI3D:
                case EvaluationHelper.OperationLayer.SCENEOBJECT:
                case EvaluationHelper.OperationLayer.OTHER:
                    manager.Publish(new InputManager.TouchRotateOtherEvent { Data = data, RotationAngle = rotateDelta });
                    break;
            }
        }

        #endregion

    }
}

#region RAY UTILITY
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
#endregion