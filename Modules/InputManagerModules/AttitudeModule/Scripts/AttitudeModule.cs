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

//! @file "AttitudeModule.cs"
//! @brief implementation of TRACER device specific attitude input
//! @author Thomas Krüger
//! @version 0
//! @date 19.05.2026

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace tracer{
    //!
    //! implementation of TRACER attitude sensore navigation
    //!
    public class AttitudeModule : InputManagerModule{

        //!
        //! Constructor.
        //!
        //! @param name Name of this module.
        //! @param _core Reference to the TRACER _core.
        //!
        public AttitudeModule(string name, Manager manager) : base(name, manager){
        }

        //!
        //! Destructor, cleaning up event registrations. 
        //!
        public override void Dispose(){
            base.Dispose();
            core.updateEvent -= OnCoreUpdateEvent;
            core.getManager<UIManager>().cameraControlChanged -= CameraControlBehaviourChanged;
            //manager.Unsubscribe<InputManager.ARInputEvent>(ARInputFunction);
        }

        //!
        //! We create a custom action entirely in code, no Asset required, checking Attitude input
        //!
        private InputAction attitudeInputAction;
        //!
        //! A reference to the attitude button. To e.g. disable functionality during ar-mode
        //!
        private MenuButton m_attitudeButton;
        //!
        //! bool for reading the sensor values only when active
        //!
        private bool sensorIsReading = false;

        //! used as data for the inputmanager event as in the UnityInputModule
        private InputManager.AttitudeEventArgs attitudeInputData;
        private int discardedFudgeValues = 0;
        private const int StartFramesToDiscard = 5;
        //! 
        //! Init m_callback
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Init(object sender, EventArgs e){

            // listening to functionality that should dissallow activating this behaviour
            core.getManager<UIManager>().cameraControlChanged += CameraControlBehaviourChanged;

            //-> subscribe to InputManager AR Event and handle ourself to be deactivated if other mode is on
            //manager.Subscribe<InputManager.ARInputEvent>(ARInputFunction);

            //creating class once, reduce Garbage Collection
            attitudeInputData = InputTracker.ToArgs<InputManager.AttitudeEventArgs>(
                InputManager.InputLevel.Primary,
                InputManager.InputState.Canceled,
                Vector2.zero,
                Vector2.zero
            );

            EnableAttitudeSensor();
        }

        private void EnableAttitudeSensor(){
            if (AttitudeSensor.current != null){
                ShowAttitudeUI(true);
                SetupAttitudeInputAction();
                core.updateEvent += OnCoreUpdateEvent;
            }else
                Helpers.Log("No attitude sensor found, feature will not be available.", Helpers.logMsgType.WARNING);
        }

        private void ShowAttitudeUI(bool show){
            if (AttitudeSensor.current == null)
                return;

            if (!show){
                if(m_attitudeButton != null){
                    //should be greyed out, instead of removing it(?)
                    core.getManager<UIManager>().removeButton(m_attitudeButton);
                    m_attitudeButton = null;
                }
            }else if (show) {
                if(m_attitudeButton == null) {
                    m_attitudeButton = new MenuButton("", Event_SwitchAttitudeButton);
                    m_attitudeButton.setIcon("Images/button_attitude"); //how is the order set up?
                    core.getManager<UIManager>().addButton(m_attitudeButton);
                }
            }
        }

        private void StopAttitude() {
            if (sensorIsReading) {
                InputSystem.DisableDevice(AttitudeSensor.current);
                attitudeInputAction.Disable();
                sensorIsReading = false;
                StopAttitudeEvent();
            }
        }

        //!
        //! setup the unity input action via code
        //!
        private void SetupAttitudeInputAction() {
            attitudeInputAction = new InputAction(
                name: "ReadAttitude",
                type: InputActionType.Value,
                binding: "<AttitudeSensor>/attitude",
                expectedControlType: "Quaternion"
            );
        }

        //!
        //! Button callback that toggles the main camera rotation overwrite by attitude sensor.
        //!
        private void Event_SwitchAttitudeButton(){
            if (AttitudeSensor.current == null) return;

            if (!sensorIsReading){
                // Crucial for performance/battery: Power up the hardware sensor
                InputSystem.EnableDevice(AttitudeSensor.current);
                attitudeInputAction.Enable();

                StartAttitudeEvent();
                
            }else{
                // Power down the hardware sensor and stop the action
                StopAttitude();
            }
        }

        private void StartAttitudeEvent() {
            attitudeInputData.State = InputManager.InputState.Started;
            discardedFudgeValues = 0;
            sensorIsReading = true;
        }

        private void StopAttitudeEvent() {
            attitudeInputData.State = InputManager.InputState.Ended;
            manager.RaiseAttitude(this, attitudeInputData);
        }

        //!
        //! Callback from TRACER _core when Unity calls it's render update
        //!
        private void OnCoreUpdateEvent(object sender, EventArgs e){
            // polling only when active
            if (!sensorIsReading) return;

            // Read the value directly
            Quaternion currentAttitude = attitudeInputAction.ReadValue<Quaternion>();

            //ignore the "wake-up lag" and not initialized values
            if(discardedFudgeValues < StartFramesToDiscard || (currentAttitude.x == 0 && currentAttitude.y == 0 && currentAttitude.z == 0 && currentAttitude.w == 0)){
                discardedFudgeValues++;
                return;
            }

            if(attitudeInputData.State == InputManager.InputState.Started) {
                //if still started, publish here started and go to ongoing then (do not distribute started with zero value)
                attitudeInputData.Rotation = currentAttitude;
                manager.RaiseAttitude(this, attitudeInputData);
                attitudeInputData.State = InputManager.InputState.Ongoing;
            }else{
                // Example: Apply to camera or object (Note: You may need to adapt the coordinate system 
                // depending on your device orientation, often requiring a 90-degree rotation adjustment).
                // transform.rotation = currentAttitude;
                attitudeInputData.Rotation = currentAttitude;
                manager.RaiseAttitude(this, attitudeInputData);
            }
        }

        //!
        //! Method to update menu button based on camera control
        //! @param sender callback sender
        //! @param c event reference
        //!
        private void CameraControlBehaviourChanged(object sender, UIManager.CameraControl c) {
            switch (c) {
                case UIManager.CameraControl.AR:
                    //remove button!
                    StopAttitude();
                    ShowAttitudeUI(false);
                    break;
                case UIManager.CameraControl.ATTITUDE:
                    //nothing to do
                    break;
                case UIManager.CameraControl.STANDARD:
                    //add button if button not available (and attitude available)
                    ShowAttitudeUI(true);
                    break;
            }
        }
    }

}
