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

//! @file "InputManager.cs"
//! @brief Implementation of the TRACER Input Manager, managing all user inupts and mapping.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @author Paulo Scatena
//! @author Thomas Krüger
//! @version 1
//! @date 19.05.2026
//! @revision overhaul event subscription and distribution, reduce unity-dependency, setup UnityInputModule


using System;
using System.Collections.Generic;

namespace tracer{
    //!
    //! Class implementing the input manager, managing all user inupts and mapping.
    //!
    public class InputManager : Manager{


        // **** PARAMETER DESIGN ****

        // --- ENUMS ---
        public enum InputLevel      { Primary, Secondary, Tertiary }
        public enum InputState      { Started, Ongoing, Ended, Canceled }   //ended can be counted as an executed as well
        //public enum InputDeviceType { Mouse, Touch, Controller, Keyboard, Special }

        // --- EVENT PARAMTER-DATA ---
        public struct InputData{
            public InputLevel Level;
            public InputState State;
            //public InputDeviceType Device;
            public UnityEngine.Vector2 Position;    //has to be replaced by own v2 implementation
            public UnityEngine.Vector2 Delta;       //has to be replaced by own v2 implementation
        }

        // TODO: remove Unity dependency, so other modules could utilize it without referencing to other module
        public class InputTracker{
            public InputLevel Level;   //primary, secondary, tertiary
            public InteractionState State = InteractionState.Idle;  //see above
            /*
                Leader & Muted - Pattern - The Rules:
                - on multi-touch, identify the "Highest Level" tracker (e.g., Secondary) -> becomes Leader
                - set all involved trackers to the same state (e.g., Dragging), but mute on the lower-level trackers
                - ProcessTracker/OnPointerUp: if tracker == IsMuted, it discards itself completely
                - Lead Tracker processes normally, but it calculates its position by averaging all trackers that share its current state.
            */
            public bool IsMuted = false;
            public UnityEngine.Vector2 CurrentPosition; //necessary for multitouch and for correct oop approach
            public UnityEngine.Vector2 CurrentDelta;
            public float TimeDown;
            public UnityEngine.Vector2 StartPosition;
            public float LastClickTime = -100f; // Tracked for Double Click

            public InputTracker(InputLevel level){ Level = level; }
            public void Reset(){ 
                State = InteractionState.Idle; 
                IsMuted = false; 
                CurrentPosition = UnityEngine.Vector2.zero;
                CurrentDelta = UnityEngine.Vector2.zero; 
                TimeDown = 0f;
                StartPosition = UnityEngine.Vector2.zero;
            }
        }

        public enum InteractionState { 
            Idle,           // Nothing is happening
            Evaluating,     // Pointer is down, waiting to see if it becomes Click, Drag, or Hold
            Dragging,       // Surpassed distance threshold (Holds are now denied)
            Holding,        // Surpassed time threshold (Drags are now denied)
            Pinching,       // Surpassed pinch delta (Drags/Holds denied)
            Rotating       // Surpassed rotation delta (Drags/Holds denied)
        }

        // --- INTERFACE ---
        public interface IInputEvent { }

        // --- EVENTS ---
        public struct AnyInputEvent                         : IInputEvent { public InputData Data; }

        // Click, Tap, Button Press
        public struct ClickUIEvent                          : IInputEvent { public InputData Data; }
        public struct ClickOtherEvent                       : IInputEvent { public InputData Data; }

        // Drags (mouse hold + move, 1 finger touch + move, controller button hold + move)
        // Primary: 1f, left mouse, right stick, Second: 2f, right mouse, left stick
        public struct DragUIEvent                           : IInputEvent { public InputData Data; public UnityEngine.Vector2 StartPos; }
        public struct DragOtherEvent                        : IInputEvent { public InputData Data; public UnityEngine.Vector2 StartPos; }

        // Holds (mouse long press - no move, touch long press - no move, button long press - no move )
        public struct HoldUIEvent                           : IInputEvent { public InputData Data; }
        public struct HoldOtherEvent                        : IInputEvent { public InputData Data; }

        // DoubleClick, Double Tap, (no controller pendant)
        public struct DoubleClickUIEvent                    : IInputEvent { public InputData Data; }
        public struct DoubleClickOtherEvent                 : IInputEvent { public InputData Data; }

        // Specifics - Pinch (determine that its no secondary drag or pinch!)
        // also controller stick abstracts to pinch to move fwd!
        public struct PinchUIEvent                          : IInputEvent { public InputData Data; public float PinchDistance; }
        public struct PinchOtherEvent                       : IInputEvent { public InputData Data; public float PinchDistance; }
        
        // Specifics - Scroll Wheel
        public struct MouseScrollUIEvent                    : IInputEvent { public InputData Data; }
        public struct MouseScrollOtherEvent                 : IInputEvent { public InputData Data; }
        
        // Specifics - Rotate (determine that its no secondary drag or pinch!)
        public struct TouchRotateUIEvent                    : IInputEvent { public InputData Data; public float RotationAngle; }
        public struct TouchRotateOtherEvent                 : IInputEvent { public InputData Data; public float RotationAngle; }

        // Specifics - Thumbsticks ( ? nec bc a drag could become a gizmo drag via stick...)
        // public struct ThumbstickLeftUIEvent                 : IInputEvent { public InputData Data; }
        // public struct ThumbstickLeftOtherEvent              : IInputEvent { public InputData Data; }
        // public struct ThumbstickRightUIEvent                 : IInputEvent { public InputData Data; }
        // public struct ThumbstickRightOtherEvent              : IInputEvent { public InputData Data; }
        //... other specifics (GController-Trigger, ...)

        #region Specific Events

        public struct AttitudeInputEvent                    : IInputEvent { public InputData Data; public UnityEngine.Quaternion Rotation;}
        //from AR module, subscribe for switch also ui stuff on start/end!
        public struct ARInputEvent                          : IInputEvent { public InputData Data; }
        
        // GPS
        // Consumers fire this to ask for GPS (so we do not run GPS all the time)
        public static event Action<GPSDemandType> OnGPSDemandChanged;
        public static void FireGPSDemand(GPSDemandType type) => OnGPSDemandChanged?.Invoke(type);
        // The GPSModule fires this when it has fresh data
        public struct GPSInputEvent                          : IInputEvent { public InputData Data; public GPSDataStruct GPSData;}

        #endregion

        // Special "shortcut" inputs (e.g. controller), that would normally be done via ui
        
        // cycle through ui elements (hover so we could select them)
        // cycle through specific ui (parameter - instant switch)
        // manipulate selected paramter (trigger + stick)
        // cycle through visible scene objects
        // ~ will not work convenient: timeline, measure

        // Direct manipulation events? so we can show all viz-helper like before.


        // **** EVENT HUB ****
        // save abos sorted by event-type
        private readonly Dictionary<Type, Delegate> _eventHub = new Dictionary<Type, Delegate>();

        public void Subscribe<T>(Action<T> callback) where T : IInputEvent{
            Type eventType = typeof(T);
            
            if (!_eventHub.ContainsKey(eventType)){
                _eventHub[eventType] = null;
            }

            // add method from module to our dict of abos
            _eventHub[eventType] = (Action<T>)_eventHub[eventType] + callback;
        }

        public void Unsubscribe<T>(Action<T> callback) where T : IInputEvent{
            Type eventType = typeof(T);
            if (_eventHub.ContainsKey(eventType)){
                _eventHub[eventType] = (Action<T>)_eventHub[eventType] - callback;
            }
        }

        // 3. a module (e.g. UnityInputModule) fires an event
        public void Publish<T>(T eventData) where T : IInputEvent{
            Type eventType = typeof(T);

            if (_eventHub.TryGetValue(eventType, out var action) && action != null){
                // invokes all subscribed methods and sends the eventData
                ((Action<T>)action).Invoke(eventData);
            }
        }

        // TODO: has to be reverted/set via prio, so that not some "low" modules reset it, if its overwritten by something higher!
        private bool camNavigationAllowed = true;
        public void SetAllowCamNavigation(bool allow){ camNavigationAllowed = allow; }
        public bool IsCamNavigationAllowed(){ return camNavigationAllowed; }

        private bool uiInteractionAllowed = true;
        public void SetUiInteraction(bool allow){ uiInteractionAllowed = allow; }
        public bool IsUiInteractionAllowed(){ return uiInteractionAllowed; }

        private bool isMultiTouchGestureAllowed = true; //needs to be disabled when on-screen joysticks are hit!
        public void SetMultiTouchGestures(bool allow){ isMultiTouchGestureAllowed = allow; }
        public bool IsMultiTouchGestureAllowed(){ return isMultiTouchGestureAllowed; }

        public void TriggerGPSOutput(bool continously = false){}

        //!
        //! Constructor initializing member variables.
        //!
        public InputManager(Type moduleType, Core tracerCore) : base(moduleType, tracerCore){
        }

        // --- HELPER METHODS FOR FIRING EVENTS ---
        public static InputData CreateData(InputTracker tracker, InputManager.InputState state) {
            return new InputData {
                Level = tracker.Level,
                State = state,
                // Device = InputDeviceType.Touch, // no differentiation yet
                Position = tracker.CurrentPosition,
                Delta = tracker.CurrentDelta
                // could also add rotation? or utilize pos+delta for performance?
            };
        }

        #region Specific Data

        public enum GPSDemandType { OneShot, StartContinuous, StopContinuous }

        public struct GPSDataStruct {
            public GPSDataStruct(float _lat, float _long, float _alt, float _accuracy, bool _valid, double _gpsTimestamp = 0) {
                latitude = _lat;
                longitude = _long;
                altitude = _alt;
                accuracy = _accuracy;
                valid = _valid;
                
                // Initialize variables before assigning via function
                minute = 0;
                hour = 0;
                day = 0;
                month = 0;

                // Populate the time fields
                CalculateTime(_gpsTimestamp);
            }
            public float latitude;
            public float longitude;
            public float altitude;
            public float accuracy;
            public bool valid;
            public int minute;
            public int hour;
            public int day;
            public int month;

            /// <summary>
            /// Populates hour, minute, day, and month. 
            /// Uses UTC time by default to ensure astronomical/solar math remains accurate across time zones.
            /// </summary>
            private void CalculateTime(double rawGpsTimestamp = 0) {
                DateTime targetTime;

                if (rawGpsTimestamp > 0) {
                    // 1. BEST PRACTICE: Convert Unity's hardware GPS timestamp (Unix Epoch seconds since 1970)
                    // This represents the exact moment the satellite sent the coordinate, regardless of delays.
                    DateTime epochStart = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    targetTime = epochStart.AddSeconds(rawGpsTimestamp);
                }else {
                    // 2. FALLBACK: Use the device's current clock.
                    // We use UtcNow instead of Now so your solar formulas don't break due to Daylight Savings or Time Zones!
                    targetTime = DateTime.UtcNow; 
                }

                minute = targetTime.Minute;
                hour = targetTime.Hour;
                day = targetTime.Day;
                month = targetTime.Month;
            }
        }

        #endregion
    }
}
