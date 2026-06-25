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

        //!
        //! Interaction Rules
        //! If false, only one tracker can be active (Drag/Hold) at a time
        //!
        private bool allowSimultaneousInteractions = false;

        //Interaction Thresholds
        private readonly float[] DragDistanceThreshold = {30f, 20f, 10f};       //in pixels!
        private readonly float[] HoldTimeThreshold = {0.45f, 0.4f, 0.35f};      //in seconds
        private const float DoubleClickTimeThreshold = 0.35f;
        // Add a tiny time buffer (e.g., 0.06 seconds). 
        // This forces the FSM to wait 60ms before committing to a 1-finger drag, giving the user time to place their 2nd or 3rd finger!
        private readonly float[] TouchTimeGracePeriod = {0.08f, 0.04f, 0.01f};

        //Multi-Touch Settings, Note: some values rely on UI/Screen scale
        private const float PinchDeadzone = 5f; // Pixels distance change to trigger pinch
        private const float RotateDeadzone = 5f; // Degrees change to rotate
        // Dot product threshold (0.5 to 0.9). Higher means fingers must move more perfectly parallel to trigger a Two-Finger Drag instead of a Pinch
        private const float ParallelDotThreshold = 0.5f; 

        private float GetDragThreshold(int fingerCount) { return DragDistanceThreshold[Mathf.Clamp(fingerCount - 1, 0, 2)];}
        private float GetHoldThreshold(int fingerCount) { return HoldTimeThreshold[Mathf.Clamp(fingerCount - 1, 0, 2)]; }
        private float GetGracePeriod(int fingerCount) { return TouchTimeGracePeriod[Mathf.Clamp(fingerCount - 1, 0, 2)]; }

        //fixing of scroll input action 'canceled' gets swallowed!
        private float scrollTimeout = 0.15f; // The delay before a scroll is considered "Ended"
        private bool _isMouseScrolling = false;
        private float _lastScrollTime = 0f;

        private InputManager.InputTracker _primary   = new InputManager.InputTracker(InputManager.InputLevel.Primary);
        private InputManager.InputTracker _secondary = new InputManager.InputTracker(InputManager.InputLevel.Secondary);
        private InputManager.InputTracker _tertiary  = new InputManager.InputTracker(InputManager.InputLevel.Tertiary);

        //to request at start, but not ongoing!
        private EvaluationHelper.OperationLayer layerDrag, layerHold, layerPinch, layerRotate = EvaluationHelper.OperationLayer.OTHER;
        #endregion

        #region TRACKER HELPERS
        // Groups trackers, assigns the state, makes the highest level the Leader, and mutes the rest.
        private void ExecuteGroupGesture(InputManager.InteractionState state, params InputManager.InputTracker[] group) {
            InputManager.InputTracker leader = group[group.Length - 1]; // Assumes array is ordered lowest to highest

            foreach (var t in group) {
                t.State = state;
                ClearPreviews(t.Level);
                if (t != leader) {
                    t.IsMuted = true;
                }
            }

            Vector2 center = GetSharedCenter(state);
            Vector2 delta = GetSharedDelta(state);

            // Leader fires the Start event
            if (state == InputManager.InteractionState.Dragging) {
                FireDragEvent(leader, InputManager.InputState.Started, center, delta);
                // Start the visual immediately
                UpdateDragActiveVisual(leader.Level, center); 
            } else if (state == InputManager.InteractionState.Holding) {
                FireHoldEvent(leader, InputManager.InputState.Started, center);
                UpdateHoldActiveVisual(leader.Level, leader.StartPosition, center);
            }
            // Pinch/Rotate start events are handled in the Orchestrator as they require custom deltas
        }

        // Dynamically averages the positions of ALL trackers sharing the exact same state
        private Vector2 GetSharedCenter(InputManager.InteractionState state) {
            Vector2 sum = Vector2.zero; 
            int count = 0;
            if (_primary.State == state) { sum += _primary.CurrentPosition; count++; }
            if (_secondary.State == state) { sum += _secondary.CurrentPosition; count++; }
            if (_tertiary.State == state) { sum += _tertiary.CurrentPosition; count++; }
            return count > 0 ? sum / count : Vector2.zero;
        }

        private Vector2 GetSharedDelta(InputManager.InteractionState state) {
            Vector2 sum = Vector2.zero; 
            int count = 0;
            if (_primary.State == state) { sum += _primary.CurrentDelta; count++; }
            if (_secondary.State == state) { sum += _secondary.CurrentDelta; count++; }
            if (_tertiary.State == state) { sum += _tertiary.CurrentDelta; count++; }
            return count > 0 ? sum / count : Vector2.zero;
        }
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
            
            /*m_inputs.VPETMap.Rotate.performed += ProcessRotateInput;
            m_inputs.VPETMap.Rotate.canceled += ProcessRotateInput;
            */

            m_inputs.VPETMap.Enable();

            EnhancedTouchSupport.Enable();
        }

        //!
        //! setup the unity input action via code
        //!
        private void SetupAnyInputAction() {
            // anyInputAction = new InputAction(type: InputActionType.Button);
            // //error on Android!
            // anyInputAction.AddBinding("/*/<button>");       // 1. Catch every keyboard key, gamepad button, or joystick button
            // anyInputAction.AddBinding("<Pointer>/press");   // 2. Catch mouse clicks, pen taps, and touchscreen presses
            // //maybe also add joystick/mouse movement?
            // anyInputAction.Enable();                        // The action must be enabled to start listening to the hardware

            InputAction anyAction = new InputAction("AnyInput", InputActionType.Button);
            // 1. Catches any touch on the screen (or mouse click/pen tap)
            anyAction.AddBinding("<Pointer>/press");

            // 2. Catches any button on a Bluetooth/USB Gamepad
            anyAction.AddBinding("<Gamepad>/<button>");

            // 3. Catches any physical keyboard key connected via USB/Bluetooth
            anyAction.AddBinding("<Keyboard>/anyKey");

            // Subscribe to your events
            anyAction.performed += ProcessAnyInput;
            
            anyAction.Enable();
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
            //https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/Touch.html
            // You should not use Touchscreen for polling. If you want to read out touches similar to UnityEngine.Input.touches, see EnhancedTouch. 
            UpdateTrackerPosition(_primary,     GetCurrentPos(InputManager.InputLevel.Primary));
            UpdateTrackerPosition(_secondary,   GetCurrentPos(InputManager.InputLevel.Secondary));
            UpdateTrackerPosition(_tertiary,    GetCurrentPos(InputManager.InputLevel.Tertiary));
        }
        // Helper to safely calculate deltas per-tracker
        private void UpdateTrackerPosition(InputManager.InputTracker tracker, Vector2 newPos) {
            // Only calculate delta if the tracker is actively being used, otherwise keep it 0
            if (tracker.State != InputManager.InteractionState.Idle) {
                tracker.CurrentDelta = newPos - tracker.CurrentPosition;
            } else {
                tracker.CurrentDelta = Vector2.zero;
            }
            tracker.CurrentPosition = newPos;
        }

        private bool IsTouch(out int nrOfTouches){
            nrOfTouches = UnityEngine.InputSystem.Touchscreen.current != null ? UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count : 0;
            bool isTouch = nrOfTouches > 0;
            return isTouch;
        }

        private Vector2 GetCurrentPos(InputManager.InputLevel level) {
            if (IsTouch(out int touchesWeHave)) {
                // Return the specific finger's position based on the level
                switch (level) {
                    case InputManager.InputLevel.Primary:
                        return UnityEngine.InputSystem.Touchscreen.current.touches[0].position.ReadValue();
                    case InputManager.InputLevel.Secondary:
                        if (UnityEngine.InputSystem.Touchscreen.current.touches.Count > 1)
                            return UnityEngine.InputSystem.Touchscreen.current.touches[1].position.ReadValue();
                        break;
                    case InputManager.InputLevel.Tertiary:
                        if (UnityEngine.InputSystem.Touchscreen.current.touches.Count > 2)
                            return UnityEngine.InputSystem.Touchscreen.current.touches[2].position.ReadValue();
                        break;
                }
            }
            
            // Mouse fallback: All levels share the primary pointer position
            return m_inputs.VPETMap.Position.ReadValue<Vector2>();
        }


        //!
        //! Check (if option to not allow multi tracker use) which one we use right now
        //!
        private bool IsAnyOtherTrackerActive(InputManager.InputTracker excludeTracker) {
            if (_primary    != excludeTracker && _primary.State     >= InputManager.InteractionState.Dragging) return true;
            if (_secondary  != excludeTracker && _secondary.State   >= InputManager.InteractionState.Dragging) return true;
            if (_tertiary   != excludeTracker && _tertiary.State    >= InputManager.InteractionState.Dragging) return true;
            return false;
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

        //!
        //! verifying that scroll input via the mouse wheel
        //! relying on the started, ongoing, canceled action was not trustworthy
        //!
        private void ProcessScrollInput() {
            // 1. Read the raw value directly
            float scrollDelta = m_inputs.VPETMap.OnPinch.ReadValue<float>();

            // 2. IS ACTIVELY SCROLLING
            if (Mathf.Abs(scrollDelta) > 0.01f) {
                Vector2 mousePos = GetCurrentPos(InputManager.InputLevel.Primary);

                float scrollDeltaAdjusted = VerifyPersistentScrollSpeed(scrollDelta * scrollWheelSensitivity);

                if (!_isMouseScrolling) {
                    // --- STARTED PHASE ---
                    _isMouseScrolling = true;
                    _secondary.State = InputManager.InteractionState.Pinching; // Lock state so right-clicks are ignored
                    
                    UpdatePinchActiveVisual(_tertiary.Level, mousePos, scrollDeltaAdjusted);
                    FirePinchEvent(_tertiary, InputManager.InputState.Started, mousePos, scrollDeltaAdjusted);
                } else {
                    // --- ONGOING PHASE ---
                    UpdatePinchActiveVisual(_tertiary.Level, mousePos, scrollDeltaAdjusted);
                    FirePinchEvent(_tertiary, InputManager.InputState.Ongoing, mousePos, scrollDeltaAdjusted);
                }

                // Reset the timeout timer every frame the wheel is moving
                _lastScrollTime = Time.time;
            }
            // 3. STOPPED SCROLLING (EVALUATING TIMEOUT)
            else if (_isMouseScrolling) {
                if (Time.time - _lastScrollTime > scrollTimeout) {
                    // --- ENDED PHASE ---
                    _isMouseScrolling = false;
                    _secondary.Reset(); // Unlock the tracker
                    
                    Vector2 mousePos = GetCurrentPos(InputManager.InputLevel.Primary);
                    FirePinchEvent(_tertiary, InputManager.InputState.Ended, mousePos, 0f);
                    ClearPreviews(_tertiary.Level);
                }
            }
            
            // 4. IDLE
            // If _isMouseScrolling is false AND scrollDelta is 0, the function just exits.
            // Zero unnecessary logic is executed.
        }

        // --- THE UPDATE LOOP (finite state machine) ---
        //!
        //! Callback from TRACER _core when Unity calls it's render update
        //!
        private void OnCoreUpdateEvent(object sender, EventArgs e){
            
            ProcessPositionInput();

            ProcessMultiTouchGestures();

            ProcessScrollInput();

            ProcessTracker(_primary);
            ProcessTracker(_secondary);
            ProcessTracker(_tertiary);
        }

        private void ProcessMultiTouchGestures() {
            // 1. Exit early
            if (!IsTouch(out int nrOfTouches) || nrOfTouches <= 1) 
                return; 

            bool pEval = _primary.State == InputManager.InteractionState.Evaluating   && !_primary.IsMuted;
            bool sEval = _secondary.State == InputManager.InteractionState.Evaluating && !_secondary.IsMuted;
            bool tEval = _tertiary.State == InputManager.InteractionState.Evaluating  && !_tertiary.IsMuted;
            int evalCount = (pEval ? 1 : 0) + (sEval ? 1 : 0) + (tEval ? 1 : 0);

            InputManager.InputTracker activeTracker;

            // --- 1. START PHASE (EVALUATING MULTI-TOUCH) ---
            if (evalCount >= 2) {
                Vector2 avgStartPos, avgCurrentPos;
                float maxTimeDown, directionDot, pinchSpreadDelta;

                if (evalCount == 3) {
                    avgStartPos = (_primary.StartPosition + _secondary.StartPosition + _tertiary.StartPosition) / 3f;
                    avgCurrentPos = (_primary.CurrentPosition + _secondary.CurrentPosition + _tertiary.CurrentPosition) / 3f;
                    maxTimeDown = Mathf.Max(Time.time - _primary.TimeDown, Time.time - _secondary.TimeDown, Time.time - _tertiary.TimeDown);

                    //dot product of all 3 touch directions
                    Vector2 dir1 = (_primary.CurrentPosition - _primary.StartPosition).normalized;
                    Vector2 dir2 = (_secondary.CurrentPosition - _secondary.StartPosition).normalized;
                    Vector2 dir3 = (_tertiary.CurrentPosition - _tertiary.StartPosition).normalized;
                    directionDot = (Vector2.Dot(dir1, dir2) + Vector2.Dot(dir2, dir3) + Vector2.Dot(dir1, dir3)) / 3f;
                    
                    float startSpread = Vector2.Distance(_primary.StartPosition, avgStartPos) + Vector2.Distance(_secondary.StartPosition, avgStartPos) + Vector2.Distance(_tertiary.StartPosition, avgStartPos);
                    float currentSpread = Vector2.Distance(_primary.CurrentPosition, avgCurrentPos) + Vector2.Distance(_secondary.CurrentPosition, avgCurrentPos) + Vector2.Distance(_tertiary.CurrentPosition, avgCurrentPos);
                    pinchSpreadDelta = Mathf.Abs(currentSpread - startSpread);

                    // Unified 3-Finger Preview (Clear the lower ones so we don't get 3 overlapping circles)
                    UpdateEvaluatingVisual(InputManager.InputLevel.Tertiary, avgStartPos, avgCurrentPos, maxTimeDown);
                    ClearPreviews(InputManager.InputLevel.Primary);
                    ClearPreviews(InputManager.InputLevel.Secondary);
                } else {
                    avgStartPos = (_primary.StartPosition + _secondary.StartPosition) / 2f;
                    avgCurrentPos = (_primary.CurrentPosition + _secondary.CurrentPosition) / 2f;
                    maxTimeDown = Mathf.Max(Time.time - _primary.TimeDown, Time.time - _secondary.TimeDown);

                    //dot product of all both 2 touch directions
                    Vector2 dir1 = (_primary.CurrentPosition - _primary.StartPosition).normalized;
                    Vector2 dir2 = (_secondary.CurrentPosition - _secondary.StartPosition).normalized;
                    directionDot = Vector2.Dot(dir1, dir2);
                    pinchSpreadDelta = Mathf.Abs(Vector2.Distance(_primary.CurrentPosition, _secondary.CurrentPosition) - Vector2.Distance(_primary.StartPosition, _secondary.StartPosition));

                    // Unified 2-Finger Preview
                    UpdateEvaluatingVisual(InputManager.InputLevel.Secondary, avgStartPos, avgCurrentPos, maxTimeDown);
                    ClearPreviews(InputManager.InputLevel.Primary);
                }

                // Calculate Rotation Math (Using Primary & Secondary as baseline for both 2 and 3 fingers)
                Vector2 startDir        = _secondary.StartPosition - _primary.StartPosition;
                Vector2 currentDir      = _secondary.CurrentPosition - _primary.CurrentPosition;
                float startAngle        = Mathf.Atan2(startDir.y, startDir.x) * Mathf.Rad2Deg;
                float currentAngle      = Mathf.Atan2(currentDir.y, currentDir.x) * Mathf.Rad2Deg;
                float angleDeltaFromStart = Mathf.Abs(Mathf.DeltaAngle(startAngle, currentAngle));

                float distanceMoved     = Vector2.Distance(avgStartPos, avgCurrentPos);

                // Query the thresholds based on how many fingers are evaluating!
                float reqDistance = GetDragThreshold(evalCount);
                float reqGrace = GetGracePeriod(evalCount);
                float reqHold = GetHoldThreshold(evalCount);

                // A. Evaluate Drags, Pinches, and Rotates
                if ((distanceMoved > reqDistance && maxTimeDown > reqGrace) || pinchSpreadDelta > PinchDeadzone || angleDeltaFromStart > RotateDeadzone) {
                    if (directionDot > ParallelDotThreshold) {
                        if (distanceMoved > reqDistance) {
                            if (evalCount == 3) 
                                ExecuteGroupGesture(InputManager.InteractionState.Dragging, _primary, _secondary, _tertiary);
                            else 
                                ExecuteGroupGesture(InputManager.InteractionState.Dragging, _primary, _secondary);
                        }
                    } 
                    else {
                        // Not parallel -> Safe to evaluate Pinch or Rotate
                        if (pinchSpreadDelta > PinchDeadzone) {
                            activeTracker = _tertiary.State == InputManager.InteractionState.Evaluating ? _tertiary : _secondary;

                            if (evalCount == 3) 
                                ExecuteGroupGesture(InputManager.InteractionState.Pinching, _primary, _secondary, _tertiary);
                            else 
                                ExecuteGroupGesture(InputManager.InteractionState.Pinching, _primary, _secondary);
                            
                            // Pinch/Rotate require explicit Started events because ExecuteGroupGesture only fires them for Drag/Hold
                            UpdatePinchActiveVisual(activeTracker.Level, avgCurrentPos, pinchSpreadDelta);
                            FirePinchEvent(activeTracker, InputManager.InputState.Started, avgCurrentPos, pinchSpreadDelta);

                        }else if (angleDeltaFromStart > RotateDeadzone) {
                            activeTracker = _tertiary.State == InputManager.InteractionState.Evaluating ? _tertiary : _secondary;
                            
                            if (evalCount == 3) 
                                ExecuteGroupGesture(InputManager.InteractionState.Rotating, _primary, _secondary, _tertiary);
                            else 
                                ExecuteGroupGesture(InputManager.InteractionState.Rotating, _primary, _secondary);

                            // Pass signed delta to Manager to know which direction they rotated
                            float signedAngleDelta = Mathf.DeltaAngle(startAngle, currentAngle); 
                            UpdateRotateActiveVisual(activeTracker.Level, avgCurrentPos, currentAngle);
                            FireRotateEvent(activeTracker, InputManager.InputState.Started, avgCurrentPos, signedAngleDelta);
                        }
                    }
                }
                // B. Evaluate Holds
                else if (maxTimeDown > reqHold) {
                    if (evalCount == 3) 
                        ExecuteGroupGesture(InputManager.InteractionState.Holding, _primary, _secondary, _tertiary);
                    else 
                        ExecuteGroupGesture(InputManager.InteractionState.Holding, _primary, _secondary);
                }
            }

            // --- 2. ONGOING PHASE (PINCH & ROTATE ONLY) ---
            // (Ongoing Drags and Holds are elegantly handled in ProcessTracker)
            bool isPinching = _primary.State == InputManager.InteractionState.Pinching;
            bool isRotating = _primary.State == InputManager.InteractionState.Rotating;

            if (!isPinching && !isRotating)
                return;

            // Dynamically get the state and center
            InputManager.InteractionState activeState = isPinching ? InputManager.InteractionState.Pinching : InputManager.InteractionState.Rotating;
            Vector2 centerPos = GetSharedCenter(activeState);
            
            // Determine the highest active level for the event (Tertiary if 3 fingers, Secondary if 2)
            activeTracker = _tertiary.State == activeState ? _tertiary : _secondary;

            // VERIFICATION: How many fingers are STILL actively participating?
            int activeGestureCount = (_primary.State == activeState ? 1 : 0) + (_secondary.State == activeState ? 1 : 0) + (_tertiary.State == activeState ? 1 : 0);

            // If a finger lifted and we dropped below 2, force the gesture to cleanly END.
            if (activeGestureCount < 2) {
                if (isPinching) 
                    FirePinchEvent(activeTracker, InputManager.InputState.Ended, centerPos, 0f);
                else 
                    FireRotateEvent(activeTracker, InputManager.InputState.Ended, centerPos, 0f);
                
                // Clean up everything sharing this state
                if (_primary.State == activeState) _primary.Reset();
                if (_secondary.State == activeState) _secondary.Reset();
                if (_tertiary.State == activeState) _tertiary.Reset();
                ClearPreviews(activeTracker.Level);
                return;
            }

            if (isPinching) {
                float currentSpread = Vector2.Distance(_primary.CurrentPosition, _secondary.CurrentPosition);
                float previousSpread = Vector2.Distance(_primary.CurrentPosition - _primary.CurrentDelta, _secondary.CurrentPosition - _secondary.CurrentDelta);
                float framePinchDelta = currentSpread - previousSpread;

                if (Mathf.Abs(framePinchDelta) > 0.01f) {
                    UpdatePinchActiveVisual(activeTracker.Level, centerPos, framePinchDelta);
                    FirePinchEvent(activeTracker, InputManager.InputState.Ongoing, centerPos, framePinchDelta);
                }
            }else if (isRotating) {
                Vector2 currentDir = _secondary.CurrentPosition - _primary.CurrentPosition;
                float currentAngle = Mathf.Atan2(currentDir.y, currentDir.x) * Mathf.Rad2Deg;

                Vector2 previousDir = (_secondary.CurrentPosition - _secondary.CurrentDelta) - (_primary.CurrentPosition - _primary.CurrentDelta);
                float previousAngle = Mathf.Atan2(previousDir.y, previousDir.x) * Mathf.Rad2Deg;
                
                float frameAngleDelta = Mathf.DeltaAngle(previousAngle, currentAngle);

                if (Mathf.Abs(frameAngleDelta) > 0.01f) {
                    UpdateRotateActiveVisual(activeTracker.Level, centerPos, currentAngle);
                    FireRotateEvent(activeTracker, InputManager.InputState.Ongoing, centerPos, frameAngleDelta);
                }
            }
        }

        private void ProcessTracker(InputManager.InputTracker tracker) {
            
            // 1. DISCARD IF MUTED
            if (tracker.State == InputManager.InteractionState.Idle || tracker.IsMuted) return;

            // 2. Single-Finger Evaluating (Multi-finger evaluating is handled in ProcessMultiTouchGestures)
            if (tracker.State == InputManager.InteractionState.Evaluating) {
                // Only draw single-finger evaluating visual if we aren't currently tracking multiple evaluating fingers
                int activeEvals = (_primary.State == InputManager.InteractionState.Evaluating ? 1 : 0) + (_secondary.State == InputManager.InteractionState.Evaluating ? 1 : 0) + (_tertiary.State == InputManager.InteractionState.Evaluating ? 1 : 0);
                
                if (activeEvals == 1) {
                    UpdateEvaluatingVisual(tracker.Level, tracker.StartPosition, tracker.CurrentPosition, tracker.TimeDown);
                }

                float distanceFromStart = Vector2.Distance(tracker.StartPosition, tracker.CurrentPosition);
                float timeHeld = Time.time - tracker.TimeDown;

                //use the fastest one, if we are not on touch!
                int thresholdIndex = 1;
                if(!IsTouch(out int nrOfTouches))
                    thresholdIndex = 0;

                float reqDistance = GetDragThreshold(thresholdIndex);
                float reqGrace = GetGracePeriod(thresholdIndex);
                float reqHold = GetHoldThreshold(thresholdIndex);

                if (!allowSimultaneousInteractions && (distanceFromStart > reqDistance || timeHeld > reqHold)) {
                    if (IsAnyOtherTrackerActive(tracker)) 
                        return; 
                }

                // Standard Single-Finger escalations
                if (distanceFromStart > reqDistance && timeHeld > reqGrace) {
                    ExecuteGroupGesture(InputManager.InteractionState.Dragging, tracker); // Works for a group of 1!
                } else if (timeHeld > reqHold) {
                    ExecuteGroupGesture(InputManager.InteractionState.Holding, tracker);
                }
            }
            
            // 3. Ongoing Executions (Leader updates visuals and fires events for 1, 2, or 3 fingers)
            if (tracker.State == InputManager.InteractionState.Dragging) {
                Vector2 sharedCenter = GetSharedCenter(InputManager.InteractionState.Dragging);
                UpdateDragActiveVisual(tracker.Level, sharedCenter);
                FireDragEvent(tracker, InputManager.InputState.Ongoing, sharedCenter, GetSharedDelta(InputManager.InteractionState.Dragging));
            } else if (tracker.State == InputManager.InteractionState.Holding) {
                Vector2 sharedCenter = GetSharedCenter(InputManager.InteractionState.Holding);
                // Note: The original start pos remains the tracker's own StartPosition to draw the hold line correctly
                UpdateHoldActiveVisual(tracker.Level, tracker.StartPosition, sharedCenter);
                FireHoldEvent(tracker, InputManager.InputState.Ongoing, sharedCenter);
            }
            
        }


        /**** PINCH SPECIFIC SCROLL WHEEL INPUT **/
        private float scrollCooldown = 0.01f;
        private float scrollWheelSensitivity = 20f;
        private float lastScrollTime = -999f;
        private float VerifyPersistentScrollSpeed(float scrollDelta){
            var currentTime = Time.unscaledTime;
            if (currentTime - lastScrollTime < scrollCooldown)
                return 0f;
            lastScrollTime = currentTime;
            return scrollDelta;
        }

        private void ProcessRotateInput(InputAction.CallbackContext ctx) {
             //this is only called via scroll-wheel / specific input event
            //thats why we handle start, ongoing here BUT have to 
            //handle the end state within OnPointerUp (removed cancel-listener)
            float rotateDelta = ctx.ReadValue<float>();
            
            if (Mathf.Abs(rotateDelta) < 0.01f && ctx.phase != InputActionPhase.Canceled) { return; }

            InputManager.InputTracker tracker = _primary; // Map to primary or secondary depending on your scheme

            if (ctx.phase == InputActionPhase.Canceled && tracker.State == InputManager.InteractionState.Rotating) {
                //implement to use only when not using Touches/Buttons (no axis!)
                FireRotateEvent(tracker, InputManager.InputState.Ended, tracker.CurrentPosition, rotateDelta);
                tracker.Reset();
                ClearPreviews(tracker.Level);
            } else {
                if (tracker.State == InputManager.InteractionState.Evaluating || tracker.State == InputManager.InteractionState.Dragging || tracker.State == InputManager.InteractionState.Idle) {
                    tracker.State = InputManager.InteractionState.Rotating;
                    FireRotateEvent(tracker, InputManager.InputState.Started, tracker.CurrentPosition,  rotateDelta);
                } else if (tracker.State == InputManager.InteractionState.Rotating) {
                    FireRotateEvent(tracker, InputManager.InputState.Ongoing, tracker.CurrentPosition,  rotateDelta);
                    
                    UpdateRotateActiveVisual(tracker.Level, tracker.CurrentPosition, rotateDelta);
                }
            }
        }
        #endregion

        #region UP/DOWN-PHASES

        private void OnPointerDown(InputManager.InputTracker tracker) {
            // If we are currently pinching or rotating, deny starting a new click/drag evaluation
            if (tracker.State == InputManager.InteractionState.Pinching || tracker.State == InputManager.InteractionState.Rotating) { return; }

            // DEBUG
            Debug.Log("<color=yellow>OnPointerDown "+tracker.Level+"</color> at "+tracker.CurrentPosition);
            // -----

            // Fetch the exact position right now, bypassing the Update loop delay
            Vector2 exactStartPos = GetCurrentPos(tracker.Level);
            

            tracker.State           = InputManager.InteractionState.Evaluating;
            tracker.TimeDown        = Time.time;
            // Initialize all position data to this exact point
            tracker.StartPosition   = exactStartPos;
            tracker.CurrentPosition = exactStartPos;
            tracker.CurrentDelta    = Vector2.zero; // Explicitly zero out the delta
        }

        private void OnPointerUp(InputManager.InputTracker tracker) {
            Debug.Log("<color=yellow>OnPointerUp "+tracker.Level+" / "+tracker.State+"</color>");

            if (tracker.State == InputManager.InteractionState.Idle) { return; }

            // 1. DISCARD IF MUTED (But clean up)
            if (tracker.IsMuted) {
                tracker.Reset();
                // Notice we return! Next frame, the Leader will just calculate the center with one less finger.
                // This gracefully degrades a 3-finger drag into a 2-finger drag!
                return; 
            }

            // 2. RESOLVE CLICKS (Only unmuted Evaluating trackers reach here)
            if (tracker.State == InputManager.InteractionState.Evaluating) {
                // If others are evaluating, mute myself and abort. I am not the last finger.
                if (tracker != _primary     && _primary.State   == InputManager.InteractionState.Evaluating) { tracker.Reset(); return; }
                if (tracker != _secondary   && _secondary.State == InputManager.InteractionState.Evaluating) { tracker.Reset(); return; }
                if (tracker != _tertiary    && _tertiary.State  == InputManager.InteractionState.Evaluating) { tracker.Reset(); return; }

                // If I survived the checks above, I am the final, highest-level finger to lift!
                if (Time.time - tracker.LastClickTime <= DoubleClickTimeThreshold) {
                    FireDoubleClickEvent(tracker);
                    tracker.LastClickTime = -100f;
                } else {
                    FireClickEvent(tracker);
                    tracker.LastClickTime = Time.time;
                }
            }
            // 3. RESOLVE ONGOING EVENTS (Ended)
            else if (tracker.State == InputManager.InteractionState.Dragging) {
                FireDragEvent(tracker, InputManager.InputState.Ended, GetSharedCenter(InputManager.InteractionState.Dragging), Vector2.zero);
                ReleaseMutedTrackers(InputManager.InteractionState.Dragging);
            }else if (tracker.State == InputManager.InteractionState.Holding) {
                FireHoldEvent(tracker, InputManager.InputState.Ended, GetSharedCenter(InputManager.InteractionState.Holding));
                ReleaseMutedTrackers(InputManager.InteractionState.Holding);
            }else if (tracker.State == InputManager.InteractionState.Pinching) {
                FirePinchEvent(tracker, InputManager.InputState.Ended, tracker.CurrentPosition, 0f);
            }else if (tracker.State == InputManager.InteractionState.Rotating) {
                FireRotateEvent(tracker, InputManager.InputState.Ended, tracker.CurrentPosition, 0f);
            }

            tracker.Reset();
            ClearPreviews(tracker.Level);
        }

        // Ensures if the Leader lifts first, the muted subordinates don't get stuck
        private void ReleaseMutedTrackers(InputManager.InteractionState stateToClear) {
            if (_primary.State   == stateToClear && _primary.IsMuted)   _primary.Reset();
            if (_secondary.State == stateToClear && _secondary.IsMuted) _secondary.Reset();
            if (_tertiary.State  == stateToClear && _tertiary.IsMuted)  _tertiary.Reset();
        }
        #endregion



        #region FIRE EVENTS

        // --- HELPER METHODS FOR FIRING EVENTS ---

        private void FireClickEvent(InputManager.InputTracker tracker) {
            InputManager.InputData data = InputManager.CreateData(tracker, InputManager.InputState.Ended);
            
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

        private void FireDoubleClickEvent(InputManager.InputTracker tracker) {
            InputManager.InputData data = InputManager.CreateData(tracker, InputManager.InputState.Ended);
            
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

        private void FireDragEvent(InputManager.InputTracker tracker, InputManager.InputState state, Vector2 centerPos, Vector2 avgDelta) {
            InputManager.InputData data = InputManager.CreateData(tracker, state);
            data.Position = centerPos;
            data.Delta = avgDelta;

            //[!REVISE] do we need to have "initial click pos"? (for evaluation for the correct thing we hit - that we want to drag)
            //Debug.Log("DRAG EVENT "+data.ToString());

            if(state == InputManager.InputState.Started) {
                layerDrag = EvaluationHelper.Instance.EvaluateOperationLayer(tracker.StartPosition);
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


        private void FireHoldEvent(InputManager.InputTracker tracker, InputManager.InputState state, Vector2 centerPos) {
            InputManager.InputData data = InputManager.CreateData(tracker, state);
            data.Position = centerPos;

            if(state == InputManager.InputState.Started) {
                layerHold = EvaluationHelper.Instance.EvaluateOperationLayer(centerPos);
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

        private void FirePinchEvent(InputManager.InputTracker tracker, InputManager.InputState state, Vector2 centerPos, float pinchDelta) {
            InputManager.InputData data = InputManager.CreateData(tracker, state);
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

        private void FireRotateEvent(InputManager.InputTracker tracker, InputManager.InputState state, Vector2 centerPos, float rotateDelta) {
            InputManager.InputData data = InputManager.CreateData(tracker, state);
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

            int inputLevelForThresholds = 0;
            if(level == InputManager.InputLevel.Secondary)
                inputLevelForThresholds = 1;
            else if(level == InputManager.InputLevel.Tertiary)
                inputLevelForThresholds = 2;

            if (!evaluatingUIs.TryGetValue(level, out GameObject container) || container == null){
                container = new GameObject($"EvaluatingUI_{level}");
                container.transform.SetParent(mainUIContainer.transform);
                
                // Setup Drag Rect Preview
                GameObject rectGO = new GameObject("DragRect");
                rectGO.transform.SetParent(container.transform);
                Image rectImg = rectGO.AddComponent<Image>();
                rectImg.color = new Color(1f, 1f, 1f, 0f); // Start transparent
                rectImg.rectTransform.position = startPos;
                rectImg.rectTransform.sizeDelta = Vector2.one * GetDragThreshold(inputLevelForThresholds) * 2f;

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
            float holdProgress = (Time.time - timeDown) / GetHoldThreshold(inputLevelForThresholds);
            float dragProgress = distance / GetDragThreshold(inputLevelForThresholds);

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
                int inputLevelForThresholds = 0;
                if(level == InputManager.InputLevel.Secondary)
                    inputLevelForThresholds = 1;
                else if(level == InputManager.InputLevel.Tertiary)
                    inputLevelForThresholds = 2;

                container = new GameObject($"DragActiveUI_{level}");
                container.transform.SetParent(mainUIContainer.transform);

                // Persistent solid rectangle following the finger during the drag
                GameObject rectGO = new GameObject("ActiveDragRect");
                rectGO.transform.SetParent(container.transform);
                Image rectImg = rectGO.AddComponent<Image>();
                rectImg.color = new Color(1f, 1f, 1f, 0.4f); // 40% opacity white
                rectImg.rectTransform.sizeDelta = Vector2.one * GetDragThreshold(inputLevelForThresholds) * 2f;

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