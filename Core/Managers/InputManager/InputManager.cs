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
        public class AnyEventArgs     : InputEventArgs {
            public AnyEventArgs() {
                // no data needed
            }
        }
        //!
        //! click and double click need no extra data, also used for as Payload for AnyInputEvent
        //!        
        public class ClickEventArgs     : InputEventArgs {
            public ClickEventArgs(InputLevel _level, InputState _state, UnityEngine.Vector2 _position, UnityEngine.Vector2 _delta = default) {
                Level = _level; State = _state; Position = _position; Delta = _delta;
            }
        }
        //!
        //! drag additionally reports where the gesture originally started
        //!
        public class DragEventArgs      : InputEventArgs{ 
            public UnityEngine.Vector2 StartPosition; 
            public DragEventArgs(InputLevel _level, InputState _state, UnityEngine.Vector2 _position, UnityEngine.Vector2 _delta, UnityEngine.Vector2 _startPosition) {
                Level = _level; State = _state; Position = _position; Delta = _delta;
                StartPosition = _startPosition;
            }    
        }
        //!
        //! hold needs no extra data, but for understandings-sake we have this as an extra definition
        //!
        public class HoldEventArgs      : InputEventArgs {
            public HoldEventArgs(InputLevel _level, InputState _state, UnityEngine.Vector2 _position, UnityEngine.Vector2 _delta = default) {
                Level = _level; State = _state; Position = _position; Delta = _delta;
            }
        }
        //!
        //! signed distance change of this frame (positive = fingers spread)
        //!
        public class PinchEventArgs     : InputEventArgs{ 
            public float PinchDelta; 
            public PinchEventArgs(InputLevel _level, InputState _state, UnityEngine.Vector2 _position, UnityEngine.Vector2 _delta, float _pinchDelta) {
                Level = _level; State = _state; Position = _position; Delta = _delta;
                PinchDelta = _pinchDelta;
            } 
        }
        //!
        //! signed angle change of this frame in degrees, used only within multitouch gesture for now
        //!
        public class RotateEventArgs    : InputEventArgs{ 
            public float RotationDelta; 
            public RotateEventArgs(InputLevel _level, InputState _state, UnityEngine.Vector2 _position, UnityEngine.Vector2 _delta, float _rotationDelta) {
                Level = _level; State = _state; Position = _position; Delta = _delta;
                RotationDelta = _rotationDelta;
            }
        }
        //!
        //! input data of the device's rotation
        //!
        public class AttitudeEventArgs  : InputEventArgs{ 
            public UnityEngine.Quaternion Rotation; 
            public AttitudeEventArgs(InputLevel _level, InputState _state, UnityEngine.Vector2 _position, UnityEngine.Vector2 _delta, UnityEngine.Quaternion _rotation) {
                Level = _level; State = _state; Position = _position; Delta = _delta;
                Rotation = _rotation;
            }
        }
        //!
        //! gps data from the module, only send if it gets asked for data via OnGPSDemandChanged
        //!
        public class GPSEventArgs       : InputEventArgs{ 
            public GPSDataStruct GPSData; 
            public GPSEventArgs(InputLevel _level, InputState _state, UnityEngine.Vector2 _position, UnityEngine.Vector2 _delta, GPSDataStruct _gpsData) {
                Level = _level; State = _state; Position = _position; Delta = _delta;
                GPSData = _gpsData;
            }
        }
        //!
        //! from AR module, subscribe for example switching ui-modes in start/end, has no data as well, see `HoldEventArgs`
        //! 
        public class AREventArgs        : InputEventArgs {
            public AREventArgs() {}
        }

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
        //! @param sender the original sender of that call
        //! @param e the InputEventArgs specified for this type
        public void RaiseAnyInput   (object sender, AnyEventArgs e)   { anyInputEvent?.Invoke(sender, e); }
        //!
        //! raise clickUIEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        //! @param sender the original sender of that call
        //! @param e the InputEventArgs specified for this type
        public void RaiseClickUI    (object sender, ClickEventArgs e)   { clickUIEvent?.Invoke(sender, e); }
        //!
        //! raise clickOtherEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        //! @param sender the original sender of that call
        //! @param e the InputEventArgs specified for this type
        public void RaiseClickOther (object sender, ClickEventArgs e)   { clickOtherEvent?.Invoke(sender, e); }
        //!
        //! raise dragUIEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        //! @param sender the original sender of that call
        //! @param e the InputEventArgs specified for this type
        public void RaiseDragUI     (object sender, DragEventArgs e)    { dragUIEvent?.Invoke(sender, e); }
        //!
        //! raise dragOtherEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        //! @param sender the original sender of that call
        //! @param e the InputEventArgs specified for this type
        public void RaiseDragOther  (object sender, DragEventArgs e)    { dragOtherEvent?.Invoke(sender, e); }
        //!
        //! raise holdUIEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        //! @param sender the original sender of that call
        //! @param e the InputEventArgs specified for this type
        public void RaiseHoldUI     (object sender, HoldEventArgs e)    { holdUIEvent?.Invoke(sender, e); }
        //!
        //! raise holdOtherEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        //! @param sender the original sender of that call
        //! @param e the InputEventArgs specified for this type
        public void RaiseHoldOther  (object sender, HoldEventArgs e)    { holdOtherEvent?.Invoke(sender, e); }
        //!
        //! raise doubleClickUIEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        //! @param sender the original sender of that call
        //! @param e the InputEventArgs specified for this type
        public void RaiseDoubleClickUI     (object sender, ClickEventArgs e)    { doubleClickUIEvent?.Invoke(sender, e); }
        //!
        //! raise doubleClickOtherEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        //! @param sender the original sender of that call
        //! @param e the InputEventArgs specified for this type
        public void RaiseDoubleClickOther  (object sender, ClickEventArgs e)    { doubleClickOtherEvent?.Invoke(sender, e); }
        //!
        //! raise pinchUIEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        //! @param sender the original sender of that call
        //! @param e the InputEventArgs specified for this type
        public void RaisePinchUI    (object sender, PinchEventArgs e)    { pinchUIEvent?.Invoke(sender, e); }
        //!
        //! raise pinchOtherEvent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        //! @param sender the original sender of that call
        //! @param e the InputEventArgs specified for this type
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
        //! @param sender the original sender of that call
        //! @param e the InputEventArgs specified for this type
        public void RaiseRotateOther(object sender, RotateEventArgs e)    { rotateOtherEvent?.Invoke(sender, e); }
        //!
        //! raise attitudeEvent, layer independent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        //! @param sender the original sender of that call
        //! @param e the InputEventArgs specified for this type
        public void RaiseAttitude   (object sender, AttitudeEventArgs e){ attitudeEvent?.Invoke(sender, e); }
        //!
        //! raise gpsEvent, layer independent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        //! @param sender the original sender of that call
        //! @param e the InputEventArgs specified for this type
        public void RaiseGPS        (object sender, GPSEventArgs e)     { gpsEvent?.Invoke(sender, e); }
        //!
        //! raise arEvent, layer independent
        //! only the input producing modules (UnityInputModule, ControllerModule, GPSModule, ...) call these
        //! sender is the producing module, so consumers can tell WHERE an input came from
        //!
        //! @param sender the original sender of that call
        //! @param e the InputEventArgs specified for this type
        public void RaiseAR        (object sender, AREventArgs e)     { arEvent?.Invoke(sender, e); }
        #endregion


        // TODO: has to be reverted, maybe set via prio, so that not some "low" modules reset it, if its overwritten by something higher!

        //!
        //! do we currently allow camera navigation via input events?
        //!
        private bool camNavigationAllowed = true;
        //!
        //! allow/deny camera navigation via input events 
        //!
        //! @param allow sets `camNavigationAllowed` to its value
        public void SetAllowCamNavigation(bool allow){ camNavigationAllowed = allow; }
        //!
        //! check if camera navigation via input events are currently allowed
        //!
        //! @return `camNavigationAllowed`
        public bool IsCamNavigationAllowed(){ return camNavigationAllowed; }

        //!
        //! do we currently allow ui-interaction via input events?
        //!
        private bool uiInteractionAllowed = true;
        //!
        //! allow/deny ui-interaction via input events 
        //!
        //! @param allow sets `uiInteractionAllowed` to its value
        public void SetUiInteraction(bool allow){ uiInteractionAllowed = allow; }
        //!
        //! check if ui-interaction via input events are currently allowed
        //!
        //! @return `uiInteractionAllowed`
        public bool IsUiInteractionAllowed(){ return uiInteractionAllowed; }

        //!
        //! do we currently allow multi-touch-gestures via input events?
        //!
        //! @remark isMultiTouchGestureAllowed have to be false, when on-screen joysticks are hit!
        //!
        private bool isMultiTouchGestureAllowed = true;
        //!
        //! allow/deny multi-touch-gestures where they are checked
        //!
        //! @param allow sets `isMultiTouchGestureAllowed` to its value
        public void SetMultiTouchGestures(bool allow){ isMultiTouchGestureAllowed = allow; }
        //!
        //! check if multi-touch-gestures are allowed
        //!
        //! @return `isMultiTouchGestureAllowed`
        public bool IsMultiTouchGestureAllowed(){ return isMultiTouchGestureAllowed; }


        #region SPECIFIC GPS

        //!
        //! enum as Payloads to set what kind of gps demand we have
        //!
        public enum GPSDemandType { OneShot, StartContinuous, StopContinuous }

        //! 
        //! Consumers that want GPS Data fire this via `onGPSDemandChangedEvent` to ask for GPS (so we do not run GPS all the time)
        //!
        public event EventHandler<GPSDemandType> onGPSDemandChangedEvent;
        //!
        //! call this from any module to trigger (if available) the RaiseGPS function here from the GPSModule
        //!
        public void RaiseGPSDemand(object sender, GPSDemandType gpsDemanyType){ onGPSDemandChangedEvent?.Invoke(sender, gpsDemanyType); }

        //!
        //! the gps data we use as payload for events. defined here - without module reference!
        //! struct so we don't allocate garbage
        //!
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

            //!
            //! Populates hour, minute, day, and month. 
            //! Uses UTC time by default to ensure astronomical/solar math remains accurate across time zones.
            //!
            //! @param rawGpsTimestamp in ms from 1970
            //!
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

        //!
        //! Constructor initializing member variables.
        //!
        //! @param  moduleType  type of modules to be loaded by this manager
        //! @param tracerCore A reference to the TRACER _core.
        //!
        public InputManager(Type moduleType, Core tracerCore) : base(moduleType, tracerCore){
        }

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
