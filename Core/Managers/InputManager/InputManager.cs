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
//! @brief Implementation of the TRACER Input Manager, provides events to listen at for all modules
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @author Paulo Scatena
//! @author Thomas Krüger
//! @version 2
//! @date 12.08.2026
//! @revision revert to use class and events again instead of EventHub


using System;

namespace tracer{
    //!
    //! Class implementing the input manager, managing all user inupts and mapping.
    //!
    public class InputManager : Manager{

        #region INPUT EVENT ARGS

        //!
        //! what level of input is addressed within the raising module
        //!
        public enum InputLevel      { Primary, Secondary, Tertiary }
        //!
        //! what state of input is currently raised from a module
        //!
        public enum InputState      { Started, Ongoing, Ended, Canceled }

        //!
        //! base payload of every input event - holds what all inputs have in common
        //!
        //! Achtung: Unterschied Class im Vgl. zu vorherigem Struct!
        //! InputData-Struct (Stack, keine GC-Last). //! 
        //! Nun: jedes Event allokiert Objekt auf Heap. 
        //! Worst Case (FireDragEvent Ongoing) 3 Tracker × 60 fps ≈ 180 Allokationen/Sekunde à ~40 Byte, 
        //! also grob 5–10 KB/s. Unitys Gen0-GC steckt das normalerweise problemlos weg 
        //! – auf Mobile mit IL2CPP aber messbar (lange Drag Sessions),
        //! --> jetzt ignorieren, später: wiederverwendete Instanz pro Event-Typ je Modul - nur befüllen, nicht erzeugen
        public class InputEventArgs : EventArgs{
            public InputLevel Level;
            public InputState State;
            public UnityEngine.Vector2 Position;
            public UnityEngine.Vector2 Delta;
        }
        //!
        //! used as paylopad for AnyInputEvent
        //!        
        public class AnyEventArgs     : InputEventArgs{}
        //!
        //! click and double click need no extra data, also used for as Payload for AnyInputEvent
        //!        
        public class ClickEventArgs     : InputEventArgs{}
        //!
        //! drag additionally reports where the gesture originally started
        //!
        public class DragEventArgs      : InputEventArgs{ public UnityEngine.Vector2 StartPosition; }
        //!
        //! hold needs no extra data, but for understandings-sake we have this as an extra definition
        //!
        public class HoldEventArgs      : InputEventArgs{}
        //!
        //! signed distance change of this frame (positive = fingers spread)
        //!
        public class PinchEventArgs     : InputEventArgs{ public float PinchDelta; }
        //!
        //! signed angle change of this frame in degrees, used only within multitouch gesture for now
        //!
        public class RotateEventArgs    : InputEventArgs{ public float RotationDelta; }
        //!
        //! input data of the device's rotation
        //!
        public class AttitudeEventArgs  : InputEventArgs{ public UnityEngine.Quaternion Rotation; }
        //!
        //! gps data from the module, only send if it gets asked for data via OnGPSDemandChanged
        //!
        public class GPSEventArgs       : InputEventArgs{ public GPSDataStruct GPSData; }
        //!
        //! from AR module, subscribe for example switching ui-modes in start/end, has no data as well, see `HoldEventArgs`
        //! 
        public class AREventArgs        : InputEventArgs {}

        #endregion

        #region INPUT EVENTS
        //!
        //! fired when any input has started
        //! for example rendering the view into an rtx once (beforehand)
        //!
        public event EventHandler<AnyEventArgs> anyInputEvent;
        //!
        //! fired when a click interaction ended on top of 2D UI
        //!
        public event EventHandler<ClickEventArgs> clickUIEvent;
        //!
        //! fired when a click interaction ended on a 3D UI, a scene object or nothing at all
        //!
        public event EventHandler<ClickEventArgs> clickOtherEvent;
        //!
        //! fired when a drag interaction happens on top of 2D UI
        //!
        public event EventHandler<DragEventArgs> dragUIEvent;
        //!
        //! fired when a drag interaction happens on a 3D UI, a scene object or nothing at all
        //!
        public event EventHandler<DragEventArgs> dragOtherEvent;
        //!
        //! fired when a hold interaction happens on top of 2D UI
        //!
        public event EventHandler<HoldEventArgs> holdUIEvent;
        //!
        //! fired when a hold interaction happens on a 3D UI, a scene object or nothing at all
        //!
        public event EventHandler<HoldEventArgs> holdOtherEvent;
         //!
        //! fired when a double-click interaction ended on top of 2D UI
        //!
        public event EventHandler<ClickEventArgs> doubleClickUIEvent;
        //!
        //! fired when a double-click interaction ended on a 3D UI, a scene object or nothing at all
        //!
        public event EventHandler<ClickEventArgs> doubleClickOtherEvent;
        //!
        //! fired when a pinch interaction happens on top of 2D UI
        //!
        public event EventHandler<PinchEventArgs> pinchUIEvent;
        //!
        //! fired when a pinch interaction happens on a 3D UI, a scene object or nothing at all
        //!
        public event EventHandler<PinchEventArgs> pinchOtherEvent;
        //!
        //! fired when a rotate interaction (multi touch) happens on top of 2D UI
        //!
        public event EventHandler<RotateEventArgs> rotateUIEvent;
        //!
        //! fired when a rotate interaction (multi touch) happens on a 3D UI, a scene object or nothing at all
        //!
        public event EventHandler<RotateEventArgs> rotateOtherEvent;
        //!
        //! fires an attitude event, device sensors are never layer dependent
        //!
        public event EventHandler<AttitudeEventArgs> attitudeEvent;
        //!
        //! fires an gps event, device sensors are never layer dependent
        //!
        public event EventHandler<GPSEventArgs> gpsEvent;
        //!
        //! fires an ar event, device sensors are never layer dependent
        //!
        public event EventHandler<AREventArgs> arEvent;
        #endregion

        #region EVENT RAISERS
        //!
        //! raise anyInputEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        public void RaiseAnyInput   (object sender, AnyEventArgs e)   { anyInputEvent?.Invoke(sender, e); }
        //!
        //! raise clickUIEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        public void RaiseClickUI    (object sender, ClickEventArgs e)   { clickUIEvent?.Invoke(sender, e); }
        //!
        //! raise clickOtherEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        public void RaiseClickOther (object sender, ClickEventArgs e)   { clickOtherEvent?.Invoke(sender, e); }
        //!
        //! raise dragUIEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        public void RaiseDragUI     (object sender, DragEventArgs e)    { dragUIEvent?.Invoke(sender, e); }
        //!
        //! raise dragOtherEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        public void RaiseDragOther  (object sender, DragEventArgs e)    { dragOtherEvent?.Invoke(sender, e); }
        //!
        //! raise holdUIEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        public void RaiseHoldUI     (object sender, HoldEventArgs e)    { holdUIEvent?.Invoke(sender, e); }
        //!
        //! raise holdOtherEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        public void RaiseHoldOther  (object sender, HoldEventArgs e)    { holdOtherEvent?.Invoke(sender, e); }
        //!
        //! raise doubleClickUIEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        public void RaiseDoubleClickUI     (object sender, ClickEventArgs e)    { doubleClickUIEvent?.Invoke(sender, e); }
        //!
        //! raise doubleClickOtherEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        public void RaiseDoubleClickOther  (object sender, ClickEventArgs e)    { doubleClickOtherEvent?.Invoke(sender, e); }
        //!
        //! raise pinchUIEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        public void RaisePinchUI    (object sender, PinchEventArgs e)    { pinchUIEvent?.Invoke(sender, e); }
        //!
        //! raise pinchOtherEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        public void RaisePinchOther (object sender, PinchEventArgs e)    { pinchOtherEvent?.Invoke(sender, e); }
        //!
        //! raise rotateUIEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        public void RaiseRotateUI   (object sender, RotateEventArgs e)    { rotateUIEvent?.Invoke(sender, e); }
        //!
        //! raise rotateOtherEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        public void RaiseRotateOther(object sender, RotateEventArgs e)    { rotateOtherEvent?.Invoke(sender, e); }
        //!
        //! raise attitudeEvent, layer independent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        public void RaiseAttitude   (object sender, AttitudeEventArgs e){ attitudeEvent?.Invoke(sender, e); }
        //!
        //! raise gpsEvent, layer independent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        public void RaiseGPS        (object sender, GPSEventArgs e)     { gpsEvent?.Invoke(sender, e); }
        //!
        //! raise arEvent, layer independent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        public void RaiseAR        (object sender, AREventArgs e)     { arEvent?.Invoke(sender, e); }
        #endregion


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


        #region Specific Data

        public enum GPSDemandType { OneShot, StartContinuous, StopContinuous }

        // GPS
        // Consumers fire this to ask for GPS (so we do not run GPS all the time)
        public static event Action<GPSDemandType> OnGPSDemandChanged;
        //!
        //! call this from any module to trigger (if available) the RaiseGPS function here from the GPSModule
        //!
        public static void FireGPSDemand(GPSDemandType type) => OnGPSDemandChanged?.Invoke(type);

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

    #region Tracking Input Data

    //used by UnityInputModule and ControllerModule
    //could be put elsewhere + remove UnityDependency!
    public class InputTracker{
        public InputManager.InputLevel Level;   //primary, secondary, tertiary
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

        public InputTracker(InputManager.InputLevel level){ Level = level; }
        public void Reset(){ 
            State = InteractionState.Idle; 
            IsMuted = false; 
            CurrentPosition = UnityEngine.Vector2.zero;
            CurrentDelta = UnityEngine.Vector2.zero; 
            TimeDown = 0f;
            StartPosition = UnityEngine.Vector2.zero;
        }

        //!
        //! creates event args of the requested type and fills everything all inputs have in common
        //! type specific fields (StartPosition, PinchDelta, ...) are set by the caller afterwards
        //! helper method to increase readability in UnityInputModule and ControllerModule - because both need it, we put it here
        //!
        public static T ToArgs<T>(InputManager.InputLevel level, InputManager.InputState state, UnityEngine.Vector2 position, UnityEngine.Vector2 delta = default) where T : InputManager.InputEventArgs, new(){
            return new T{ Level = level, State = state, Position = position, Delta = delta };
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
    #endregion
}
