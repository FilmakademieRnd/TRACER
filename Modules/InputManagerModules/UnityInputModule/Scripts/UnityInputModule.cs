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
//! @version 1
//! @date 06.08.2026
//! @changed improved readability

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
        //! helper for checking and triggering different states of "any input"
        //!
        private bool isAnyInputActive = false;

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

        //!
        //! all input trackers, ordered from lowest to highest level (Primary, Secondary, Tertiary)
        //! this array is the single source of truth - every "do it for all trackers" logic loops over it
        //!
        private readonly InputTracker[] _trackers = {
            new InputTracker(InputManager.InputLevel.Primary),
            new InputTracker(InputManager.InputLevel.Secondary),
            new InputTracker(InputManager.InputLevel.Tertiary)
        };

        //!
        //! named access for the places where one specific level is meant (e.g. binding a unity action)
        //!
        private InputTracker _primary   => _trackers[0];
        private InputTracker _secondary => _trackers[1];
        private InputTracker _tertiary  => _trackers[2];

        //to request at start, but not ongoing!
        private EvaluationHelper.OperationLayer layerDrag, layerHold, layerPinch, layerRotate = EvaluationHelper.OperationLayer.OTHER;
        #endregion

        #region TRACKER HELPERS

        // --- QUERIES (how many / which tracker is in a certain state) ---

        // Number of trackers currently in that state (optionally ignoring the muted ones)
        private int CountInState(InteractionState state, bool skipMuted = false) {
            int count = 0;
            foreach (InputTracker t in _trackers)
                if (t.State == state && (!skipMuted || !t.IsMuted)) count++;
            return count;
        }

        // Highest level tracker in that state, i.e. the Leader of a running gesture (null if none)
        private InputTracker HighestInState(InteractionState state) {
            for (int i = _trackers.Length - 1; i >= 0; i--)
                if (_trackers[i].State == state) return _trackers[i];
            return null;
        }

        // Is any OTHER tracker in that state? (used to find out if we are the last finger of a gesture)
        private bool AnyOtherInState(InputTracker excludeTracker, InteractionState state) {
            foreach (InputTracker t in _trackers)
                if (t != excludeTracker && t.State == state) return true;
            return false;
        }

        // The first n trackers as an ordered group (Primary first) - only built on gesture start, so the allocation is uncritical
        private InputTracker[] LeadingTrackers(int count) {
            InputTracker[] group = new InputTracker[Mathf.Clamp(count, 1, _trackers.Length)];
            Array.Copy(_trackers, group, group.Length);
            return group;
        }

        // --- RESETS ---

        // Clean up everything sharing this state
        private void ResetAllInState(InteractionState state) {
            foreach (InputTracker t in _trackers)
                if (t.State == state) t.Reset();
        }

        // --- MULTI-TOUCH MATH (all of it works for 2 AND 3 trackers, so no separate code paths are needed) ---

        // Centroid of the positions where the leading n trackers were pressed down
        private Vector2 AverageStartPosition(int trackerCount) {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < trackerCount; i++) sum += _trackers[i].StartPosition;
            return sum / trackerCount;
        }

        // Centroid of the current positions of the leading n trackers
        private Vector2 AverageCurrentPosition(int trackerCount) {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < trackerCount; i++) sum += _trackers[i].CurrentPosition;
            return sum / trackerCount;
        }

        // Longest time any of the leading n trackers is already held down
        private float MaxTimeHeld(int trackerCount) {
            float maxTime = 0f;
            for (int i = 0; i < trackerCount; i++) maxTime = Mathf.Max(maxTime, Time.time - _trackers[i].TimeDown);
            return maxTime;
        }

        // Averaged dot product of all tracker-pairs: 1 means all move perfectly parallel (drag), lower means they diverge (pinch/rotate)
        private float AverageDirectionDot(int trackerCount) {
            float sum = 0f;
            int pairs = 0;
            for (int a = 0; a < trackerCount; a++) {
                Vector2 dirA = (_trackers[a].CurrentPosition - _trackers[a].StartPosition).normalized;
                for (int b = a + 1; b < trackerCount; b++) {
                    Vector2 dirB = (_trackers[b].CurrentPosition - _trackers[b].StartPosition).normalized;
                    sum += Vector2.Dot(dirA, dirB);
                    pairs++;
                }
            }
            return pairs > 0 ? sum / pairs : 1f;
        }

        // How much the fingers spread out around their centroid (for 2 fingers this is identical to their plain distance)
        private float CentroidSpread(int trackerCount, bool useStartPosition) {
            Vector2 center = useStartPosition ? AverageStartPosition(trackerCount) : AverageCurrentPosition(trackerCount);
            float spread = 0f;
            for (int i = 0; i < trackerCount; i++)
                spread += Vector2.Distance(useStartPosition ? _trackers[i].StartPosition : _trackers[i].CurrentPosition, center);
            return spread;
        }

        // --- GESTURE EXECUTION ---

        // Groups trackers, assigns the state, makes the highest level the Leader, and mutes the rest.
        private void ExecuteGroupGesture(InteractionState state, params InputTracker[] group) {
            InputTracker leader = group[group.Length - 1]; // Assumes array is ordered lowest to highest

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
            if (state == InteractionState.Dragging) {
                FireDragEvent(leader, InputManager.InputState.Started, center, delta);
                // Start the visual immediately
                UpdateDragActiveVisual(leader.Level, center); 
            } else if (state == InteractionState.Holding) {
                FireHoldEvent(leader, InputManager.InputState.Started, center);
                UpdateHoldActiveVisual(leader.Level, leader.StartPosition, center);
            }
            // Pinch/Rotate start events are handled in the Orchestrator as they require custom deltas
        }

        // Dynamically averages the positions of ALL trackers sharing the exact same state
        private Vector2 GetSharedCenter(InteractionState state) {
            Vector2 sum = Vector2.zero; 
            int count = 0;
            foreach (InputTracker t in _trackers)
                if (t.State == state) { sum += t.CurrentPosition; count++; }
            return count > 0 ? sum / count : Vector2.zero;
        }

        private Vector2 GetSharedDelta(InteractionState state) {
            Vector2 sum = Vector2.zero; 
            int count = 0;
            foreach (InputTracker t in _trackers)
                if (t.State == state) { sum += t.CurrentDelta; count++; }
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
            anyInputAction = new InputAction("AnyInput", InputActionType.Button);
            // 1. Catches any touch on the screen (or mouse click/pen tap)
            anyInputAction.AddBinding("<Pointer>/press");

            // 2. Catches any button on a Bluetooth/USB Gamepad
            anyInputAction.AddBinding("<Gamepad>/<button>");

            // 3. Catches any physical keyboard key connected via USB/Bluetooth
            anyInputAction.AddBinding("<Keyboard>/anyKey");

            // Subscribe to your events
            // anyAction.performed += ProcessAnyInput;
            // no more subscription, we poll manually from the update event to trigger different states
            // without the need of started/performed - they may behave differently
            
            anyInputAction.Enable();
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
                //anyInputAction.performed                    -= ProcessAnyInput;
                anyInputAction.Disable();
                anyInputAction.Dispose();
            }
        }

        #endregion

        #region PROCESSION

        //!
        //! check if any input was detected and keep track of its state
        //!
        private void ProcessManualAnyInput() {
            if(anyInputAction == null) return;

            bool isCurrentlyAnyInputDectected = anyInputAction.IsPressed();

            if(isCurrentlyAnyInputDectected && !isAnyInputActive) {
                isAnyInputActive = true;
                FireAnyInputEvent(InputManager.InputState.Started);
            }else if(isCurrentlyAnyInputDectected && isAnyInputActive) {
                FireAnyInputEvent(InputManager.InputState.Ongoing);
            }else if(!isCurrentlyAnyInputDectected && isAnyInputActive) {
                isAnyInputActive = false;
                FireAnyInputEvent(InputManager.InputState.Ended);
            }
        }

        //! obsolete - the event driven approach is not good for continous values
        //! tracks the positions of our primary input (primary touch, mouse pos)
        //! and writes them into a buffer to allow further calculations (delta, speed, etc)
        //!
        private void ProcessPositionInput(){ 
            //https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/Touch.html
            // You should not use Touchscreen for polling. If you want to read out touches similar to UnityEngine.Input.touches, see EnhancedTouch. 
            foreach (InputTracker t in _trackers)
                UpdateTrackerPosition(t, GetCurrentPos(t.Level));
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
        private bool IsAnyOtherTrackerActive(InputTracker excludeTracker) {
            foreach (InputTracker t in _trackers)
                if (t != excludeTracker && t.State >= InteractionState.Dragging) return true;
            return false;
        }


        //!
        //! verifying that scroll input via the mouse wheel
        //! relying on the started, ongoing, canceled action was not trustworthy
        //!
        private void ProcessScrollInput() {
            // 1. Read the raw value directly
            float scrollDelta = m_inputs.VPETMap.OnPinch.ReadValue<float>();

            // NOTE: the scroll wheel has no tracker of its own - it deliberately re-uses the _tertiary slot to publish
            // its pinch events (and locks _secondary so a right-click cannot interfere). So a Tertiary pinch event does
            // NOT necessarily mean "three fingers" - on desktop it is the mouse wheel. See the input-level manifest.

            // 2. IS ACTIVELY SCROLLING
            if (Mathf.Abs(scrollDelta) > 0.01f) {
                Vector2 mousePos = GetCurrentPos(InputManager.InputLevel.Primary);

                float scrollDeltaAdjusted = VerifyPersistentScrollSpeed(scrollDelta * scrollWheelSensitivity);

                if (!_isMouseScrolling) {
                    // --- STARTED PHASE ---
                    _isMouseScrolling = true;
                    _secondary.State = InteractionState.Pinching; // Lock state so right-clicks are ignored
                    
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
            
            ProcessManualAnyInput();

            ProcessPositionInput();

            ProcessMultiTouchGestures();

            ProcessScrollInput();

            foreach (InputTracker t in _trackers)
                ProcessTracker(t);
        }

        private void ProcessMultiTouchGestures() {
            // 1. Exit early
            if (!IsTouch(out int nrOfTouches) || nrOfTouches <= 1 || !manager.IsMultiTouchGestureAllowed()) 
                return; 

            int evalCount = CountInState(InteractionState.Evaluating, skipMuted: true);

            InputTracker activeTracker;

            // --- 1. START PHASE (EVALUATING MULTI-TOUCH) ---
            if (evalCount >= 2) {
                // all of the following math works identically for 2 and 3 trackers, so there is only one code path
                Vector2 avgStartPos     = AverageStartPosition(evalCount);
                Vector2 avgCurrentPos   = AverageCurrentPosition(evalCount);
                float maxTimeDown       = MaxTimeHeld(evalCount);

                //dot product of all touch directions
                float directionDot      = AverageDirectionDot(evalCount);
                float pinchSpreadDelta  = Mathf.Abs(CentroidSpread(evalCount, false) - CentroidSpread(evalCount, true));

                // Unified Preview on the leading level (Clear the lower ones so we don't get overlapping circles)
                InputTracker evalLeader = _trackers[evalCount - 1];
                UpdateEvaluatingVisual(evalLeader.Level, avgStartPos, avgCurrentPos, maxTimeDown);
                for (int i = 0; i < evalCount - 1; i++)
                    ClearPreviews(_trackers[i].Level);

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
                            ExecuteGroupGesture(InteractionState.Dragging, LeadingTrackers(evalCount));
                        }
                    } 
                    else {
                        // Not parallel -> Safe to evaluate Pinch or Rotate
                        if (pinchSpreadDelta > PinchDeadzone) {
                            activeTracker = evalLeader;

                            ExecuteGroupGesture(InteractionState.Pinching, LeadingTrackers(evalCount));
                            
                            // Pinch/Rotate require explicit Started events because ExecuteGroupGesture only fires them for Drag/Hold
                            UpdatePinchActiveVisual(activeTracker.Level, avgCurrentPos, pinchSpreadDelta);
                            FirePinchEvent(activeTracker, InputManager.InputState.Started, avgCurrentPos, pinchSpreadDelta);

                        }else if (angleDeltaFromStart > RotateDeadzone) {
                            activeTracker = evalLeader;
                            
                            ExecuteGroupGesture(InteractionState.Rotating, LeadingTrackers(evalCount));

                            // Pass signed delta to Manager to know which direction they rotated
                            float signedAngleDelta = Mathf.DeltaAngle(startAngle, currentAngle); 
                            UpdateRotateActiveVisual(activeTracker.Level, avgCurrentPos, currentAngle);
                            FireRotateEvent(activeTracker, InputManager.InputState.Started, avgCurrentPos, signedAngleDelta);
                        }
                    }
                }
                // B. Evaluate Holds
                else if (maxTimeDown > reqHold) {
                    ExecuteGroupGesture(InteractionState.Holding, LeadingTrackers(evalCount));
                }
            }

            // --- 2. ONGOING PHASE (PINCH & ROTATE ONLY) ---
            // (Ongoing Drags and Holds are elegantly handled in ProcessTracker)
            bool isPinching = _primary.State == InteractionState.Pinching;
            bool isRotating = _primary.State == InteractionState.Rotating;

            if (!isPinching && !isRotating)
                return;

            // Dynamically get the state and center
            InteractionState activeState = isPinching ? InteractionState.Pinching : InteractionState.Rotating;
            Vector2 centerPos = GetSharedCenter(activeState);
            
            // Determine the highest active level for the event (Tertiary if 3 fingers, Secondary if 2)
            activeTracker = HighestInState(activeState);

            // VERIFICATION: How many fingers are STILL actively participating?
            int activeGestureCount = CountInState(activeState);

            // If a finger lifted and we dropped below 2, force the gesture to cleanly END.
            if (activeGestureCount < 2) {
                if (isPinching) 
                    FirePinchEvent(activeTracker, InputManager.InputState.Ended, centerPos, 0f);
                else 
                    FireRotateEvent(activeTracker, InputManager.InputState.Ended, centerPos, 0f);
                
                // Clean up everything sharing this state
                ResetAllInState(activeState);
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

        private void ProcessTracker(InputTracker tracker) {
            
            // 1. DISCARD IF MUTED
            if (tracker.State == InteractionState.Idle || tracker.IsMuted) return;

            // 2. Single-Finger Evaluating (Multi-finger evaluating is handled in ProcessMultiTouchGestures)
            if (tracker.State == InteractionState.Evaluating) {
                // Only draw single-finger evaluating visual if we aren't currently tracking multiple evaluating fingers
                int activeEvals = CountInState(InteractionState.Evaluating);
                
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

                //if we disallow multitouchgesture, we allow simulataneous interactions!
                //if (!allowSimultaneousInteractions && (distanceFromStart > reqDistance || timeHeld > reqHold)) {
                if ((!allowSimultaneousInteractions && manager.IsMultiTouchGestureAllowed()) && (distanceFromStart > reqDistance || timeHeld > reqHold)) {
                    if (IsAnyOtherTrackerActive(tracker)) 
                        return; 
                }

                // Standard Single-Finger escalations
                if (distanceFromStart > reqDistance && timeHeld > reqGrace) {
                    ExecuteGroupGesture(InteractionState.Dragging, tracker); // Works for a group of 1!
                } else if (timeHeld > reqHold) {
                    ExecuteGroupGesture(InteractionState.Holding, tracker);
                }
            }

            if(allowSimultaneousInteractions || !manager.IsMultiTouchGestureAllowed()) {
                //no shared center or stuff!
                if (tracker.State == InteractionState.Dragging) {
                    UpdateDragActiveVisual(tracker.Level, tracker.CurrentPosition);
                    FireDragEvent(tracker, InputManager.InputState.Ongoing, tracker.CurrentPosition, GetSharedDelta(InteractionState.Dragging));
                } else if (tracker.State == InteractionState.Holding) {
                    UpdateHoldActiveVisual(tracker.Level, tracker.StartPosition, tracker.CurrentPosition);
                    FireHoldEvent(tracker, InputManager.InputState.Ongoing, tracker.CurrentPosition);
                }
            } else {
                //standard behaviour as with touch gestures
                // 3. Ongoing Executions (Leader updates visuals and fires events for 1, 2, or 3 fingers)
                if (tracker.State == InteractionState.Dragging) {
                    Vector2 sharedCenter = GetSharedCenter(InteractionState.Dragging);
                    UpdateDragActiveVisual(tracker.Level, sharedCenter);
                    FireDragEvent(tracker, InputManager.InputState.Ongoing, sharedCenter, GetSharedDelta(InteractionState.Dragging));
                } else if (tracker.State == InteractionState.Holding) {
                    Vector2 sharedCenter = GetSharedCenter(InteractionState.Holding);
                    // Note: The original start pos remains the tracker's own StartPosition to draw the hold line correctly
                    UpdateHoldActiveVisual(tracker.Level, tracker.StartPosition, sharedCenter);
                    FireHoldEvent(tracker, InputManager.InputState.Ongoing, sharedCenter);
                }
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

        //!
        //! TODO: implement for scroll wheel within certain action or key modifier
        //!
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
        //!
        //! 
        //!
        private void OnPointerDown(InputTracker tracker) {
            // If we are currently pinching or rotating, deny starting a new click/drag evaluation
            if (tracker.State == InteractionState.Pinching || tracker.State == InteractionState.Rotating) { return; }

            // DEBUG
            Debug.Log("<color=yellow>OnPointerDown "+tracker.Level+"</color> at "+tracker.CurrentPosition);
            // -----

            // Fetch the exact position right now, bypassing the Update loop delay
            Vector2 exactStartPos = GetCurrentPos(tracker.Level);
            

            tracker.State           = InteractionState.Evaluating;
            tracker.TimeDown        = Time.time;
            // Initialize all position data to this exact point
            tracker.StartPosition   = exactStartPos;
            tracker.CurrentPosition = exactStartPos;
            tracker.CurrentDelta    = Vector2.zero; // Explicitly zero out the delta
        }

        //!
        //! 
        //!
        private void OnPointerUp(InputTracker tracker) {
            Debug.Log("<color=yellow>OnPointerUp "+tracker.Level+" / "+tracker.State+"</color>");

            if (tracker.State == InteractionState.Idle) { return; }

            // 1. DISCARD IF MUTED (But clean up)
            if (tracker.IsMuted) {
                tracker.Reset();
                // Notice we return! Next frame, the Leader will just calculate the center with one less finger.
                // This gracefully degrades a 3-finger drag into a 2-finger drag!
                return; 
            }

            // 2. RESOLVE CLICKS (Only unmuted Evaluating trackers reach here)
            if (tracker.State == InteractionState.Evaluating) {
                // If others are evaluating, mute myself and abort. I am not the last finger.
                if (AnyOtherInState(tracker, InteractionState.Evaluating)) { tracker.Reset(); return; }

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
            else if (tracker.State == InteractionState.Dragging) {
                FireDragEvent(tracker, InputManager.InputState.Ended, GetSharedCenter(InteractionState.Dragging), Vector2.zero);
                ReleaseMutedTrackers(InteractionState.Dragging);
            }else if (tracker.State == InteractionState.Holding) {
                FireHoldEvent(tracker, InputManager.InputState.Ended, GetSharedCenter(InteractionState.Holding));
                ReleaseMutedTrackers(InteractionState.Holding);
            }else if (tracker.State == InteractionState.Pinching) {
                FirePinchEvent(tracker, InputManager.InputState.Ended, tracker.CurrentPosition, 0f);
            }else if (tracker.State == InteractionState.Rotating) {
                FireRotateEvent(tracker, InputManager.InputState.Ended, tracker.CurrentPosition, 0f);
            }

            tracker.Reset();
            ClearPreviews(tracker.Level);
        }

        // Ensures if the Leader lifts first, the muted subordinates don't get stuck
        private void ReleaseMutedTrackers(InteractionState stateToClear) {
            foreach (InputTracker t in _trackers)
                if (t.State == stateToClear && t.IsMuted) t.Reset();
        }
        #endregion



        #region FIRE EVENTS

        // --- HELPER METHODS FOR FIRING EVENTS ---

        
        //! 
        //! fire the event of the `anyInputAction`
        //! @param state the state of the `anyInputAction` we check manually
        //!
        private void FireAnyInputEvent(InputManager.InputState state) {
            var anyInputData = new InputManager.AnyEventArgs {
                State = state
            };
            
            manager.RaiseAnyInput(this, anyInputData);
        }

        //!
        //! 
        //!
        private void FireClickEvent(InputTracker tracker) {
            var data = new InputManager.InputEventArgs(tracker.Level, InputManager.InputState.Ended, tracker.CurrentPosition);
            
            switch (EvaluationHelper.Instance.EvaluateOperationLayer(tracker.CurrentPosition)){
                case EvaluationHelper.OperationLayer.UI2D:
                    manager.RaiseClickUI(this, data);
                    break;
                case EvaluationHelper.OperationLayer.UI3D:
                case EvaluationHelper.OperationLayer.SCENEOBJECT:
                case EvaluationHelper.OperationLayer.OTHER:
                    manager.RaiseClickOther(this, data);
                    // possible further investigation for outcome "other"
                    // if (RayMeshUtility.GetHitPointPrecise(mainCam.ScreenPointToRay(m_pos), m_worldGameObjectWeHit, RayMeshUtility.Accuracy.ExactMesh, out m_worldHitPos)){
                    //     UnityHitVisualizerHelper.Spawn(m_worldHitPos, Color.green, 0.15f);
                    // }
                    break;
            }

            SpawnClickVisual(tracker.Level, tracker.CurrentPosition, isDouble: false);
        }

        //!
        //! 
        //!
        private void FireDoubleClickEvent(InputTracker tracker) {
            var data = new InputManager.InputEventArgs(tracker.Level, InputManager.InputState.Ended, tracker.CurrentPosition);
            
            switch (EvaluationHelper.Instance.EvaluateOperationLayer(tracker.CurrentPosition)){
                case EvaluationHelper.OperationLayer.UI2D:
                    manager.RaiseDoubleClickUI(this, data);
                    break;
                case EvaluationHelper.OperationLayer.UI3D:
                case EvaluationHelper.OperationLayer.SCENEOBJECT:
                case EvaluationHelper.OperationLayer.OTHER:
                    manager.RaiseDoubleClickOther(this, data);
                    break;
            }
            SpawnClickVisual(tracker.Level, tracker.CurrentPosition, isDouble: true);
        }

        //!
        //! 
        //!
        private void FireDragEvent(InputTracker tracker, InputManager.InputState state, Vector2 centerPos, Vector2 avgDelta) {
            var data = new InputManager.DragEventArgs(tracker.Level, state, centerPos, avgDelta, tracker.StartPosition);

            // only ever set in started, because afterwards THIS input event stays on its OperationLayer!
            if(state == InputManager.InputState.Started) {
                layerDrag = EvaluationHelper.Instance.EvaluateOperationLayer(tracker.StartPosition);
            }

            switch (layerDrag){
                case EvaluationHelper.OperationLayer.UI2D:
                    manager.RaiseDragUI(this, data);
                    break;
                case EvaluationHelper.OperationLayer.UI3D:
                case EvaluationHelper.OperationLayer.SCENEOBJECT:
                case EvaluationHelper.OperationLayer.OTHER:
                    manager.RaiseDragOther(this, data);
                    break;
            }
        }

        //!
        //! 
        //!
        private void FireHoldEvent(InputTracker tracker, InputManager.InputState state, Vector2 centerPos) {
            var data = new InputManager.InputEventArgs(tracker.Level, state, tracker.CurrentPosition);

            if(state == InputManager.InputState.Started) {
                layerHold = EvaluationHelper.Instance.EvaluateOperationLayer(centerPos);
            }

            switch (layerHold){
                case EvaluationHelper.OperationLayer.UI2D:
                    manager.RaiseHoldUI(this, data);
                    break;
                case EvaluationHelper.OperationLayer.UI3D:
                case EvaluationHelper.OperationLayer.SCENEOBJECT:
                case EvaluationHelper.OperationLayer.OTHER:
                    manager.RaiseHoldOther(this, data);
                    break;
            }
        }

        //!
        //! 
        //!
        private void FirePinchEvent(InputTracker tracker, InputManager.InputState state, Vector2 centerPos, float pinchDelta) {
            var data = new InputManager.PinchEventArgs(tracker.Level, state, centerPos, pinchDelta);
            
            if(state == InputManager.InputState.Started) {
                layerPinch = EvaluationHelper.Instance.EvaluateOperationLayer(centerPos);
            }

            switch (layerPinch){
                case EvaluationHelper.OperationLayer.UI2D:
                    manager.RaisePinchUI(this, data);
                    break;
                case EvaluationHelper.OperationLayer.UI3D:
                case EvaluationHelper.OperationLayer.SCENEOBJECT:
                case EvaluationHelper.OperationLayer.OTHER:
                    manager.RaisePinchOther(this, data);
                    break;
            }
        }

        //!
        //! 
        //!
        private void FireRotateEvent(InputTracker tracker, InputManager.InputState state, Vector2 centerPos, float rotateDelta) {
            var data = new InputManager.RotateEventArgs(tracker.Level, state, centerPos, rotateDelta);
            
            if(state == InputManager.InputState.Started) {
                layerRotate = EvaluationHelper.Instance.EvaluateOperationLayer(centerPos);
            }

            switch (layerRotate){
                case EvaluationHelper.OperationLayer.UI2D:
                    manager.RaiseRotateUI(this, data);
                    break;
                case EvaluationHelper.OperationLayer.UI3D:
                case EvaluationHelper.OperationLayer.SCENEOBJECT:
                case EvaluationHelper.OperationLayer.OTHER:
                    manager.RaiseRotateOther(this, data);
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