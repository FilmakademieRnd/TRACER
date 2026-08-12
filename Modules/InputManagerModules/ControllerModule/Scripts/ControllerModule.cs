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


//! @file "ControllerModule.cs"
//! @brief Controller input 
//! @author Alexandru Schwartz
//! @author Thomas Krüger
//! @version 1
//! @date 25.06.2026
//! @note revise behaviour, should be implemented more convenient and abstract with our new input behaviour
//! @note2 should the controller be an equal input behaviour as mouse/touch or just implement generics (move, look, select)?

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace tracer{

    public class ControllerModule : InputManagerModule{

        //!
        //! Reference to the instantiated controller canvas
        //!
        private GameObject _controllerCrosshairCanvas;
        //!
        //! Instead of checking for other input, we simply switch the croshair off after a certain time w/o input
        //!
        private float crosshairOffTimer = -1f;
        //!
        //! Reference to the currently selected scene object
        //!
        private SceneObject _currentSelectedSceneObject, _previousSelectedSceneObject;

        //!
        //! Reference to the selector SnapSelect component
        //!
        private SnapSelect _selectorSnapSelect;

        //!
        //! Reference to the color select component
        //!
        private ColorSelect _colorSelect;

        //!
        //! Reference to the UIManager
        //!
        private UIManager _uiManager;

        //!
        //! Reference to the SceneManager
        //!
        private SceneManager _sceneManager;

        //!
        //! List of elements in the selector SnapSelect
        //!
        private List<SnapSelectElement> _selectorSnapSelectElementsList;

        //!
        //! The index of the currently selected SnapSelect element in the selector
        //!
        private int _selectorCurrentSelectedSnapSelectElement = 0;


        //!
        //! The currently selected abstract parameter
        //!
        private AbstractParameter _selectedAbstractParam;

        //!
        //! Event handler for controller editing completion
        //!
        public event EventHandler<AbstractParameter> ControllerdoneEditing;

        #region NEW INPUT MANAGER ADJUSTMENT

        private enum ControllerModeEnum {
            Viewing = 0,
            Manipulation = 1,
            //could be very abstract and simply check if manipulation parameter is float, v2, v3, color and use the sticks according to it
            //right now: do the simple manual way, until we know how the ui will be
            Manip_Translate = 10,
            Manip_Rotate = 11,
            Manip_Scale = 12,
            Manip_Cam = 20,
            Manip_Light = 30,
            Manip_Color = 40,
            Manip_SingleValue = 50,  //hovered over e.g. the x value from position
            Manip_Vec2 = 53,    //e.g. sensorSize
            Manip_ListParameter = 56    //e.g. SensorSizes
        }
        private ControllerModeEnum controlMode = ControllerModeEnum.Viewing;
        
        //!
        //! The generated Unity input class defining all available user inputs.
        //!
        private Inputs m_inputs;
        
        //! left and right sticks
        private float stickDeadzone = 0.15f; 
        // Converts normalized stick input (-1 to 1) into simulated pixel deltas.
        private float syntheticSensitivity = 500f; 
        private bool _isLeftStickDragging, _isRightStickDragging = false;
        //! dpad
        private float dpadDeadzone = 0.5f;
        private Selectable _currentSelection;
        private Image _currentSelectionImage;
        private Color _selectionColorWas;
        private bool _dpadInUse = false;
        //! triggers
        private float triggerDeadzone = 0.1f;
        private bool _isLeftTriggerHold, _isRightTriggerHold = false;
        private bool inOrbitMode = false;
        //! our input trackers for abstract utilization
        private InputTracker _primary   = new InputTracker(InputManager.InputLevel.Primary);
        private InputTracker _secondary   = new InputTracker(InputManager.InputLevel.Secondary);
        private InputTracker _tertiary   = new InputTracker(InputManager.InputLevel.Tertiary);
        #endregion


        //!
        //! Initialization method for the controller.
        //!
        protected override void Init(object sender, EventArgs e){
            // Get the scene manager from the _core.
            _sceneManager = core.getManager<SceneManager>();
            
            // Get the UI manager from the _core.
            _uiManager = core.getManager<UIManager>();

            // TODO: ban module references!
            // Subscribe to the ControllerdoneEditing event.
            ControllerdoneEditing += _sceneManager.getModule<UndoRedoModule>().addHistoryStep;

            //SHOW CORSHAIR
            _controllerCrosshairCanvas = UnityEngine.Object.Instantiate(Resources.Load("Prefabs/ControllerCanvas") as GameObject, Camera.main.transform);
            _controllerCrosshairCanvas.SetActive(false);

            //TODO: start controller peripherie check method
            //      do not subscribe to events if we have no controller!

            // Subscribe to the _core update event.
            core.updateEvent += TracerUpdate;

            //enable input
            m_inputs = new Inputs();
            //doing this in the update!
            m_inputs.VPETMap.Controller_South.canceled   += PressConfirm;
            m_inputs.VPETMap.Controller_East.canceled    += PressCancel;
            m_inputs.VPETMap.Controller_Select.canceled  += PressSelect;
            m_inputs.VPETMap.Controller_Left_Shoulder.canceled    += PressLeftShoulder;
            m_inputs.VPETMap.Controller_Right_Shoulder.canceled   += PressRightShoulder;
            m_inputs.VPETMap.Controller_Left_Stick_Press.canceled += PressLeftStick;
            m_inputs.VPETMap.Controller_Right_Stick_Press.canceled += PressRightStick;
            m_inputs.VPETMap.Enable();

            // Subscribe to UI manager events.
            _uiManager.selectionChanged += UiManagerSelectionChanged;
            //_uiManager.selectionRemoved += UiManagerSelectionRemoved;
            _uiManager.UI2DCreated += UiCreationFinished;
            _uiManager.colorSelectGameObject += GetColorSelect;
        }

        //!
        //! Cleanup method for the controller.
        //!
        public override void Dispose(){
            base.Dispose();

            // Unsubscribe from controller button events.
/*          manager.buttonNorth -= PressNorth;
            manager.buttonSouth -= PressSouth;
            manager.buttonEast -= PressEast;
            manager.buttonWest -= PressWest;
            manager.buttonUp -= PressUp;
            manager.buttonDown -= PressDown;
            manager.buttonLeft -= PressLeft;
            manager.buttonRight -= PressRight;
            manager.buttonLeftTrigger -= PressLeftTrigger;
            manager.buttonRightTrigger -= PressRightTrigger;
            manager.buttonLeftShoulder -= PressLeftShoulder;
            manager.buttonRighrShoulder -= PressRightShoulder;
            manager.leftControllerStick -= MoveLeftStick;
            manager.rightControllerStick -= MoveRightStick;
            manager.ControllerStickCanceled -= DoneEditing;
*/
            if(_selectorSnapSelect)
                _selectorSnapSelect.parameterChanged -= ParamChange;

            // Unsubscribe from the _core update event.
            core.updateEvent -= TracerUpdate;

            // Unsubscribe from UI manager events.
            _uiManager.selectionChanged -= UiManagerSelectionChanged;
            //_uiManager.selectionRemoved -= UiManagerSelectionRemoved;
            _uiManager.UI2DCreated      -= UiCreationFinished;
            _uiManager.colorSelectGameObject -= GetColorSelect;

            // Unsubscribe from the ControllerdoneEditing event.

            // [REVIEW]
            // Direct access to a module should be prevented!
            //ControllerdoneEditing -= _sceneManager.getModule<UndoRedoModule>().addHistoryStep;
        }
    

        public ControllerModule(string name, Manager manager) : base(name, manager){}

        #region PROCESSING
        private void ProcessRightStick(InputTracker _tracker, Vector2 rawStickInput) {
            // Evaluate Deadzone
            if (rawStickInput.magnitude > stickDeadzone) {
                
                // Synthesize the Touch Data
                // Multiply by deltaTime to make the panning frame-rate independent, 
                // just like a smooth finger drag.
                Vector2 syntheticDelta = rawStickInput * syntheticSensitivity * Time.deltaTime;
                
                // dont accidentaly trigger a gizmo drag!
                Vector2 ghostCursor = new Vector2(-9999f, -9999f);
                // Controllers don't have a screen position, so we anchor the action to the dead center of the screen.
                Vector2 screenCenter = ghostCursor; //new Vector2(Screen.width / 2f, Screen.height / 2f);

                // --- STARTED PHASE ---
                if (!_isRightStickDragging) {
                    _isRightStickDragging = true;
                    _controllerCrosshairCanvas.SetActive(true);
                    crosshairOffTimer = 10f;
                    
                    // Fire your specific DragOtherEvent
                    FireDragOtherEvent(_tracker, InputManager.InputState.Started, screenCenter, syntheticDelta);
                } 
                // --- ONGOING PHASE ---
                else {
                    FireDragOtherEvent(_tracker, InputManager.InputState.Ongoing, screenCenter, syntheticDelta);
                }
            }
            // Evaluate Stick Release
            else if (_isRightStickDragging) {
                // --- ENDED PHASE ---
                // The stick snapped back to the center (inside the deadzone)
                _isRightStickDragging = false;
                
                Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                
                // Delta is zero because the movement has stopped
                FireDragOtherEvent(_tracker, InputManager.InputState.Ended, screenCenter, Vector2.zero);
            }
        }

        //!
        //! similiar to ProcessLeftStick, but
        //! hor input is left/right move, ver is fwd/back (pinch)
        //!
        private void ProcessLeftStick(InputTracker _tracker, Vector2 rawStickInput) {
            Vector2 syntheticDelta;
            Quaternion parentRotation;
            float manipulationSpeed = Time.deltaTime;

            switch (controlMode) {
                case ControllerModeEnum.Viewing:
                    if (rawStickInput.magnitude > stickDeadzone) {
                        syntheticDelta = rawStickInput * syntheticSensitivity * Time.deltaTime;
                        Vector2 horDragDelta = -syntheticDelta; horDragDelta.y = 0;
                        float verPinchDelta = syntheticDelta.y;

                        // dont accidentaly trigger a gizmo drag!
                        Vector2 ghostCursor = new Vector2(-9999f, -9999f);
                        Vector2 screenCenter = ghostCursor; //new Vector2(Screen.width / 2f, Screen.height / 2f);
                        if (!_isLeftStickDragging) {
                            _isLeftStickDragging = true;
                            _controllerCrosshairCanvas.SetActive(true);
                            crosshairOffTimer = 10f;

                            FireDragOtherEvent(_tracker, InputManager.InputState.Started, screenCenter, horDragDelta);
                            FirePinchOtherEvent(_tracker, InputManager.InputState.Started, screenCenter, verPinchDelta);
                        }else {
                            FireDragOtherEvent(_tracker, InputManager.InputState.Ongoing, screenCenter, horDragDelta);
                            FirePinchOtherEvent(_tracker, InputManager.InputState.Ongoing, screenCenter, verPinchDelta);
                        }
                    }else if (_isLeftStickDragging) {
                        _isLeftStickDragging = false;
                        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                        FireDragOtherEvent(_tracker, InputManager.InputState.Ended, screenCenter, Vector2.zero);
                        FirePinchOtherEvent(_tracker, InputManager.InputState.Ended, screenCenter, 0f);
                    }
                    break;

                case ControllerModeEnum.Manip_Translate:
                    if (rawStickInput.magnitude < stickDeadzone)
                        return;
                    // Moves Object Local X / Z -> fire a drag event that emulates a hit at that gizmo part (this is prone to errors!)
                    // by just changing the parameter values, we dont have any of the "fancy ui" as when dragging though...
                    // -> either way, do like this for now (waiting for final ui/ux either way)
                    Parameter<Vector3> pos = (Parameter<Vector3>)_selectedAbstractParam;
                    syntheticDelta = rawStickInput * syntheticSensitivity * Time.deltaTime;
                    Vector3 manipulationVec;

                    parentRotation = _currentSelectedSceneObject.transform.parent ? _currentSelectedSceneObject.transform.parent.rotation : Quaternion.identity;

                    if(_uiManager.ManipulationLayer == UIManager.ManipulationLayerEnum.LOCAL)
                        manipulationVec = CalculateLocalPosition(_currentSelectedSceneObject.transform.localPosition, _currentSelectedSceneObject.transform.localRotation, syntheticDelta, manipulationSpeed);
                    else if(_uiManager.ManipulationLayer == UIManager.ManipulationLayerEnum.GLOBAL)
                        manipulationVec = CalculateGlobalPosition(_currentSelectedSceneObject.transform.localPosition, parentRotation, syntheticDelta, manipulationSpeed);
                    else
                        manipulationVec = CalculateCameraRelativePosition(_currentSelectedSceneObject.transform.localPosition, parentRotation, syntheticDelta, Camera.main.transform, manipulationSpeed);


                    //pos.setValue(_currentSelectedSceneObject.transform.position + (Vector3)(manipulationVec * syntheticDelta * Time.deltaTime));
                    pos.setValue(manipulationVec);
                    break;
                case ControllerModeEnum.Manip_Rotate:
                    if (rawStickInput.magnitude < stickDeadzone)
                        return;

                    manipulationSpeed = 10*Time.deltaTime;
                    // Rotate: Pitch / Roll
                    // -> either way, do like this for now (waiting for final ui/ux either way)
                    Parameter<Quaternion> rot = (Parameter<Quaternion>)_selectedAbstractParam;
                    syntheticDelta = rawStickInput * syntheticSensitivity * Time.deltaTime;
                    Quaternion manipulationRot;

                    parentRotation = _currentSelectedSceneObject.transform.parent ? _currentSelectedSceneObject.transform.parent.rotation : Quaternion.identity;

                    if(_uiManager.ManipulationLayer == UIManager.ManipulationLayerEnum.LOCAL)
                        manipulationRot = CalculateLocalRotation(_currentSelectedSceneObject.transform.localRotation, syntheticDelta, manipulationSpeed);
                    else if(_uiManager.ManipulationLayer == UIManager.ManipulationLayerEnum.GLOBAL)
                        manipulationRot = CalculateGlobalRotation(_currentSelectedSceneObject.transform.localRotation, parentRotation, syntheticDelta, manipulationSpeed);
                    else
                        manipulationRot = CalculateCameraRelativeRotation(_currentSelectedSceneObject.transform.localRotation, parentRotation, syntheticDelta, Camera.main.transform, manipulationSpeed);

                    rot.setValue(manipulationRot);
                    break;
                case ControllerModeEnum.Manip_Scale:
                    if (rawStickInput.magnitude < stickDeadzone)
                        return;

                    Parameter<Vector3> scale = (Parameter<Vector3>)_selectedAbstractParam;
                    syntheticDelta = rawStickInput * syntheticSensitivity * Time.deltaTime * manipulationSpeed;
                    Vector3 manipulationScale = _currentSelectedSceneObject.transform.localScale + (Vector3.one * syntheticDelta.y);

                    scale.setValue(manipulationScale);
                    break;
                case ControllerModeEnum.Manip_Color:
                    if (rawStickInput.magnitude < stickDeadzone || !_colorSelect)
                        return;

                    syntheticDelta = rawStickInput * syntheticSensitivity * Time.deltaTime * manipulationSpeed;
                    _colorSelect.controllerManipulator(new Vector3(syntheticDelta.x, syntheticDelta.y, 0));
                    break;
                case ControllerModeEnum.Manip_Vec2:
                    if (rawStickInput.magnitude < stickDeadzone)
                        return;

                    Parameter<Vector2> valueV2 = (Parameter<Vector2>)_selectedAbstractParam;
                    syntheticDelta = rawStickInput * syntheticSensitivity * Time.deltaTime * manipulationSpeed;
                    valueV2.setValue(valueV2.value + syntheticDelta);
                    break;
                case ControllerModeEnum.Manip_SingleValue:
                    if (rawStickInput.magnitude < stickDeadzone)
                        return;

                    Parameter<float> val = (Parameter<float>)_selectedAbstractParam;
                    syntheticDelta = rawStickInput * syntheticSensitivity * Time.deltaTime * manipulationSpeed;
                    val.setValue(val.value + syntheticDelta.y);
                    break;
                case ControllerModeEnum.Manip_ListParameter:
                case ControllerModeEnum.Manipulation:
                    if (rawStickInput.magnitude < stickDeadzone)
                        return;

                    //SPECIFIC BEHAVIOUR
                    //- Translate: Moves Object Local X / Z (Floor plane)

                    //- Rotate: Pitch / Roll

                    //- Scale: (Vertical): Uniform Scale

                    //- Other Modes, vertical: increase/decrease, fields e.g. color: x/y axis
                    break;
            }
        }

        //!
        //! left and right trigger, delta is inverse
        //!
        private void ProcessTrigger(InputTracker _tracker, float delta) {
            
            float manipulationSpeed = 10*Time.deltaTime;
            Quaternion parentRotation;
            
            switch (controlMode) {
                case ControllerModeEnum.Viewing:
                    //left/right trigger decision
                    if (Mathf.Abs(delta) > triggerDeadzone) {
                        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                        float speed = 4f;
                        if ((delta < 0 && !_isLeftTriggerHold) || (delta > 0 && !_isRightTriggerHold)) {
                            if(delta < 0) _isLeftTriggerHold = true; else _isRightTriggerHold = true;

                            FireDragOtherEvent(_tracker, InputManager.InputState.Started, screenCenter, Vector2.down*delta*speed);
                        }else {
                            FireDragOtherEvent(_tracker, InputManager.InputState.Ongoing, screenCenter, Vector2.down*delta*speed);
                        }
                    }else if ((delta < 0 && _isLeftTriggerHold) || (delta > 0 && _isRightTriggerHold)) {
                        if(delta < 0) _isLeftTriggerHold = false; else _isRightTriggerHold = false;

                        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                        FireDragOtherEvent(_tracker, InputManager.InputState.Ended, screenCenter, Vector2.zero);
                    }
                    break;

                case ControllerModeEnum.Manip_Translate:
                    //Y up/down
                    if (Mathf.Abs(delta) <= triggerDeadzone) 
                        return;

                    manipulationSpeed = 2*Time.deltaTime;
                    Parameter<Vector3> pos = (Parameter<Vector3>)_selectedAbstractParam;
                    Vector3 manipulationVec;

                    parentRotation = _currentSelectedSceneObject.transform.parent ? _currentSelectedSceneObject.transform.parent.rotation : Quaternion.identity;

                    if(_uiManager.ManipulationLayer == UIManager.ManipulationLayerEnum.LOCAL)
                        manipulationVec = CalculateLocalYPosition(_currentSelectedSceneObject.transform.localPosition, _currentSelectedSceneObject.transform.localRotation, delta, manipulationSpeed);
                    else if(_uiManager.ManipulationLayer == UIManager.ManipulationLayerEnum.GLOBAL)
                        manipulationVec = CalculateGlobalYPosition(_currentSelectedSceneObject.transform.localPosition, parentRotation, delta, manipulationSpeed);
                    else
                        manipulationVec = CalculateCameraRelativeYPosition(_currentSelectedSceneObject.transform.localPosition, parentRotation, Camera.main.transform, delta, manipulationSpeed);


                    pos.setValue(manipulationVec);
                    break;
                case ControllerModeEnum.Manip_Rotate:
                    // spin left/right
                    if (Mathf.Abs(delta) <= triggerDeadzone) 
                        return;
                    
                    Parameter<Quaternion> rot = (Parameter<Quaternion>)_selectedAbstractParam;
                    Quaternion manipulationRot;

                    parentRotation = _currentSelectedSceneObject.transform.parent ? _currentSelectedSceneObject.transform.parent.rotation : Quaternion.identity;

                    if(_uiManager.ManipulationLayer == UIManager.ManipulationLayerEnum.LOCAL)
                        manipulationRot = CalculateLocalYaw(_currentSelectedSceneObject.transform.localRotation, delta, manipulationSpeed);
                    else if(_uiManager.ManipulationLayer == UIManager.ManipulationLayerEnum.GLOBAL)
                        manipulationRot = CalculateGlobalYaw(_currentSelectedSceneObject.transform.localRotation, parentRotation, delta, manipulationSpeed);
                    else
                        manipulationRot = CalculateCameraRelativeYaw(_currentSelectedSceneObject.transform.localRotation, parentRotation, Camera.main.transform, delta, manipulationSpeed);

                    rot.setValue(manipulationRot);
                    break;
                case ControllerModeEnum.Manip_Scale:
                    if (Mathf.Abs(delta) <= triggerDeadzone) 
                        return;

                    Parameter<Vector3> scale = (Parameter<Vector3>)_selectedAbstractParam;
                    Vector3 manipulationScale = _currentSelectedSceneObject.transform.localScale + (Vector3.one * delta * Time.deltaTime * manipulationSpeed);

                    scale.setValue(manipulationScale);
                    break;
                case ControllerModeEnum.Manip_Color:
                    if (Mathf.Abs(delta) <= triggerDeadzone || !_colorSelect) 
                        return;

                    _colorSelect.controllerManipulator(new Vector3(0, 0, delta));
                    break;
                case ControllerModeEnum.Manip_ListParameter:
                    if (Mathf.Abs(delta) <= triggerDeadzone) 
                        return;

                    //only go one list up/down, not multiple by holding
                    if ((delta < 0 && !_isLeftTriggerHold) || (delta > 0 && !_isRightTriggerHold)){
                            if(delta < 0) _isLeftTriggerHold = true; else _isRightTriggerHold = true;

                        ListParameter listPara = (ListParameter)_selectedAbstractParam;
                        int newValue = listPara.value + (int)Mathf.Sign(delta);
                        if(newValue < 0)
                            newValue = listPara.parameterList.Count-1;
                        else
                            newValue = newValue % listPara.parameterList.Count;
                        listPara.setValue(newValue);
                    } else {
                        if(delta < 0) _isLeftTriggerHold = false; else _isRightTriggerHold = false;
                    }
                    break;
                case ControllerModeEnum.Manip_Vec2:
                    break;
                case ControllerModeEnum.Manipulation:
                default:
                    //values +-
                    if (Mathf.Abs(delta) <= triggerDeadzone) 
                        return;

                    Parameter<float> val = (Parameter<float>)_selectedAbstractParam;
                    val.setValue(val.value + delta * Time.deltaTime * manipulationSpeed);

                    //- Other Modes, vertical: increase/decrease, fields e.g. color: x/y axis
                    break;
            }
        }

        //! cycle through ui and 'select' it
        private void ProcessDPad() {
            // 1. Read the raw D-pad Vector
            Vector2 dpadInput = m_inputs.VPETMap.Controller_Dpad.ReadValue<Vector2>();

            // 2. Evaluate Deadzone & Discrete Press Logic
            if (dpadInput.sqrMagnitude > dpadDeadzone * dpadDeadzone){
                if (!_dpadInUse){
                    _dpadInUse = true;
                    switch (controlMode) {
                        case ControllerModeEnum.Viewing:
                            NavigateUI(dpadInput);
                            break;
                        default:
                            //left right toggle
                            if(dpadInput.x > 0)
                                SwitchManipulationMode(1);
                            else if(dpadInput.x < 0)
                                SwitchManipulationMode(-1);
                            break;
                    }
                } else {
                    //we could have a timer that counts up a limit to rush through the selectables instead of manually pressing
                }
            }else{
                // Reset the lock when the player lets go of the D-pad
                _dpadInUse = false; 
            }
        }

        #endregion

        #region BTN EVENTS
        private void PressConfirm(InputAction.CallbackContext ctx) {
            //Debug.Log("PressConfirm<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
            if(_currentSelection != null) {
                if(_currentSelection.GetComponent<SnapSelectElement>()){
                    //Debug.Log("Confirm selection as ControllerClick");
                    _currentSelection.GetComponent<SnapSelectElement>().ControllerClick();
                }else{
                    //Debug.Log("Confirm selection as Selectable.OnSelect");
                    // lightweight base event payload
                    BaseEventData baseEvent = new BaseEventData(EventSystem.current);
                    
                    // fire 'submit interface' directly to the object
                    ExecuteEvents.Execute(_currentSelection.gameObject, baseEvent, ExecuteEvents.submitHandler);
                }
            } else {
                SelectSceneObject();
            }
        }


        private void PressCancel(InputAction.CallbackContext ctx) {
            if(_currentSelection != null) {
                EventSystem.current.SetSelectedGameObject(null);
                _currentSelection = null;
            } else {
                switch (controlMode) {
                    case ControllerModeEnum.Viewing:
                        break;
                    default:
                        //go back from manipulation mode to viewing mode
                        //controlMode = ControllerModeEnum.Viewing;
                        core.getManager<UIManager>().clearSelectedObjects();
                        break;
                }
            }
        }

        //handles re-select (was Joystick Button 6)
        private void PressSelect(InputAction.CallbackContext ctx) {
            switch (controlMode) {
                case ControllerModeEnum.Viewing:
                    if(_currentSelection == null) {
                        if(_previousSelectedSceneObject != null) {
                            //this will also switch to Manipulation Mode
                            core.getManager<UIManager>().addSelectedObject(_previousSelectedSceneObject);
                        }
                    }
                    break;
                default:
                    
                    break;
            }
            
        }

        private void PressLeftShoulder(InputAction.CallbackContext ctx){
            //cycle through selectables all the time (no difference)
            CycleThroughSceneObjects(EvaluationHelper.NavDirection.Left);
        }

        private void PressRightShoulder(InputAction.CallbackContext ctx){
            //cycle through selectables all the time (no difference)
            CycleThroughSceneObjects(EvaluationHelper.NavDirection.Right);
        }

        private void PressLeftStick(InputAction.CallbackContext ctx) {
            //switch local <> global manipulation axis
            switch (controlMode) {
                case ControllerModeEnum.Viewing:
                    break;
                default:
                    _uiManager.CycleManipulationMode();
                    core.StartCoroutine(AnimateFloatingText(""+_uiManager.ManipulationLayer, new Vector2(Screen.width / 2, Screen.height / 2)));
                    break;
            }
            
        }

        private void PressRightStick(InputAction.CallbackContext ctx) {
            inOrbitMode = !inOrbitMode;
            core.StartCoroutine(AnimateFloatingText(inOrbitMode ? "Orbit View Mode" : "Standard View Mode", new Vector2(Screen.width / 2, Screen.height / 2)));
        }
        #endregion

        private void CycleThroughSceneObjects(EvaluationHelper.NavDirection direction) {
            //we do a check with role here too - ignore the overhead...
            SceneObject nextSceneObject = EvaluationHelper.Instance.FindNextVisibleObject(core.getManager<SceneManager>().getAllSceneObjects(), _currentSelectedSceneObject, Camera.main, direction, _uiManager.activeRole);
            
            if (nextSceneObject != null){
                if(_uiManager.isThisOurSelectedObject(nextSceneObject)){
                    return;
                }else{
                    _uiManager.clearSelectedObjects();
                }

                _uiManager.addSelectedObject(nextSceneObject);
            }else{
                _uiManager.clearSelectedObjects();
            }
        }

        #region CALLBACK EVENTS

        private void FireDragOtherEvent(InputTracker tracker, InputManager.InputState state, Vector2 position, Vector2 delta) {
            // Construct your InputData payload exactly as you do in the UnityInputModule
            tracker.CurrentPosition = position;
            tracker.CurrentDelta = delta;

            if(state == InputManager.InputState.Started) {
                tracker.StartPosition = position;
            }

            var data = InputTracker.ToArgs<InputManager.DragEventArgs>(tracker.Level, state, position, delta);
            data.StartPosition = tracker.StartPosition;
            manager.RaiseDragOther(this, data); 
        }
        private void FirePinchOtherEvent(InputTracker tracker, InputManager.InputState state, Vector2 position, float delta) {
            // Construct your InputData payload exactly as you do in the UnityInputModule
            tracker.CurrentPosition = position;

            if(state == InputManager.InputState.Started) {
                tracker.StartPosition = position;
            }

            var data = InputTracker.ToArgs<InputManager.PinchEventArgs>(tracker.Level, state, position);
            data.PinchDelta = delta;
            manager.RaisePinchOther(this, data); 
        }
        #endregion

        #region UI NAV
        //!
        //! called when destroying UI elements, or switching menus.
        //! ensures our current selection hasn't been disabled or destroyed.
        //!
        private void RevertUIElements(){
            Debug.Log("RefreshUIElements");
            // If our selected object was destroyed or turned off, clear it.
            if (_currentSelection != null && (!_currentSelection.gameObject.activeInHierarchy || !_currentSelection.interactable)){
                if(_currentSelectionImage)
                    _currentSelectionImage.color = _selectionColorWas;

                _currentSelection = null;
                _currentSelectionImage = null;
                Debug.Log("set current selection to null");
            }

            //try reselecting previous element, if available!

            // If you want to force a re-evaluation of the screen immediately:
            // if (_currentSelection == null)
            //     SelectFallbackTopLeft();
        }
        private void NavigateUI(Vector2 rawDirection){
            // 1. Ensure we have a starting point
            if (_currentSelection == null || !_currentSelection.gameObject.activeInHierarchy){
                SelectFallbackTopLeft();
                return;
            }

            // 2. Snap the raw Vector2 to a strict 4-way direction
            Vector3 navDir;
            if (Mathf.Abs(rawDirection.x) > Mathf.Abs(rawDirection.y))
                navDir = rawDirection.x > 0 ? Vector3.right : Vector3.left;
            else
                navDir = rawDirection.y > 0 ? Vector3.up : Vector3.down;
            

            // 3. The Magic Function: Finds the closest UI element in that direction
            Selectable nextElement = _currentSelection.FindSelectable(navDir);

            //TODO: check for hidden/faded elements and do not select them
            //      e.g. the spinning wheel
            //      or at least do not select in circles but cycle through

            if (nextElement != null)
                SetSelection(nextElement);
            else
                Debug.Log("no next selectable found in direction "+navDir);
            
        }

        private void SetSelection(Selectable newSelection){
            if(_currentSelection && _currentSelectionImage)
                _currentSelectionImage.color = _selectionColorWas;

            _currentSelection = newSelection;

            if(_currentSelection && _currentSelection.GetComponent<Image>()){
                _currentSelectionImage = _currentSelection.GetComponent<Image>();
                _selectionColorWas = _currentSelectionImage.color;
                _currentSelectionImage.color = Color.green;
            }

            // Tells the Unity EventSystem that this is the active object.
            // This triggers the Hover/Selected color changes on your Buttons automatically!
            _currentSelection.Select(); 
            Debug.Log("Selected "+_currentSelection.name);
        }

        /// <summary>
        /// Finds the interactable UI element closest to the Top-Left of the screen.
        /// </summary>
        private void SelectFallbackTopLeft(){
            Debug.Log("Select Fallback Top Left");

            // Selectable.allSelectabless is a built-in static list maintained by Unity!
            // No performance-heavy FindObjectsOfType needed.
            var allSelectables = Selectable.allSelectablesArray;
            
            Selectable closestToTopLeft = null;
            float minDistance = float.MaxValue;
            
            // Screen space top-left is (0, Screen.height)
            Vector2 topLeft = new Vector2(0, Screen.height);

            foreach (Selectable s in allSelectables){
                if (!s.interactable || !s.gameObject.activeInHierarchy) continue;

                // Get screen position of the UI element
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, s.transform.position);
                
                float distance = Vector2.Distance(topLeft, screenPos);
                if (distance < minDistance){
                    minDistance = distance;
                    closestToTopLeft = s;
                }
            }

            if (closestToTopLeft != null)
                SetSelection(closestToTopLeft);
        }
        #endregion

        #region ROTATION MANIP

        //******* TWO AXIS VIA STICK

        //! 
        //! Pitches and rolls the object around its own internal axes.
        //!
        private Quaternion CalculateLocalRotation(Quaternion currentRotation, Vector2 stickAxis, float speed) {
            // Positive X pitches forward. Negative Z rolls clockwise.
            float pitchAngle = -stickAxis.x * speed;
            float rollAngle =  stickAxis.y * speed;
            Quaternion localDelta = Quaternion.Euler(pitchAngle, 0f, rollAngle);
            // MULTIPLY ON THE RIGHT: Applies the rotation relative to the object's intrinsic axes.
            return currentRotation * localDelta;
        }

        //! 
        //! Pitches and rolls the object around the absolute world axes (World X and World Z).
        //!
        private Quaternion CalculateGlobalRotation(Quaternion currentLocalRot, Quaternion parentRot, Vector2 stickAxis, float speed) {
            float pitchAngle = -stickAxis.x * speed;
            float rollAngle =  stickAxis.y * speed;
            Quaternion globalDelta = Quaternion.Euler(pitchAngle, 0f, rollAngle);
            // MULTIPLY ON THE LEFT: Applies the rotation relative to the absolute World space.
            //return globalDelta * currentRotation;
            // 2. THE FIX: Wrap the delta in the parent's space.
            // Math Proof: TargetLocal = ParentInverse * WorldDelta * ParentRot * CurrentLocal
            return Quaternion.Inverse(parentRot) * globalDelta * parentRot * currentLocalRot;
        }

        //!
        //! Pitches and rolls the object perfectly aligned with the screen, regardless of where the camera is looking.
        //!
        private Quaternion CalculateCameraRelativeRotation(Quaternion currentLocalRot, Quaternion parentRot, Vector2 stickAxis, Transform cameraTransform, float speed) {
            float pitchAngle = -stickAxis.x * speed;
            float rollAngle = stickAxis.y * speed;

            // Instead of Euler angles, we build absolute world-space rotations based on the Camera's current directional vectors.
            // Pitch rotates around the Camera's Right axis. Roll rotates around the Camera's Forward axis.
            Quaternion pitchDelta = Quaternion.AngleAxis(pitchAngle, cameraTransform.forward);
            Quaternion rollDelta = Quaternion.AngleAxis(rollAngle, cameraTransform.right);

            // Combine the two camera-aligned deltas
            Quaternion cameraDelta = pitchDelta * rollDelta;

            // MULTIPLY ON THE LEFT: Because our cameraDelta was built using World-Space vectors (camera.right/forward), 
            // we must apply it globally to the object.
            //return cameraDelta * currentRotation;
            // 2. THE FIX: Wrap the delta in the parent's space.
            return Quaternion.Inverse(parentRot) * cameraDelta * parentRot * currentLocalRot;
        }
        
        /*private void RotateLocal(Transform target, Vector2 stickInput, float rotationSpeed){
            if (target == null || stickInput.sqrMagnitude < 0.001f) return;

            // Calculate degrees for this frame
            float pitchDelta = stickInput.y * rotationSpeed * Time.deltaTime;
            
            // Notice the negative sign on X: In Unity's left-handed system, positive Z rotation 
            // tilts counter-clockwise. Negating it ensures pushing Right on the stick rolls Clockwise.
            float rollDelta = -stickInput.x * rotationSpeed * Time.deltaTime;

            // Space.Self applies the rotation along the object's local X and Z axes
            target.Rotate(pitchDelta, 0f, rollDelta, Space.Self);
        }

        private void RotateGlobal(Transform target, Vector2 stickInput, float rotationSpeed){
            if (target == null || stickInput.sqrMagnitude < 0.001f) return;

            float pitchDelta = stickInput.y * rotationSpeed * Time.deltaTime;
            float rollDelta = -stickInput.x * rotationSpeed * Time.deltaTime;

            // Space.World applies the rotation strictly along the absolute World X and Z axes,
            // ignoring how the object is currently tilted.
            target.Rotate(pitchDelta, 0f, rollDelta, Space.World);
        }

        public void RotateCameraRelative(Transform target, Vector2 stickInput, Transform cameraTransform, float rotationSpeed){
            if (target == null || cameraTransform == null || stickInput.sqrMagnitude < 0.001f) return;

            float pitchDelta = stickInput.y * rotationSpeed * Time.deltaTime;
            float rollDelta = -stickInput.x * rotationSpeed * Time.deltaTime;

            // Grab the camera's right and forward axes, but flatten them to the floor (ignore camera tilt)
            Vector3 camRight = cameraTransform.right;
            Vector3 camForward = cameraTransform.forward;
            camRight.y = 0f;
            camForward.y = 0f;
            camRight.Normalize();
            camForward.Normalize();

            // Rotate around the camera's horizontal axis for Pitch, and camera's forward axis for Roll
            target.Rotate(camRight, pitchDelta, Space.World);
            target.Rotate(camForward, rollDelta, Space.World);
        }*/

        //******** ONE AXIS VIA BUMPER
        
        //!
        //! Spins the object around its own internal spine (like a spinning top).
        //!
        public Quaternion CalculateLocalYaw(Quaternion currentRotation, float axisInput, float speed) {
            float yawAngle = axisInput * speed;
            Quaternion localDelta = Quaternion.Euler(0f, yawAngle, 0f);
            // MULTIPLY ON RIGHT: Intrinsic rotation
            return currentRotation * localDelta;
        }

        //!
        //! Spins the object around the absolute world center axis (like a carousel).
        //!
        public Quaternion CalculateGlobalYaw(Quaternion currentRotation, Quaternion parentRot, float axisInput, float speed) {
            float yawAngle = axisInput * speed;
            Quaternion globalDelta = Quaternion.Euler(0f, yawAngle, 0f);
            // MULTIPLY ON LEFT: Extrinsic world rotation
            //return globalDelta * currentRotation;
            // bugfix: Wrap the world delta in the parent's space.
            return Quaternion.Inverse(parentRot) * globalDelta * parentRot * currentRotation;
        }

        //!
        //! Spins the object perfectly upright relative to your screen view.
        //!
        public Quaternion CalculateCameraRelativeYaw(Quaternion currentRotation, Quaternion parentRot, Transform cameraTransform, float axisInput, float speed) {
            float yawAngle = axisInput * speed;
            // Build an angle-axis delta using the Camera's Up vector as the pivot axle
            Quaternion cameraDelta = Quaternion.AngleAxis(yawAngle, cameraTransform.up);
            // MULTIPLY ON LEFT: Applied globally using the calculated camera axis
            //return cameraDelta * currentRotation;
            // bugfix: Wrap the camera's world delta in the parent's space.
            return Quaternion.Inverse(parentRot) * cameraDelta * parentRot * currentRotation;
        }

        #endregion

        #region POSITION MANIPULATION

        //******** TWO AXIS VIA STICK

        //!
        //! Moves the object strictly along its own Forward (Z) and Right (X) axes.
        //!
        public Vector3 CalculateLocalPosition(Vector3 currentPosition, Quaternion currentRotation, Vector2 stickAxis, float speed) {
            // local movement vector
            Vector3 localDelta = new Vector3(stickAxis.x, 0f, stickAxis.y) * speed;
            // align the movement with the object's current rotation.
            // In Unity, multiplying a Quaternion by a Vector3 rotates that vector into the Quaternion's space.
            Vector3 worldDelta = currentRotation * localDelta;
            return currentPosition + worldDelta;
        }

        //!
        //! Moves the object along the absolute World X and World Z axes, ignoring how the object is rotated.
        //!
        public Vector3 CalculateGlobalPosition(Vector3 currentPosition, Quaternion parentRot, Vector2 stickAxis, float speed) {
            // No rotation math required. We just directly map the stick to world coordinates.
            Vector3 worldDelta = new Vector3(stickAxis.x, 0f, stickAxis.y) * speed;

            // bugfix: Convert the World Delta into the Parent's Local Space (otherwise nested rotated objects moved wrongly!)
            Vector3 localDelta = Quaternion.Inverse(parentRot) * worldDelta;

            return currentPosition + localDelta;
        }

        //!
        //! Moves the object relative to the screen, but locks the movement to the X/Z floor plane.
        //!
        public Vector3 CalculateCameraRelativePosition(Vector3 currentPosition, Quaternion parentRot, Vector2 stickAxis, Transform cameraTransform, float speed) {
            // the camera's raw directional vectors
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            // flatten the vectors onto the world's XZ plane.
            // this prevents the object from flying up into the air or digging into the floor if the camera is angled.
            camForward.y = 0f;
            camRight.y = 0f;

            // Normalize. If we don't normalize, pushing the stick "Up" while looking 
            // almost straight down at the object will cause it to move incredibly slowly.
            camForward.Normalize();
            camRight.Normalize();

            Vector3 viewportDelta = (camRight * stickAxis.x + camForward * stickAxis.y) * speed;

            // bugfix: Convert the World Delta into the Parent's Local Space
            Vector3 localDelta = Quaternion.Inverse(parentRot) * viewportDelta;

            return currentPosition + localDelta;
        }

        //*********** ONE AXIS VIA BUMPER
        //!
        //! Moves the object strictly out of its own "roof" or "floor" based on its tilt.
        //!
        public Vector3 CalculateLocalYPosition(Vector3 currentPosition, Quaternion currentRotation, float axisInput, float speed) {
            // Move strictly along local Y
            Vector3 localDelta = new Vector3(0f, axisInput, 0f) * speed;
            Vector3 worldDelta = currentRotation * localDelta;
            return currentPosition + worldDelta;
        }

        //!
        //! Moves the object strictly up toward the sky or down toward the world floor, ignoring object rotation.
        //!
        public Vector3 CalculateGlobalYPosition(Vector3 currentPosition, Quaternion parentRot, float axisInput, float speed) {
            Vector3 globalDelta = new Vector3(0f, axisInput, 0f) * speed;
            
            // bugfix: Convert the World Delta into the Parent's Local Space
            Vector3 localDelta = Quaternion.Inverse(parentRot) * globalDelta;

            return currentPosition + localDelta;
        }

        //!
        //! Moves the object up or down relative to your monitor screen.
        //!
        public Vector3 CalculateCameraRelativeYPosition(Vector3 currentPosition, Quaternion parentRot, Transform cameraTransform, float axisInput, float speed) {
            // Use the camera's raw Up vector (no floor projection needed here, as we want vertical screen movement)
            Vector3 viewportDelta = cameraTransform.up * axisInput * speed;
            // bugfix: Convert the Camera's World Delta into the Parent's Local Space
            Vector3 localDelta = Quaternion.Inverse(parentRot) * viewportDelta;

            return currentPosition + localDelta;
        }
        #endregion
        
        //!
        //! Tracer update function
        //!
        private void TracerUpdate(object sender, EventArgs e){

            //FREE MODE (not in manipulating)

            //look around
            ProcessRightStick(inOrbitMode ? _tertiary : _primary, m_inputs.VPETMap.Controller_Right_Stick.ReadValue<Vector2>());
            //moving
            ProcessLeftStick(_secondary, m_inputs.VPETMap.Controller_Left_Stick.ReadValue<Vector2>());
            //shoulder trigger for going up/down
            ProcessTrigger(_secondary, -m_inputs.VPETMap.Controller_Left_Trigger.ReadValue<float>());
            ProcessTrigger(_secondary, m_inputs.VPETMap.Controller_Right_Trigger.ReadValue<float>());
            
            //shoulder buttons for combo: running (move faster)
            //shoulder buttons as switch: orbit<>look, 

            ProcessDPad();
            
            if(crosshairOffTimer > 0f) {
                crosshairOffTimer -= Time.deltaTime;
                if(crosshairOffTimer <= 0f)
                    _controllerCrosshairCanvas.SetActive(false);
            }
        }
        
        //!
        //! Handles the retrieval of the ColorSelect component.
        //!
        private void GetColorSelect(object sender, GameObject go)
        {
            _colorSelect = go.GetComponent<ColorSelect>();
        }

        
        //!
        //! Turns off the crosshair, clears the selected object in the UI manager, and initiates controller selection.
        //!
        private void SelectSceneObject(){
            //OffCrosshair();
            //_uiManager.clearSelectedObjects();
            Vector2 center = new Vector2(Screen.width / 2, Screen.height / 2);
            SceneObject sceneObject = EvaluationHelper.Instance.EvaluateSceneObject(center);

            //found no selectable
            if(sceneObject == null) {
                //test if we hit a 3d ui (cam, light, ...)
                //See GetSceneObjectAtPosition in SelectionModule
                GameObject hitObject = EvaluationHelper.Instance.EvaluateGameObject(center);
                if (hitObject) {
                    IconUpdate icon = hitObject.GetComponent<IconUpdate>();
                    if(icon && icon.m_parentObject)
                        sceneObject = icon.m_parentObject;
                }  
            }

            if (sceneObject != null){
                if(_uiManager.isThisOurSelectedObject(sceneObject)){
                    return;
                }else{
                    _uiManager.clearSelectedObjects();
                }

                _uiManager.addSelectedObject(sceneObject);
            }else{
                Debug.Log("<color=red>no valid SceneObject to select</color>");
                _uiManager.clearSelectedObjects();
            }
        }
    


        #region Selection Logic
        
        //!
        //! This method responds to a change in the selected objects within the UI manager.
        //! BUT ORDER IS NOT RELIABLE -> therefore put into UiCreationFinished
        //!
        //private void UiManagerSelectionChanged(object sender, List<SceneObject> sceneObjects){}
        
        //!
        //! This method responds to a change in the selected objects within the UI manager.´
        //! beware that order of events is not fixed and we cannot rely on created ui here
        //!
        private void UiManagerSelectionChanged(object sender, List<SceneObject> sceneObject){
            RevertUIElements();

            if(_uiManager.SelectedObjects.Count == 0){
                controlMode = ControllerModeEnum.Viewing;
                _currentSelectedSceneObject = null;
            }
        }

        // called when the ui has been (re)created
        private void UiCreationFinished(object sender, UIBehaviour uib) {
            // we have to call GetCurrentSelector here, because we instantly want to select something, but the order of events 
            // from selection-changed is random!

            if (uib != null && _uiManager.SelectedObjects.Count > 0){
                _currentSelectedSceneObject = _uiManager.SelectedObjects[0];
                _previousSelectedSceneObject = _currentSelectedSceneObject;
                //Debug.Log("_currentSelectedSceneObject is "+_currentSelectedSceneObject.gameObject.name);
                GetCurrentSelector(uib);
                controlMode = ControllerModeEnum.Manip_Translate;
            } else {
                controlMode = ControllerModeEnum.Viewing;
                _currentSelectedSceneObject = null;
            }
            
        }
        #endregion


        #region ManipulationModeRegion
        
        //!
        //! This method retrieves the current selector when not in MAIN_VIEW_MODE.
        //!
        private void GetCurrentSelector(UIBehaviour uib){
            //remove if already available!
            if(_selectorSnapSelect)
                _selectorSnapSelect.parameterChanged -= ParamChange;

            //_selectorSnapSelect = GameObject.Find("PRE_UI_AddSelector(Clone)").GetComponent<SnapSelect>();
            _selectorSnapSelect = (SnapSelect)uib;
            _selectorSnapSelect.parameterChanged += ParamChange;
            
            // int because buttonID is int
            HashSet<int> seenButtonIDs = new HashSet<int>(); 
            _selectorSnapSelectElementsList = new();

            foreach (var element in _selectorSnapSelect.elements){
                if (seenButtonIDs.Add(element.buttonID)){
                    _selectorSnapSelectElementsList.Add(element);
                }
            }
            //TODO: (as in the other selection as well - save previous manipulation element for a certain amount of time...)
            _selectorCurrentSelectedSnapSelectElement = 0;
            _selectedAbstractParam = _currentSelectedSceneObject.parameterList[_selectorCurrentSelectedSnapSelectElement];
        }

        //!
        //! This method responds to parameter changes in the manipulation mode.
        //!
        private void ParamChange(object sender, int manipulatorMode)
        {
            _selectorCurrentSelectedSnapSelectElement = manipulatorMode;
        }

        //!
        //! This method switches to the next available manipulation mode.
        //!
        private void SwitchManipulationMode(int dir = 1){
            int prevNextElement = _selectorCurrentSelectedSnapSelectElement + dir;
            if(prevNextElement < 0)
                prevNextElement = _selectorSnapSelectElementsList.Count-1;

            _selectorCurrentSelectedSnapSelectElement = prevNextElement % _selectorSnapSelectElementsList.Count;
            _selectorSnapSelectElementsList[_selectorCurrentSelectedSnapSelectElement].ControllerClick();
            
            _selectedAbstractParam = _currentSelectedSceneObject.parameterList[_selectorCurrentSelectedSnapSelectElement];
            
            //GetSpinner();
            SetManipulationMode();
        }
    

        private void SetManipulationMode() {
            //see UICreator2DModule
            Debug.Log("Set Manipulation Mode for name: "+_selectedAbstractParam.name);
            switch (_selectedAbstractParam.name){
                case "position":
                    controlMode = ControllerModeEnum.Manip_Translate;
                    break;
                case "rotation":
                    controlMode = ControllerModeEnum.Manip_Rotate;
                    break;
                case "scale":
                    controlMode = ControllerModeEnum.Manip_Scale;
                    break;
                case "color":
                    controlMode = ControllerModeEnum.Manip_Color;
                    break; 
                case "pathPositions":   //was a button like TRS before:
                    //should be skipped as selection!
                    break;
                case "pathRotations":   //was a button like TRS before: 
                    //should be skipped as selection!
                    break;
                case "animHostGen":     //RPC we use to trigger the character animation for a given path (both above) in AnimHost
                                        //not visualized at all (beware that they still increase to index of these snap elements)
                    break;
                case "SensorSizes":
                case "FocalLengths":
                    controlMode = ControllerModeEnum.Manip_ListParameter;
                    break;
                case "sensorSize":
                    controlMode = ControllerModeEnum.Manip_Vec2;
                    break;
                case "intensity":
                case "range":
                case "aperture":
                case "aspectRatio":
                case "radius":
                case "fov":
                case "farClipPlane":
                case "nearClipPlane":
                case "focalDistance":
                default:
                    controlMode = ControllerModeEnum.Manip_SingleValue;
                    break;
            }
        }
        
        //!
        //! This method invokes the doneEditing event for undo/redo when an editing operation is completed.
        //!
        private void DoneEditing(object sender, Vector2 value)
        {
            if (_selectedAbstractParam != null)
            {
                Debug.Log("ControllerModule: DoneEditing");
                ControllerdoneEditing?.Invoke(this, _selectedAbstractParam);
            }
        }
        #endregion

        // --- HELPER METHODS FOR FIRING EVENTS ---
        

        #region DEBUGGING
        private GameObject mainUIContainer;
        // Ensures we always have a canvas to draw on
        private void EnsureMainCanvasExists(){
            if (mainUIContainer != null) return;
            mainUIContainer = new GameObject("ControllerModuleUI");
            Canvas canvas = mainUIContainer.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
        }
        private System.Collections.IEnumerator AnimateFloatingText(string message, Vector2 startPos){
            EnsureMainCanvasExists();
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
        #endregion        

    }
    
   
}