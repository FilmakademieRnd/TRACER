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
            m_manager.core.updateEvent -= OnCoreUpdateEvent;
            manager.Unsubscribe<InputManager.ARInputEvent>(ARInputFunction);
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
        private InputManager.InputData attitudeInputData;
        //! 
        //! Init m_callback
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Init(object sender, EventArgs e){

            m_manager.core.updateEvent += OnCoreUpdateEvent;

            // listening to functionality that should dissallow activating this behaviour
            // (ar-module for example - without having a direct reference!)
            // ((would only work independent via an arbitrary priority management))

            //-> subscribe to InputManager AR Event and handle ourself to be deactivated if other mode is on
            manager.Subscribe<InputManager.ARInputEvent>(ARInputFunction);

            EnableAttitudeSensor();
        }

        //!
        //! Function to disable ourself, if AR functionality got active
        //!
        //! @param evt the InputData
        //!
        private void ARInputFunction(InputManager.ARInputEvent evt){

            switch (evt.Data.Level) {
                case InputManager.InputLevel.Primary:
                    switch (evt.Data.State){
                        case InputManager.InputState.Started:
                            StopAttitude();
                            SetAttitudeUI(false);
                            break;
                        case InputManager.InputState.Ongoing:
                            break;
                        case InputManager.InputState.Canceled:
                        case InputManager.InputState.Ended:
                            SetAttitudeUI(true);
                            break;
                    }
                    break; 
            } 
        }

        private void EnableAttitudeSensor(){
            if (AttitudeSensor.current != null){
                SetAttitudeUI(true);
                SetupAttitudeInputAction();
            }else
                Helpers.Log("No attitude sensor found, feature will not be available.", Helpers.logMsgType.WARNING);
        }

        private void SetAttitudeUI(bool show){
            if (AttitudeSensor.current == null)
                return;

            if (!show && m_attitudeButton != null){
                //should be greyed out, instead of removing it!
                core.getManager<UIManager>().removeButton(m_attitudeButton);
            }else if (show) {
                if(m_attitudeButton == null) {
                    m_attitudeButton = new MenuButton("", SwitchAttitudeCamControl);
                    m_attitudeButton.setIcon("Images/button_attitude"); //how is the order set up?
                }
                core.getManager<UIManager>().addButton(m_attitudeButton);
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
        private void SwitchAttitudeCamControl(){
            if (AttitudeSensor.current == null) return;

            if (!sensorIsReading){
                // Crucial for performance/battery: Power up the hardware sensor
                InputSystem.EnableDevice(AttitudeSensor.current);
                attitudeInputAction.Enable();
                sensorIsReading = true;

                StartAttitudeEvent();
            }else{
                // Power down the hardware sensor and stop the action
                StopAttitude();
            }
        }

        private void StartAttitudeEvent() {
            attitudeInputData = new InputManager.InputData {
                Level = InputManager.InputLevel.Primary,
                State = InputManager.InputState.Started,
                Position = Vector2.zero,
                Delta = Vector2.zero
            };

            manager.Publish(new InputManager.AttitudeInputEvent { Data = attitudeInputData, Rotation = Quaternion.identity });
        }

        private void StopAttitudeEvent() {
            attitudeInputData = new InputManager.InputData {
                Level = InputManager.InputLevel.Primary,
                State = InputManager.InputState.Ended,
                Position = Vector2.zero,
                Delta = Vector2.zero
            };

            manager.Publish(new InputManager.AttitudeInputEvent { Data = attitudeInputData, Rotation = Quaternion.identity });
        }

        //!
        //! Callback from TRACER _core when Unity calls it's render update
        //!
        private void OnCoreUpdateEvent(object sender, EventArgs e){
            // polling only when active
            if (!sensorIsReading) return;

            // Read the value directly
            Quaternion currentAttitude = attitudeInputAction.ReadValue<Quaternion>();

            //we probably have some latency at the start, that's why we have a rotation "jump" - ignore it with this
            if(currentAttitude == Quaternion.identity)
                return;

            attitudeInputData.State = InputManager.InputState.Ongoing;
            
            // Example: Apply to camera or object (Note: You may need to adapt the coordinate system 
            // depending on your device orientation, often requiring a 90-degree rotation adjustment).
            // transform.rotation = currentAttitude;

            manager.Publish(new InputManager.AttitudeInputEvent { Data = attitudeInputData, Rotation = currentAttitude });
        }
    }

}
