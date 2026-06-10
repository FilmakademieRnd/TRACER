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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;  //one has to add this to "Assembly Definition Reference" in the "ModulesAssembly"

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

        //Interaction Thresholds
        private const float DragDistanceThreshold = 15f; 
        private const float ClickTimeThreshold = 0.3f;
        private const float HoldTimeThreshold = 0.4f;
        private const float DoubleClickTimeThreshold = 0.35f;

        //Multi-Touch Settings, Note: some values rely on UI/Screen scale
        private const float PinchDeadzone = 5f; // Pixels distance change to trigger pinch
        private const float RotateDeadzone = 2f; // Degrees change to trigger rotate

        private bool isMultiTouching = false;
        private float initialPinchDistance, previousPinchDistance, initialRotateAngle, previousRotateAngle;

        // TODO: put into InputManager & remove Unity dependency, so other modules could utilize it without referencing to other module
        // e.g. like the AttitudeModule!
        public class InputTracker{
            public InputManager.InputLevel Level;   //primary, secondary, tertiary
            public InteractionState State = InteractionState.Idle;  //see above
            public Vector2 CurrentPosition; //necessary for multitouch and for correct oop approach
            public Vector2 CurrentDelta;
            public float TimeDown;
            public Vector2 StartPosition;
            public float LastClickTime = -100f; // Tracked for Double Click

            public InputTracker(InputManager.InputLevel level){ Level = level; }
            public void Reset(){ State = InteractionState.Idle; CurrentDelta = Vector2.zero; }
        }

        private InputTracker _primary   = new InputTracker(InputManager.InputLevel.Primary);
        private InputTracker _secondary = new InputTracker(InputManager.InputLevel.Secondary);
        private InputTracker _tertiary  = new InputTracker(InputManager.InputLevel.Tertiary);

        //to request at start, but not ongoing!
        private EvaluationHelper.OperationLayer layerDrag, layerHold, layerPinch, layerRotate = EvaluationHelper.OperationLayer.OTHER;
        #endregion

        #region DEBUG VIZ VARS
        // UI State Tracking
        private GameObject mainUIContainer;
        private Sprite circleSprite;
        private Dictionary<InputManager.InputLevel, GameObject> evaluatingUIs = new Dictionary<InputManager.InputLevel, GameObject>();
        private Dictionary<InputManager.InputLevel, GameObject> activeHoldUIs = new Dictionary<InputManager.InputLevel, GameObject>();
        private Dictionary<InputManager.InputLevel, GameObject> activeDragUIs = new Dictionary<InputManager.InputLevel, GameObject>();
        private Dictionary<InputManager.InputLevel, GameObject> activePinchUIs = new Dictionary<InputManager.InputLevel, GameObject>();
        private Dictionary<InputManager.InputLevel, GameObject> activeRotateUIs = new Dictionary<InputManager.InputLevel, GameObject>();
        // Optional: Enable/Disable the debug text
        private const bool showDebugText = true;
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

            // --- PRIMARY (1-Finger / Left Mouse) ---
            m_inputs.VPETMap.OnPrimaryInputClick.started += ctx => OnPointerDown(_primary);
            m_inputs.VPETMap.OnPrimaryInputClick.canceled += ctx => OnPointerUp(_primary);

            // --- SECONDARY (2-Fingers / Right Mouse) ---
            m_inputs.VPETMap.OnSecondaryInputClick.started += ctx => OnPointerDown(_secondary);
            m_inputs.VPETMap.OnSecondaryInputClick.canceled += ctx => OnPointerUp(_secondary);

            // --- TERTIARY (3-Fingers / Middle Mouse) ---
            m_inputs.VPETMap.OnTertiaryInputClick.started += ctx => OnPointerDown(_tertiary);
            m_inputs.VPETMap.OnTertiaryInputClick.canceled += ctx => OnPointerUp(_tertiary);

            
            // --- GESTURES (Scrollwheel, Triggers, Touch Pinch/Rotate) ---
            m_inputs.VPETMap.OnPinch.started += ProcessPinchInput;
            m_inputs.VPETMap.OnPinch.performed += ProcessPinchInput;
            m_inputs.VPETMap.OnPinch.canceled += ProcessPinchInput;
            
            /*m_inputs.VPETMap.Rotate.performed += ProcessRotateInput;
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

            // --- PRIMARY (1-Finger / Left Mouse) ---
            m_inputs.VPETMap.OnPrimaryInputClick.started -= ctx => OnPointerDown(_primary);
            m_inputs.VPETMap.OnPrimaryInputClick.canceled -= ctx => OnPointerUp(_primary);

            m_inputs.VPETMap.OnSecondaryInputClick.started -= ctx => OnPointerDown(_secondary);
            m_inputs.VPETMap.OnSecondaryInputClick.canceled -= ctx => OnPointerUp(_secondary);

            m_inputs.VPETMap.OnTertiaryInputClick.started -= ctx => OnPointerDown(_tertiary);
            m_inputs.VPETMap.OnTertiaryInputClick.canceled -= ctx => OnPointerUp(_tertiary);

            m_inputs.VPETMap.OnPinch.started -= ProcessPinchInput;
            m_inputs.VPETMap.OnPinch.performed -= ProcessPinchInput;
            m_inputs.VPETMap.OnPinch.canceled -= ProcessPinchInput;
            
            /*m_inputs.VPETMap.Rotate.performed -= ProcessRotateInput;
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

        //! obsolete - the event driven approach is not good for continous values
        //! tracks the positions of our primary input (primary touch, mouse pos)
        //! and writes them into a buffer to allow further calculations (delta, speed, etc)
        //!
        private void ProcessPositionInput(){ 
            // Get the position
            Vector2 primaryPos = m_inputs.VPETMap.Position.ReadValue<Vector2>();
            UpdateTrackerPosition(_primary, primaryPos);

            // 2. Check if we are currently using a Touchscreen
            bool isTouch = Touchscreen.current != null && Input.touchCount > 0;//Touchscreen.current.touches.Count > 0;
            //[REVISE] remove Positiom [TouchScreen] from Unity Input Actions (?)

            if (isTouch) {  //Debug.Log("IS TOUCH!!!!!! "+Input.touchCount);
                // Touch fallback: Read exact finger positions
                if (Touchscreen.current.touches.Count > 1) {
                    Vector2 secPos = Touchscreen.current.touches[1].position.ReadValue();
                    UpdateTrackerPosition(_secondary, secPos);
                }
                
                if (Touchscreen.current.touches.Count > 2) {
                    Vector2 tertPos = Touchscreen.current.touches[2].position.ReadValue();
                    UpdateTrackerPosition(_tertiary, tertPos);
                }
            } else {
                // Mouse fallback: Secondary (Right Click) and Tertiary (Middle Click) 
                // physically share the same position as Primary on a PC.
                UpdateTrackerPosition(_secondary, primaryPos);
                UpdateTrackerPosition(_tertiary, primaryPos);
            }
        }

        // Helper to safely calculate deltas per-tracker
        private void UpdateTrackerPosition(InputTracker tracker, Vector2 newPos) {
            // Only calculate delta if the tracker is actively being used, otherwise keep it 0
            if (tracker.State != InteractionState.Idle) {
                tracker.CurrentDelta = newPos - tracker.CurrentPosition;
            } else {
                tracker.CurrentDelta = Vector2.zero;
            }
            tracker.CurrentPosition = newPos;
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
                Position = _primary.CurrentPosition,
                Delta = _primary.CurrentDelta
            };
            manager.Publish(new InputManager.AnyInputEvent { Data = anyInputData });
        }

        // --- THE UPDATE LOOP (finite state machine) ---
        //!
        //! Callback from TRACER _core when Unity calls it's render update
        //!
        private void OnCoreUpdateEvent(object sender, EventArgs e){
            
            ProcessPositionInput();

            ProcessMultiTouchGestures();

            ProcessTracker(_primary);
            ProcessTracker(_secondary);
            ProcessTracker(_tertiary);
        }

        private void ProcessMultiTouchGestures() {
            // 1. Exit early if we don't have exactly two active fingers
            if (_primary.State == InteractionState.Idle || _secondary.State == InteractionState.Idle) {
                isMultiTouching = false;
                return; 
            }

            // 2. FSM Safety Check: We can ONLY initiate a gesture if BOTH trackers are Evaluating.
            // If either tracker is already Dragging or Holding, we deny the multi-touch entirely.
            bool canStartGesture   = _primary.State == InteractionState.Evaluating && _secondary.State == InteractionState.Evaluating;
            bool isAlreadyPinching = _primary.State == InteractionState.Pinching;
            bool isAlreadyRotating = _primary.State == InteractionState.Rotating;

            if (!canStartGesture && !isAlreadyPinching && !isAlreadyRotating) {
                return; // Denied by FSM rules
            }

            // Get current tracking data
            Vector2 currentP1 = _primary.CurrentPosition; 
            Vector2 currentP2 = _secondary.CurrentPosition;
            float currentDistance = Vector2.Distance(currentP1, currentP2);
            Vector2 dir = currentP2 - currentP1;
            float currentAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector2 centerPos = (currentP1 + currentP2) / 2f;

            // 3. Initialization Phase (First frame the second finger touches)
            if (!isMultiTouching) {
                isMultiTouching = true;
                initialPinchDistance = currentDistance;
                previousPinchDistance = currentDistance;
                initialRotateAngle = currentAngle;
                previousRotateAngle = currentAngle;
                return; 
            }

            // 4. Handle Start Phase (Transitioning from Evaluating -> Pinching/Rotating)
            if (canStartGesture) {
                float distanceDeltaFromStart = Mathf.Abs(currentDistance - initialPinchDistance);
                float angleDeltaFromStart = Mathf.Abs(Mathf.DeltaAngle(initialRotateAngle, currentAngle));

                if (distanceDeltaFromStart > PinchDeadzone) {
                    // Lock states to prevent Drag/Hold/Click
                    _primary.State = InteractionState.Pinching;
                    _secondary.State = InteractionState.Pinching;
                    ClearPreviews(InputManager.InputLevel.Primary);
                    ClearPreviews(InputManager.InputLevel.Secondary);

                    float framePinchDelta = currentDistance - previousPinchDistance;
                    UpdatePinchActiveVisual(InputManager.InputLevel.Primary, centerPos, framePinchDelta);
                    FirePinchEvent(_primary, InputManager.InputState.Started, centerPos, framePinchDelta);
                }
                else if (angleDeltaFromStart > RotateDeadzone) {
                    // Lock states
                    _primary.State = InteractionState.Rotating;
                    _secondary.State = InteractionState.Rotating;
                    ClearPreviews(InputManager.InputLevel.Primary);
                    ClearPreviews(InputManager.InputLevel.Secondary);

                    float frameAngleDelta = Mathf.DeltaAngle(previousRotateAngle, currentAngle);
                    UpdateRotateActiveVisual(InputManager.InputLevel.Primary, centerPos, currentAngle);
                    FireRotateEvent(_primary, InputManager.InputState.Started, centerPos, frameAngleDelta);
                }
            }
            // 5. Handle Ongoing Phase
            else if (isAlreadyPinching) {
                float framePinchDelta = currentDistance - previousPinchDistance;
                if (Mathf.Abs(framePinchDelta) > 0.01f) {
                    UpdatePinchActiveVisual(InputManager.InputLevel.Primary, centerPos, framePinchDelta);
                    FirePinchEvent(_primary, InputManager.InputState.Ongoing, centerPos, framePinchDelta);
                }
            }
            else if (isAlreadyRotating) {
                float frameAngleDelta = Mathf.DeltaAngle(previousRotateAngle, currentAngle);
                if (Mathf.Abs(frameAngleDelta) > 0.01f) {
                    UpdateRotateActiveVisual(InputManager.InputLevel.Primary, centerPos, currentAngle);
                    FireRotateEvent(_primary, InputManager.InputState.Ongoing, centerPos, frameAngleDelta);
                }
            }

            // Store for the next frame
            previousPinchDistance = currentDistance;
            previousRotateAngle = currentAngle;
        }

        private void ProcessTracker(InputTracker tracker) {
            if (tracker.State == InteractionState.Idle || tracker.State == InteractionState.Pinching || tracker.State == InteractionState.Rotating) { return; }

            if (tracker.State == InteractionState.Dragging) {
                FireDragEvent(tracker, InputManager.InputState.Ongoing);
                UpdateDragActiveVisual(tracker.Level, tracker.CurrentPosition);
                return;
            }
            
            //deny hold option for sec & tert?
            if (tracker.State == InteractionState.Holding) {
                FireHoldEvent(tracker, InputManager.InputState.Ongoing);

                UpdateHoldActiveVisual(tracker.Level, tracker.StartPosition, tracker.CurrentPosition);
                return;
            }

            if (tracker.State == InteractionState.Evaluating) {
                
                UpdateEvaluatingVisual(tracker.Level, tracker.StartPosition, tracker.CurrentPosition, tracker.TimeDown);

                float distanceFromStart = Vector2.Distance(tracker.StartPosition, tracker.CurrentPosition);
                float timeHeld = Time.time - tracker.TimeDown;

                // Distance overrides Time (Drag denies Hold)
                if (distanceFromStart > DragDistanceThreshold) {
                    tracker.State = InteractionState.Dragging;
                    FireDragEvent(tracker, InputManager.InputState.Started);

                    ClearPreviews(tracker.Level); // Remove circle/rect
                } 
                // Time overrides Distance (Hold denies Drag)
                else if (timeHeld > HoldTimeThreshold) {
                    tracker.State = InteractionState.Holding;
                    FireHoldEvent(tracker, InputManager.InputState.Started);
                }
            }
        }

        private void ProcessPinchInput(InputAction.CallbackContext ctx) {
            //this is only called via scroll-wheel / specific input event
            //thats why we handle start, ongoing, end here
            //OnPointerUp is not called via Scrollwheel (specific buttons may have to handle it otherwise!)
            float pinchDelta = ctx.ReadValue<float>();
            
            // Deadzone check (but what about moving position (without necessary delta))
            if (Mathf.Abs(pinchDelta) < 0.01f && ctx.phase != InputActionPhase.Canceled) { return; }

            // Example: Map 2-finger pinch to Primary. (Adjust if your logic maps it to Secondary)
            InputTracker tracker = _primary; 

            if (ctx.phase == InputActionPhase.Canceled && tracker.State == InteractionState.Pinching) {
                //implement to use only when not using Touches/Buttons (no axis!)
                FirePinchEvent(tracker, InputManager.InputState.Ended, tracker.CurrentPosition, pinchDelta);
                tracker.Reset();
                ClearPreviews(tracker.Level);
            } else {
                if (tracker.State == InteractionState.Evaluating || tracker.State == InteractionState.Dragging || tracker.State == InteractionState.Idle) {
                    tracker.State = InteractionState.Pinching;
                    FirePinchEvent(tracker, InputManager.InputState.Started, tracker.CurrentPosition, pinchDelta);
                } else if (tracker.State == InteractionState.Pinching) {
                    FirePinchEvent(tracker, InputManager.InputState.Ongoing, tracker.CurrentPosition, pinchDelta);

                    UpdatePinchActiveVisual(tracker.Level, tracker.CurrentPosition, pinchDelta);
                }
            }
        }

        private void ProcessRotateInput(InputAction.CallbackContext ctx) {
             //this is only called via scroll-wheel / specific input event
            //thats why we handle start, ongoing here BUT have to 
            //handle the end state within OnPointerUp (removed cancel-listener)
            float rotateDelta = ctx.ReadValue<float>();
            
            if (Mathf.Abs(rotateDelta) < 0.01f && ctx.phase != InputActionPhase.Canceled) { return; }

            InputTracker tracker = _primary; // Map to primary or secondary depending on your scheme

            if (ctx.phase == InputActionPhase.Canceled && tracker.State == InteractionState.Rotating) {
                //implement to use only when not using Touches/Buttons (no axis!)
                FireRotateEvent(tracker, InputManager.InputState.Ended, tracker.CurrentPosition, rotateDelta);
                tracker.Reset();
                ClearPreviews(tracker.Level);
            } else {
                if (tracker.State == InteractionState.Evaluating || tracker.State == InteractionState.Dragging || tracker.State == InteractionState.Idle) {
                    tracker.State = InteractionState.Rotating;
                    FireRotateEvent(tracker, InputManager.InputState.Started, tracker.CurrentPosition,  rotateDelta);
                } else if (tracker.State == InteractionState.Rotating) {
                    FireRotateEvent(tracker, InputManager.InputState.Ongoing, tracker.CurrentPosition,  rotateDelta);
                    
                    UpdateRotateActiveVisual(tracker.Level, tracker.CurrentPosition, rotateDelta);
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
            tracker.StartPosition = tracker.CurrentPosition;
        }

        private void OnPointerUp(InputTracker tracker) {
            if (tracker.State == InteractionState.Idle) { return; }

            if (tracker.State == InteractionState.Evaluating) {
                float duration = Time.time - tracker.TimeDown;
                
                if (duration <= ClickTimeThreshold) {
                    if (Time.time - tracker.LastClickTime <= DoubleClickTimeThreshold) {
                        FireDoubleClickEvent(tracker);
                        tracker.LastClickTime = -100f; 
                    } else {
                        FireClickEvent(tracker);
                        tracker.LastClickTime = Time.time; 
                    }
                }
            } else if (tracker.State == InteractionState.Dragging) {
                FireDragEvent(tracker, InputManager.InputState.Ended);
            } else if (tracker.State == InteractionState.Holding) {
                FireHoldEvent(tracker, InputManager.InputState.Ended);
            }else if (tracker.State == InteractionState.Pinching) {
                FirePinchEvent(tracker, InputManager.InputState.Ended, tracker.CurrentPosition, 0f);
            }else if (tracker.State == InteractionState.Rotating) {
                FireRotateEvent(tracker, InputManager.InputState.Ended, tracker.CurrentPosition, 0f);
            }

            // Pinch/Rotate cancels are handled in their own Process methods to support wheel/axis lifting
            //if (tracker.State != InteractionState.Pinching && tracker.State != InteractionState.Rotating) {
            tracker.Reset();
            ClearPreviews(tracker.Level);
            //}
        }

        #endregion



        #region FIRE EVENTS

        // --- HELPER METHODS FOR FIRING EVENTS ---
        // TODO: put into InputManager
        private InputManager.InputData CreateData(InputTracker tracker, InputManager.InputState state) {
            return new InputManager.InputData {
                Level = tracker.Level,
                State = state,
                // Device = InputDeviceType.Touch, // no differentiation yet
                Position = tracker.CurrentPosition,
                Delta = tracker.CurrentDelta
                // could also add rotation? or utilize pos+delta for performance?
            };
        }

        private void FireClickEvent(InputTracker tracker) {
            InputManager.InputData data = CreateData(tracker, InputManager.InputState.Ended);
            
            switch (EvaluationHelper.Instance.EvaluateOperationLayer(tracker.CurrentPosition)){
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

            SpawnClickVisual(tracker.Level, tracker.CurrentPosition, isDouble: false);
        }

        private void FireDoubleClickEvent(InputTracker tracker) {
            InputManager.InputData data = CreateData(tracker, InputManager.InputState.Ended);
            
            switch (EvaluationHelper.Instance.EvaluateOperationLayer(tracker.CurrentPosition)){
                case EvaluationHelper.OperationLayer.UI2D:
                    manager.Publish(new InputManager.DoubleClickUIEvent { Data = data });
                    break;
                case EvaluationHelper.OperationLayer.UI3D:
                case EvaluationHelper.OperationLayer.SCENEOBJECT:
                case EvaluationHelper.OperationLayer.OTHER:
                    manager.Publish(new InputManager.DoubleClickOtherEvent { Data = data });
                    break;
            }
            SpawnClickVisual(tracker.Level, tracker.CurrentPosition, isDouble: true);
        }

        private void FireDragEvent(InputTracker tracker, InputManager.InputState state) {
            InputManager.InputData data = CreateData(tracker, state);

            //[!REVISE] do we need to have "initial click pos"? (for evaluation for the correct thing we hit - that we want to drag)
            //Debug.Log("DRAG EVENT "+data.ToString());

            if(state == InputManager.InputState.Started) {
                layerDrag = EvaluationHelper.Instance.EvaluateOperationLayer(tracker.CurrentPosition);
            }

            switch (layerDrag){
                case EvaluationHelper.OperationLayer.UI2D:
                    manager.Publish(new InputManager.DragUIEvent { Data = data, StartPos = tracker.StartPosition });
                    break;
                case EvaluationHelper.OperationLayer.UI3D:
                case EvaluationHelper.OperationLayer.SCENEOBJECT:
                case EvaluationHelper.OperationLayer.OTHER:
                    manager.Publish(new InputManager.DragOtherEvent { Data = data, StartPos = tracker.StartPosition });
                    break;
            }
        }

        private void FireHoldEvent(InputTracker tracker, InputManager.InputState state) {
            InputManager.InputData data = CreateData(tracker, state);

            if(state == InputManager.InputState.Started) {
                layerHold = EvaluationHelper.Instance.EvaluateOperationLayer(tracker.CurrentPosition);
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

        private void FirePinchEvent(InputTracker tracker, InputManager.InputState state, Vector2 centerPos, float pinchDelta) {
            InputManager.InputData data = CreateData(tracker, state);
            data.Position = centerPos;
            
            if(state == InputManager.InputState.Started) {
                layerPinch = EvaluationHelper.Instance.EvaluateOperationLayer(centerPos);
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

        private void FireRotateEvent(InputTracker tracker, InputManager.InputState state, Vector2 centerPos, float rotateDelta) {
            InputManager.InputData data = CreateData(tracker, state);
            data.Position = centerPos;
            
            if(state == InputManager.InputState.Started) {
                layerRotate = EvaluationHelper.Instance.EvaluateOperationLayer(centerPos);
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

        #region UI DEBUGGING
        // --- ONE-SHOT VISUALS (Click & Text) ---
        private void SpawnClickVisual(InputManager.InputLevel level, Vector2 pos, bool isDouble){
            EnsureMainCanvasExists();

            // Spawn Text
            if (showDebugText){
                string text = $"{level} {(isDouble ? "Double-Click" : "Click")}";
                core.StartCoroutine(AnimateFloatingText(text, pos + new Vector2(0, 60f)));
            }

            // Spawn Circles
            if (isDouble){
                SpawnWobbleCircle(pos + new Vector2(-20f, -20));
                SpawnWobbleCircle(pos + new Vector2(20f, -20));
            }else
                SpawnWobbleCircle(pos);
            
        }

        private void SpawnWobbleCircle(Vector2 pos){
            GameObject circleGO = new GameObject("ClickCircle");
            circleGO.transform.SetParent(mainUIContainer.transform);
            Image img = circleGO.AddComponent<Image>();
            img.sprite = GetOrCreateCircleSprite(); // Your existing procedural circle
            img.rectTransform.position = pos;
            img.rectTransform.sizeDelta = new Vector2(30f, 30f);

            core.StartCoroutine(AnimateWobble(img.rectTransform));
        }

        private IEnumerator AnimateWobble(RectTransform rect){
            float duration = 0.6f;
            float elapsed = 0f;

            while (elapsed < duration){
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Fast scale in with overshoot, then wobble (sin wave decaying over time), then scale to 0
                float popIn = EaseOutBack(Mathf.Clamp01(t * 3f)); // Fast pop in (0 to 0.33 of duration)
                float wobble = Mathf.Sin(t * Mathf.PI * 6f) * 0.3f * (1f - t); // 3 full wobbles, decaying amplitude
                float scaleOut = 1f - Mathf.Pow(Mathf.Clamp01((t - 0.7f) * 3.33f), 2f); // Scale down at the end

                float finalScale = (popIn + wobble) * scaleOut;
                rect.localScale = Vector3.one * Mathf.Max(0, finalScale);
                
                yield return null;
            }
            UnityEngine.GameObject.Destroy(rect.gameObject);
        }

        private IEnumerator AnimateFloatingText(string message, Vector2 startPos){
            GameObject textGO = new GameObject("InputText");
            textGO.transform.SetParent(mainUIContainer.transform);
            
            Text txt = textGO.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // Default Unity font fallback
            txt.text = message;
            txt.fontSize = 24;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.rectTransform.position = startPos;
            
            // Add shadow for readability
            Outline outline = textGO.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1, -1);

            float duration = 1.0f;
            float elapsed = 0f;

            while (elapsed < duration){
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Float up and fade out
                txt.rectTransform.position = startPos + new Vector2(0, t * 50f);
                txt.color = new Color(1f, 1f, 1f, 1f - Mathf.Pow(t, 2f));
                outline.effectColor = new Color(0, 0, 0, 1f - Mathf.Pow(t, 2f));
                
                yield return null;
            }
            UnityEngine.GameObject.Destroy(textGO);
        }

        // --- CONTINUOUS VISUALS (Evaluating Previews & Active Hold Line) ---
        private void UpdateEvaluatingVisual(InputManager.InputLevel level, Vector2 startPos, Vector2 currentPos, float timeDown){
            EnsureMainCanvasExists();

            if (!evaluatingUIs.TryGetValue(level, out GameObject container) || container == null){
                container = new GameObject($"EvaluatingUI_{level}");
                container.transform.SetParent(mainUIContainer.transform);
                
                // Setup Drag Rect Preview
                GameObject rectGO = new GameObject("DragRect");
                rectGO.transform.SetParent(container.transform);
                Image rectImg = rectGO.AddComponent<Image>();
                rectImg.color = new Color(1f, 1f, 1f, 0f); // Start transparent
                rectImg.rectTransform.position = startPos;
                rectImg.rectTransform.sizeDelta = new Vector2(DragDistanceThreshold * 2f, DragDistanceThreshold * 2f);

                // Setup Hold Fill Circle
                GameObject circleGO = new GameObject("HoldFillCircle");
                circleGO.transform.SetParent(container.transform);
                Image circleImg = circleGO.AddComponent<Image>();
                circleImg.sprite = GetOrCreateCircleSprite();
                circleImg.type = Image.Type.Filled;
                circleImg.fillMethod = Image.FillMethod.Radial360;
                circleImg.fillAmount = 0f;
                circleImg.color = new Color(1f, 0.8f, 0.2f, 0.8f); // Yellowish
                circleImg.rectTransform.position = startPos;
                circleImg.rectTransform.sizeDelta = new Vector2(40f, 40f);

                evaluatingUIs[level] = container;
            }

            // Update Values
            float distance = Vector2.Distance(startPos, currentPos);
            float holdProgress = (Time.time - timeDown) / HoldTimeThreshold;
            float dragProgress = distance / DragDistanceThreshold;

            Image fillCircle = container.transform.GetChild(1).GetComponent<Image>();
            Image dragRect = container.transform.GetChild(0).GetComponent<Image>();

            fillCircle.fillAmount = Mathf.Clamp01(holdProgress);
            dragRect.color = new Color(1f, 1f, 1f, Mathf.Clamp01(dragProgress) * 0.5f); // Max 50% opacity
        }

        private void UpdateHoldActiveVisual(InputManager.InputLevel level, Vector2 startPos, Vector2 currentPos){
            EnsureMainCanvasExists();

            if (!activeHoldUIs.TryGetValue(level, out GameObject container) || container == null){
                container = new GameObject($"HoldLineUI_{level}");
                container.transform.SetParent(mainUIContainer.transform);

                // Center dot
                GameObject dotGO = new GameObject("HoldStartDot");
                dotGO.transform.SetParent(container.transform);
                Image dotImg = dotGO.AddComponent<Image>();
                dotImg.sprite = GetOrCreateCircleSprite();
                dotImg.color = Color.green;
                dotImg.rectTransform.position = startPos;
                dotImg.rectTransform.sizeDelta = new Vector2(20f, 20f);

                // Connecting Line
                GameObject lineGO = new GameObject("HoldLine");
                lineGO.transform.SetParent(container.transform);
                Image lineImg = lineGO.AddComponent<Image>();
                lineImg.color = new Color(0f, 1f, 0f, 0.5f); // Semi-transparent green
                
                // Pivot at the left center so stretching scales it forward
                lineImg.rectTransform.pivot = new Vector2(0f, 0.5f); 
                lineImg.rectTransform.position = startPos;

                activeHoldUIs[level] = container;

                if (showDebugText)
                    core.StartCoroutine(AnimateFloatingText($"{level} Hold", startPos + new Vector2(0, 40f)));
            }

            // Math to stretch and rotate the line towards the current finger position
            RectTransform lineRect = container.transform.GetChild(1).GetComponent<RectTransform>();
            Vector2 dir = currentPos - startPos;
            float distance = dir.magnitude;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            lineRect.sizeDelta = new Vector2(distance, 4f); // 4px thick line
            lineRect.rotation = Quaternion.Euler(0, 0, angle);
        }

        private void UpdateDragActiveVisual(InputManager.InputLevel level, Vector2 currentPos) {
            EnsureMainCanvasExists();

            if (!activeDragUIs.TryGetValue(level, out GameObject container) || container == null) {
                container = new GameObject($"DragActiveUI_{level}");
                container.transform.SetParent(mainUIContainer.transform);

                // Persistent solid rectangle following the finger during the drag
                GameObject rectGO = new GameObject("ActiveDragRect");
                rectGO.transform.SetParent(container.transform);
                Image rectImg = rectGO.AddComponent<Image>();
                rectImg.color = new Color(1f, 1f, 1f, 0.4f); // 40% opacity white
                rectImg.rectTransform.sizeDelta = new Vector2(DragDistanceThreshold * 2f, DragDistanceThreshold * 2f);

                activeDragUIs[level] = container;

                if (showDebugText) {
                    core.StartCoroutine(AnimateFloatingText($"{level} Drag", currentPos + new Vector2(0, 50f)));
                }
            }

            // Update the position every frame
            RectTransform activeRect = container.transform.GetChild(0).GetComponent<RectTransform>();
            activeRect.position = currentPos;
        }

        private void UpdatePinchActiveVisual(InputManager.InputLevel level, Vector2 centerPos, float pinchValue) {
            EnsureMainCanvasExists();

            if (!activePinchUIs.TryGetValue(level, out GameObject container) || container == null) {
                container = new GameObject($"PinchUI_{level}");
                container.transform.SetParent(mainUIContainer.transform);

                GameObject circleGO = new GameObject("PinchCircle");
                circleGO.transform.SetParent(container.transform);
                Image circleImg = circleGO.AddComponent<Image>();
                circleImg.sprite = GetOrCreateCircleSprite();
                circleImg.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange transparent
                circleImg.rectTransform.position = centerPos;
                
                activePinchUIs[level] = container;

                if (showDebugText) {
                    core.StartCoroutine(AnimateFloatingText($"{level} Pinch", centerPos + new Vector2(0, 60f)));
                }
            }
            //if we use the scroll-wheel, this will flicker!

            // Scale the circle dynamically based on pinch value (adjust multiplier as needed for your data)
            RectTransform circleRect = container.transform.GetChild(0).GetComponent<RectTransform>();
            circleRect.position = centerPos;
            float dynamicSize = 80f;// + (pinchValue * 20f); 
            circleRect.sizeDelta = new Vector2(dynamicSize, dynamicSize);
        }

        private void UpdateRotateActiveVisual(InputManager.InputLevel level, Vector2 centerPos, float rotationAngle) {
            EnsureMainCanvasExists();

            if (!activeRotateUIs.TryGetValue(level, out GameObject container) || container == null) {
                container = new GameObject($"RotateUI_{level}");
                container.transform.SetParent(mainUIContainer.transform);

                // A line across the center indicating the rotation angle
                GameObject lineGO = new GameObject("RotateLine");
                lineGO.transform.SetParent(container.transform);
                Image lineImg = lineGO.AddComponent<Image>();
                lineImg.color = new Color(0.8f, 0.2f, 1f, 0.8f); // Purple
                lineImg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                lineImg.rectTransform.sizeDelta = new Vector2(120f, 4f); 
                lineImg.rectTransform.position = centerPos;

                activeRotateUIs[level] = container;

                if (showDebugText) {
                    core.StartCoroutine(AnimateFloatingText($"{level} Rotate", centerPos + new Vector2(0, 60f)));
                }
            }

            // Rotate the visual based on the angle
            RectTransform lineRect = container.transform.GetChild(0).GetComponent<RectTransform>();
            lineRect.position = centerPos;
            lineRect.rotation = Quaternion.Euler(0, 0, rotationAngle);
        }

        // Clears specific continuous previews
        private void ClearPreviews(InputManager.InputLevel level) {
            if (evaluatingUIs.TryGetValue(level, out GameObject evalUI) && evalUI != null) {
                UnityEngine.GameObject.Destroy(evalUI);
            }
            if (activeHoldUIs.TryGetValue(level, out GameObject holdUI) && holdUI != null) {
                UnityEngine.GameObject.Destroy(holdUI);
            }
            if (activeDragUIs.TryGetValue(level, out GameObject dragUI) && dragUI != null) {
                UnityEngine.GameObject.Destroy(dragUI);
            }
            if (activePinchUIs.TryGetValue(level, out GameObject pinchUI) && pinchUI != null) {
                UnityEngine.GameObject.Destroy(pinchUI);
            }
            if (activeRotateUIs.TryGetValue(level, out GameObject rotateUI) && rotateUI != null) {
                UnityEngine.GameObject.Destroy(rotateUI);
            }
        }

        // Ensures we always have a canvas to draw on
        private void EnsureMainCanvasExists(){
            if (mainUIContainer != null) return;
            mainUIContainer = new GameObject("InputModuleUI");
            Canvas canvas = mainUIContainer.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
        }

        // --- PROCEDURAL SPRITE GENERATION ---
        private Sprite GetOrCreateCircleSprite(){
            if (circleSprite != null) return circleSprite;

            int resolution = 128; // 128x128 is a good balance for UI crispness vs generation speed
            Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            Color[] colors = new Color[resolution * resolution];
            
            float center = resolution / 2f;
            float radius = resolution / 2f;

            for (int y = 0; y < resolution; y++){
                for (int x = 0; x < resolution; x++){
                    // Calculate distance from center (with 0.5f offset for pixel center)
                    float dx = (x + 0.5f) - center;
                    float dy = (y + 0.5f) - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // Anti-aliasing math: smooth fade over 1 pixel at the edge
                    float alpha = Mathf.Clamp01(radius - distance);
                    
                    colors[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply(); // Upload to GPU

            // Create the sprite and pivot at center
            circleSprite = Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), 100f);
            return circleSprite;
        }

        // --- MATH HELPERS ---
        private float EaseOutBack(float x){
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
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