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
//! @version 0
//! @date 08.09.2022


using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

namespace tracer
{
    //!
    //! Class implementing the input manager, managing all user inupts and mapping.
    //!
    public class InputManager : Manager
    {
        // [REVIEW]
        // Doesn't seem to be in use
        //!
        //! Enumeration defining supported input event types.
        //!
        public enum InputEventType
        {
            TAP,
            DRAG,
            STARTED,
            PERFORMED
        }

        //!
        //! The default input event.
        //!
        public event EventHandler<Vector2> objectSelectionEvent;
        
        public event EventHandler<Vector2> ControllerObjectSelectionEvent;

        //!
        //! Press start event, i.e. the begin of a click.
        //!
        public event EventHandler<Vector2> inputPressStart;
        //!
        //! Press start event, i.e. the begin of a click.
        //!
        public event EventHandler<Vector2> inputPressPerformed;
        //!
        //! Press end event, i.e. the end of a click.
        //!
        public event EventHandler<Vector2> inputPressEnd;
        //!
        //! Press move event, i.e. the moving of the cursor/finger.
        //!
        public event EventHandler<Vector2> inputMove;

        //!
        //! The two finger pinch input event.
        //!
        public event EventHandler<float> pinchEvent;
        //!
        //! The two finger drag input event.
        //!
        public event EventHandler<Vector2> twoDragEvent;
        //!
        //! The three finger drag input event.
        //!
        public event EventHandler<Vector2> threeDragEvent;

        //!
        //! Event to announce there is a finger gestures operation happening
        //!
        public event EventHandler<bool> fingerGestureEvent;

        //!
        //! Event to stop the UI drag operations (snap select)
        //!
        public event EventHandler<bool> toggle2DUIInteraction;

        //!
        //! Event linked to the UI command of changing to the next available camera
        //!
        public event EventHandler<bool> updateCameraUICommand;

        //!
        //! Event linked to change of CameraControl
        //!
        public event EventHandler<CameraControl> cameraControlChanged;

        #region Controller Events
        public event EventHandler<float> buttonNorth;
        public event EventHandler<float> buttonSouth;
        public event EventHandler<float> buttonEast;
        public event EventHandler<float> buttonWest;
        public event EventHandler<float> buttonUp;
        public event EventHandler<float> buttonDown;
        public event EventHandler<float> buttonLeft;
        public event EventHandler<float> buttonRight;
        public event EventHandler<float> buttonLeftTrigger;
        public event EventHandler<float> buttonRightTrigger;
        public event EventHandler<float> buttonLeftShoulder;
        public event EventHandler<float> buttonRighrShoulder;
        public event EventHandler<Vector2> leftControllerStick;
        public event EventHandler<Vector2> rightControllerStick;
        public event EventHandler<Vector2> ControllerStickCanceled;
        #endregion
        
        //!
        //! Enumeration describing possible touch input gestures.
        //!
        private enum InputTouchType
        {
            ONE,
            TWO,
            THREE,
            NONE
        }
        //!
        //! The touch input gesture type.
        //!
        private InputTouchType m_touchType;

        //!
        //! Flag to determine if a touch drag gesture is being performed.
        //!
        private bool m_isTouchDrag;
        //!
        //! Flag to specify type of gesture. 
        //!
        private bool m_isPinch;
        //!
        //! Simple latch for buffering operations. 
        //!
        private bool m_doOnce;
        //!
        //! Buffer Vector2 for input position comparison.
        //!
        private Vector2 m_posBuffer;
        //!
        //! Buffer float for input distance comparison.
        //!
        private float m_distBuffer;
        //!
        //! Buffers the main cameras initial rotation.
        //!
        private Quaternion m_cameraMainOffset;
        //!
        //! Buffers the sensors initial attitude.
        //!
        private Quaternion m_invAttitudeSensorOffset;
        //!
        //! A reference to the attitude button.
        //!
        private MenuButton m_attitudeButton;
        //!
        //! Enum defining the automatic camera control state.
        //!
        public enum CameraControl
        {
            NONE,
            ATTITUDE,
            TOUCH,
            AR
        }
        //!
        //! Flag defining if the camera is controlled by the attitide sensor.
        //!
        private CameraControl m_cameraControl = CameraControl.NONE;
        public CameraControl cameraControl
        {
            get => m_cameraControl;
            set
            {
                m_cameraControl = value;
                cameraControlChanged?.Invoke(this, value);
            }
        }
        //!
        //! The previous camera control type
        //!
        private CameraControl m_oldcameraControl = CameraControl.NONE;
        //!
        //! The generated Unity input class defining all available user inputs.
        //!
        private Inputs m_inputs;
        //!
        //! Simple latch for in-editor right click input.
        //!
        private bool orbitClick = false;
        //!
        //! Simple latch for in-editor middle click input.
        //!
        private bool dragClick = false;

        //!
        //! Constructor initializing member variables.
        //!
        public InputManager(Type moduleType, Core tracerCore) : base(moduleType, tracerCore)
        {
            // Enable input
            m_inputs = new Inputs();
            m_inputs.VPETMap.Enable();

            // Binding of the click event
            m_inputs.VPETMap.Tap.performed += ctx => TapFunction(ctx);

            // Dedicated bindings for monitoring touch and drag interactions
            m_inputs.VPETMap.Click.started += ctx => PressStart(ctx);
            m_inputs.VPETMap.Click.performed += ctx => PressPerformed(ctx);
            m_inputs.VPETMap.Click.canceled += ctx => PressEnd(ctx);
            
            
            //controller input
            m_inputs.VPETMap.Controller_North.performed += ctx => NorthButtonPressed(ctx);
            m_inputs.VPETMap.Controller_South.performed += ctx =>SouthButtonPressed(ctx);
            m_inputs.VPETMap.Controller_East.performed += ctx =>EastButtonPressed(ctx);
            m_inputs.VPETMap.Controller_West.performed += ctx =>WestButtonPressed(ctx);
            m_inputs.VPETMap.Controller_Up.performed += ctx =>UpButtonPressed(ctx);
            m_inputs.VPETMap.Controller_Down.performed += ctx =>DownButtonPressed(ctx);
            m_inputs.VPETMap.Controller_Left.performed += ctx =>LeftButtonPressed(ctx);
            m_inputs.VPETMap.Controller_Right.performed += ctx =>RightButtonPressed(ctx);
            m_inputs.VPETMap.Controller_Left_Trigger.performed += ctx =>LeftTriggerButtonPressed(ctx);
            m_inputs.VPETMap.Controller_Right_Trigger.performed += ctx =>RightTriggerButtonPressed(ctx);
            m_inputs.VPETMap.Controller_Left_Shoulder.performed += ctx =>LeftShoulderButtonPressed(ctx);
            m_inputs.VPETMap.Controller_Right_Shoulder.performed += ctx =>RightShoulderButtonPressed(ctx);
            m_inputs.VPETMap.Controller_Left_Stick.performed += ctx => LeftStick(ctx);
            m_inputs.VPETMap.Controller_Left_Stick.canceled += ctx => LeftStick(ctx);
            m_inputs.VPETMap.Controller_Right_Stick.performed += ctx => RightStick(ctx);
            m_inputs.VPETMap.Controller_Right_Stick.canceled += ctx => RightStick(ctx);
            m_inputs.VPETMap.Controller_Left_Stick.canceled += ctx => StickCanceld(ctx);;
            m_inputs.VPETMap.Controller_Right_Stick.canceled += ctx => StickCanceld(ctx);;

            
            // Keep track of cursor/touch move
            m_inputs.VPETMap.Position.performed += ctx => MovePoint(ctx);

            // Enhaced touch interface API
            EnhancedTouchSupport.Enable();

            // Subscription to new touch or lift gestures
            UnityEngine.InputSystem.EnhancedTouch.Touch.onFingerDown += FingerDown;
            UnityEngine.InputSystem.EnhancedTouch.Touch.onFingerUp += FingerUp;

            // Subscription to finger movement 
            UnityEngine.InputSystem.EnhancedTouch.Touch.onFingerMove += FingerMove;

            // Additional subscriptions for specific input gestures
            UnityEngine.InputSystem.EnhancedTouch.Touch.onFingerMove += TwoFingerMove;
            UnityEngine.InputSystem.EnhancedTouch.Touch.onFingerMove += ThreeFingerMove;

#if UNITY_EDITOR
            // Editor-only mouse camera manipulation
            m_inputs.VPETMap.OrbitClick.performed += OrbitClick_performed;
            m_inputs.VPETMap.OrbitClick.canceled += OrbitClick_canceled;
            m_inputs.VPETMap.DragClick.performed += DragClick_performed;
            m_inputs.VPETMap.DragClick.canceled += DragClick_canceled;
            m_inputs.VPETMap.Position.performed += Position_performed;
            m_inputs.VPETMap.ZoomWheel.performed += ZoomWheel_performed;
#endif
        }

        #region Controller Button Events Invoke
        private void LeftShoulderButtonPressed(InputAction.CallbackContext ctx)
        {
            buttonLeftShoulder?.Invoke(this, ctx.ReadValue<float>());
        }
        private void RightShoulderButtonPressed(InputAction.CallbackContext ctx)
        {
            buttonRighrShoulder?.Invoke(this, ctx.ReadValue<float>());
        }
        private void RightTriggerButtonPressed(InputAction.CallbackContext ctx)
        {
            buttonRightTrigger?.Invoke(this, ctx.ReadValue<float>());
        }

        private void LeftTriggerButtonPressed(InputAction.CallbackContext ctx)
        {
            buttonLeftTrigger?.Invoke(this, ctx.ReadValue<float>());
        }

        private void RightButtonPressed(InputAction.CallbackContext ctx)
        {
            buttonRight?.Invoke(this, ctx.ReadValue<float>());
        }

        private void LeftButtonPressed(InputAction.CallbackContext ctx)
        {
            buttonLeft?.Invoke(this, ctx.ReadValue<float>());
        }

        private void DownButtonPressed(InputAction.CallbackContext ctx)
        {
            buttonDown?.Invoke(this, ctx.ReadValue<float>());
        }

        private void UpButtonPressed(InputAction.CallbackContext ctx)
        {
            buttonUp?.Invoke(this, ctx.ReadValue<float>());
        }

        private void WestButtonPressed(InputAction.CallbackContext ctx)
        {
            buttonWest?.Invoke(this, ctx.ReadValue<float>());
        }

        private void EastButtonPressed(InputAction.CallbackContext ctx)
        {
            buttonEast?.Invoke(this, ctx.ReadValue<float>());
        }
        private void SouthButtonPressed(InputAction.CallbackContext ctx)
        {
            buttonSouth?.Invoke(this, ctx.ReadValue<float>());
        }
        private void NorthButtonPressed(InputAction.CallbackContext ctx)
        {
            buttonNorth?.Invoke(this, ctx.ReadValue<float>());
            
        }
        private void LeftStick(InputAction.CallbackContext ctx)
        {
            leftControllerStick?.Invoke(this, ctx.ReadValue<Vector2>());
        }
        
        private void RightStick(InputAction.CallbackContext ctx)
        {
            rightControllerStick?.Invoke(this, ctx.ReadValue<Vector2>());
        }
        
        private void StickCanceld(InputAction.CallbackContext ctx)
        {
            ControllerStickCanceled?.Invoke(this, ctx.ReadValue<Vector2>());
        }
        
        #endregion
        
        //!
        //! Function to handle right mouse button input (editor only)
        //!
        private void OrbitClick_performed(InputAction.CallbackContext obj)
        {
            orbitClick = true;
        }

        //!
        //! Function to handle right mouse button input (editor only)
        //!
        private void OrbitClick_canceled(InputAction.CallbackContext obj)
        {
            orbitClick = false;
            m_doOnce = true;
        }

        //!
        //! Function to handle middle mouse button input (editor only)
        //!
        private void DragClick_performed(InputAction.CallbackContext obj)
        {
            dragClick = true;
        }

        //!
        //! Function to handle middle mouse button input (editor only)
        //!
        private void DragClick_canceled(InputAction.CallbackContext obj)
        {
            dragClick = false;
            m_doOnce = true;
        }

        //!
        //! Function to handle mouse movement for camera operation (editor only)
        //!
        private void Position_performed(InputAction.CallbackContext obj)
        {
            if (orbitClick)
            {
                Vector2 pos = m_inputs.VPETMap.Position.ReadValue<Vector2>();

                // Store it once
                if (m_doOnce)
                {
                    m_posBuffer = pos;
                    m_doOnce = false;
                }

                // Invoke event
                twoDragEvent?.Invoke(this, pos - m_posBuffer);

                // Update buffer
                m_posBuffer = pos;
            }
            else if (dragClick)
            {
                // Grab the position
                Vector2 pos = m_inputs.VPETMap.Position.ReadValue<Vector2>();

                // Store it once
                if (m_doOnce)
                {
                    m_posBuffer = pos;
                    m_doOnce = false;
                }

                // Invoke event
                threeDragEvent?.Invoke(this, pos - m_posBuffer);

                // Update buffer
                m_posBuffer = pos;
            }    
        }

        //!
        //! Function to handle mouse zoom wheel input (editor only)
        //!
        private void ZoomWheel_performed(InputAction.CallbackContext obj)
        {
            float dist = 0.1f * m_inputs.VPETMap.ZoomWheel.ReadValue<float>();

            // Invoke event
            pinchEvent?.Invoke(this, dist);
        }

        //! 
        //! Function called when Unity initializes the TRACER core.
        //! 
        //! @param sender A reference to the TRACER core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Init(object sender, EventArgs e)
        {
            base.Init(sender, e);
            // Global variables initialization
            m_isPinch = false;
            m_doOnce = true;
            m_touchType = InputTouchType.NONE;
        }

        //! 
        //! Virtual function called when Unity calls it's Start function.
        //! 
        //! @param sender A reference to the TRACER core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Start(object sender, EventArgs e)
        {
            base.Start(sender, e);
            if(m_cameraControl == CameraControl.NONE)
                enableAttitudeSensor();
        }

        //! 
        //! Function called before Unity destroys the TRACER core.
        //! 
        //! @param sender A reference to the TRACER core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Cleanup(object sender, EventArgs e)
        {
            base.Cleanup(sender, e);

            m_inputs.VPETMap.Tap.performed -= TapFunction;

            m_inputs.VPETMap.Click.performed -= PressPerformed;
            m_inputs.VPETMap.Click.canceled -= PressEnd;

            m_inputs.VPETMap.Position.performed -= MovePoint;

            UnityEngine.InputSystem.EnhancedTouch.Touch.onFingerDown -= FingerDown;
            UnityEngine.InputSystem.EnhancedTouch.Touch.onFingerUp -= FingerUp;

            UnityEngine.InputSystem.EnhancedTouch.Touch.onFingerMove -= FingerMove;

            UnityEngine.InputSystem.EnhancedTouch.Touch.onFingerMove -= TwoFingerMove;
            UnityEngine.InputSystem.EnhancedTouch.Touch.onFingerMove -= ThreeFingerMove;
            
            //controller input
            m_inputs.VPETMap.Controller_North.performed -= NorthButtonPressed;
            m_inputs.VPETMap.Controller_South.performed -= SouthButtonPressed;
            m_inputs.VPETMap.Controller_East.performed -= EastButtonPressed;
            m_inputs.VPETMap.Controller_West.performed -= WestButtonPressed;
            m_inputs.VPETMap.Controller_Up.performed -= UpButtonPressed;
            m_inputs.VPETMap.Controller_Down.performed -= DownButtonPressed;
            m_inputs.VPETMap.Controller_Left.performed -= LeftButtonPressed;
            m_inputs.VPETMap.Controller_Right.performed -= RightButtonPressed;
            m_inputs.VPETMap.Controller_Left_Trigger.performed -= LeftTriggerButtonPressed;
            m_inputs.VPETMap.Controller_Right_Trigger.performed -= RightTriggerButtonPressed;
            m_inputs.VPETMap.Controller_Left_Shoulder.performed -= LeftShoulderButtonPressed;
            m_inputs.VPETMap.Controller_Right_Shoulder.performed -= RightShoulderButtonPressed;
            m_inputs.VPETMap.Controller_Left_Stick.performed -= LeftStick;
            m_inputs.VPETMap.Controller_Left_Stick.canceled -= LeftStick;
            m_inputs.VPETMap.Controller_Right_Stick.performed -= RightStick;
            m_inputs.VPETMap.Controller_Right_Stick.canceled -= RightStick;
            
            m_inputs.VPETMap.Controller_Left_Stick.canceled -= StickCanceld;
            m_inputs.VPETMap.Controller_Right_Stick.canceled -= StickCanceld;
            

#if UNITY_EDITOR
            // Editor-only mouse camera manipulation
            m_inputs.VPETMap.OrbitClick.performed -= OrbitClick_performed;
            m_inputs.VPETMap.OrbitClick.canceled -= OrbitClick_canceled;
            m_inputs.VPETMap.DragClick.performed -= DragClick_performed;
            m_inputs.VPETMap.DragClick.canceled -= DragClick_canceled;
            m_inputs.VPETMap.Position.performed -= Position_performed;
            m_inputs.VPETMap.ZoomWheel.performed -= ZoomWheel_performed;
#endif
        }

        public void enableAttitudeSensor()
        {
            // Enable attitude sensor and bind it to the camera update
            if (m_cameraControl == CameraControl.NONE)
            {
                if (AttitudeSensor.current != null)
                {
                    if (!AttitudeSensor.current.enabled)
                    {
                        InputSystem.EnableDevice(AttitudeSensor.current);
                        m_attitudeButton = new MenuButton("", useAttitude);
                        m_attitudeButton.setIcon("Images/button_attitude");
                        core.getManager<UIManager>().addButton(m_attitudeButton);
                    }
                }
                else
                    Helpers.Log("No attitude sensor found, feature will not be available.", Helpers.logMsgType.WARNING);
            }
        }

        public void disableAttitudeSensor()
        {
            // Enable attitude sensor and bind it to the camera update

            if (AttitudeSensor.current != null)
            {
                InputSystem.DisableDevice(AttitudeSensor.current);
                core.getManager<UIManager>().removeButton(m_attitudeButton);
                m_cameraControl = CameraControl.NONE;
            }
            else
                Helpers.Log("No attitude sensor found, feature will not be available.", Helpers.logMsgType.WARNING);
        }

        //!
        //! Single tap/touch operation.
        //!
        private void TapFunction(InputAction.CallbackContext c)
        {
            
            if (c.performed)
            {
                Vector2 point = m_inputs.VPETMap.Position.ReadValue<Vector2>();
                if (!TappedUI(point) && !Tapped3DUI(point))
                {
                    objectSelectionEvent?.Invoke(this, point);
                }
            }

            // just an exampe, needs different code to discover correct type and values!
            // we need to define TRACER actions like tap, hold, drag, etc. and map it to
            // multiple bindings like keyboard, mouse click and touch (see referenced video)
            // please watch https://youtu.be/rMlcwtoui4I

            // at start we should check if we are on object, canvas or UI element
            if (c.started)
            {
                //e.type = InputEventType.STARTED;
                //e.delta = Vector2.zero;
                //e.time = 0f;
            }
        }

        //!
        //! Input move function, for monitoring the moving of the cursor/finger.
        //!
        private void MovePoint(InputAction.CallbackContext c)
        {
            Vector2 point = m_inputs.VPETMap.Position.ReadValue<Vector2>();
            inputMove?.Invoke(this, m_inputs.VPETMap.Position.ReadValue<Vector2>());

            if (!m_isTouchDrag && m_touchType == InputTouchType.ONE)
                m_isTouchDrag = true;
        }

        //!
        //! Input press start function, for monitoring the start of touch/click interactions.
        //!
        private void PressStart(InputAction.CallbackContext c)
        {
            Vector2 point = m_inputs.VPETMap.Position.ReadValue<Vector2>();
            if (!TappedUI(point))
                inputPressStart?.Invoke(this, point);
        }

        //!
        //! Input press start function, for monitoring the start of touch/click interactions.
        //!
        private void PressPerformed(InputAction.CallbackContext c)
        {
            Vector2 point = m_inputs.VPETMap.Position.ReadValue<Vector2>();
            if (!TappedUI(point))
                inputPressPerformed?.Invoke(this, point);
        }

        //!
        //! Input press end function, for monitoring the end of touch/click interactions.
        //!
        private void PressEnd(InputAction.CallbackContext c)
        {
            Vector2 point = m_inputs.VPETMap.Position.ReadValue<Vector2>();

            inputPressEnd?.Invoke(this, point);

            // Reset monitor variables
            m_touchType = InputTouchType.NONE;
            m_isTouchDrag = false;
        }

        //!
        //! Function to handle  any new finger touching the screen.
        //!
        private void FingerDown(Finger fgr)
        {
            // If a specific gesture is in progress, do not accept new input
            if (m_isTouchDrag)
                return;

            // Reset monitor variables
            m_touchType = InputTouchType.NONE;

            // Poll touch count 
            int touchCount = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count;

            // Single touch
            if (touchCount == 1)
                m_touchType = InputTouchType.ONE;
            // Double touch
            if (touchCount == 2)
                m_touchType = InputTouchType.TWO;
            // Triple touch - ignored in case of a two finger operation in progress
            if (touchCount == 3)
                m_touchType = InputTouchType.THREE;
        }

        //!
        //! Function to handle any finger being lifted from the screen.
        //!
        private void FingerUp(Finger fgr)
        {
            // Suspend the touch input
            m_touchType = InputTouchType.NONE;

            m_cameraControl = m_oldcameraControl;


            // Also the moving
            m_isTouchDrag = false;

            // Restore UI Interaction
            toggle2DUIInteraction.Invoke(this, true);
        }

        //!
        //! Function to handle initial finger movement on the screen.
        //!
        private void FingerMove(Finger fgr)
        {
            // If a specific gesture is in progress, do not accept new input
            if (m_isTouchDrag)
                return;

            // Else (i.e., touch was made, but not moved)
            // and if operating with multi-finger input,
            // force the suspension of active selection.
            if (m_touchType == InputTouchType.TWO || m_touchType == InputTouchType.THREE)
            {
                LockUIOperation();
                //ClearClickInput();
            }
        }

        //!
        //! Function to handle specifically two-finger gestures.
        //!
        private void TwoFingerMove(Finger fgr)
        {
            if (m_touchType != InputTouchType.TWO)
                return;

            // Register the gesture
            m_isTouchDrag = true;

            // Monitor touches
            var tcs = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;

            // Are they moving in the same direction?
            float dotProd = Vector2.Dot(tcs[0].delta, tcs[1].delta);
            // If yes, it's a two finger drag
            if (dotProd > 0)
            {
                // Reset control variables
                if (m_isPinch)
                    m_doOnce = true;
                m_isPinch = false;
            }
            // Else it's a two finger pinch
            else if (dotProd < 0)
            {
                // Reset control variables
                if (!m_isPinch)
                    m_doOnce = true;
                m_isPinch = true;
            }

            // Two finger drag (used for orbit)
            if(!m_isPinch && (m_cameraControl == CameraControl.NONE || m_cameraControl == CameraControl.TOUCH))
            { 
                // Grab the average position
                Vector2 pos = .5f * (tcs[0].screenPosition + tcs[1].screenPosition);

                // Store it once
                if (m_doOnce)
                {
                    m_posBuffer = pos;
                    m_doOnce = false;
                }

                // Invoke event
                if(m_cameraControl != CameraControl.TOUCH)
                    m_oldcameraControl = m_cameraControl;
                m_cameraControl = CameraControl.TOUCH;
                twoDragEvent?.Invoke(this, pos - m_posBuffer);

                // Update buffer
                m_posBuffer = pos;
            }
            // Two finger pinch (used for zoom)
            else
            {
                // Grab the distance
                float dist = Vector2.Distance(tcs[0].screenPosition, tcs[1].screenPosition);

                // Store it once
                if (m_doOnce)
                {
                    m_distBuffer = dist;
                    m_doOnce = false;
                }

                // Invoke event
                if (m_cameraControl != CameraControl.TOUCH)
                    m_oldcameraControl = m_cameraControl;
                m_cameraControl = CameraControl.TOUCH;

                pinchEvent?.Invoke(this, dist - m_distBuffer);

                // Update buffer
                m_distBuffer = dist;
            }
            // Announce gesture event
            fingerGestureEvent?.Invoke(this, true);
        }

        //!
        //! Function to handle specifically three-finger gestures.
        //!
        private void ThreeFingerMove(Finger fgr)
        {
            if (m_touchType != InputTouchType.THREE)
                return;

            // Register the gesture
            m_isTouchDrag = true;

            // Monitor touches
            var tcs = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;

            // Grab the average position
            Vector2 pos = 1f / 3f * (tcs[0].screenPosition + tcs[1].screenPosition + tcs[2].screenPosition);

            // Store it once
            if (m_doOnce)
            {
                m_posBuffer = pos;
                m_doOnce = false;
            }

            // Invoke event
            if (m_cameraControl != CameraControl.TOUCH)
                m_oldcameraControl = m_cameraControl;
            m_cameraControl = CameraControl.TOUCH;
            threeDragEvent?.Invoke(this, pos - m_posBuffer);

            // Update buffer
            m_posBuffer = pos;

            // Announce gesture event
            fingerGestureEvent?.Invoke(this, true);
        }

        //!
        //! Helper function to stop UI operations while moving camera
        //!
        private void LockUIOperation()
        {
            // Clear monitor variables
            m_doOnce = true;

            // Invoke end of press event
            inputPressEnd?.Invoke(this, Vector2.zero);
            toggle2DUIInteraction?.Invoke(this, false);
        }

        //!
        //! Helper function to reset existing operations of an input click (e.g. object selection)
        //!
        private void ClearClickInput()
        {
            // Clear monitor variables
            m_doOnce = true;

            // Invoke end of press event
            // [REVIEW]
            // Doesn't seem to be needed - are we overlooking something if leaving it out? 
            //inputPressEnd?.Invoke(this, null);

            // Force an empty selection
            // [REVIEW]
            // Is this too much of a hack?
            Vector2 point = new(-5, -5);
            objectSelectionEvent?.Invoke(this, point);
        }

        //!
        //! returns true if tap was over any UI element (it goes over all raycaster in the scene - ideally that would be GraphicRaycaster from the 2D UI)
        //!
        //! @param pos position of the tap
        //!
        private bool TappedUI(Vector2 pos)
        {
            PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
            eventDataCurrentPosition.position = pos;
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

            return results.Count > 0;
        }

        //!
        //! returns true if tap was over the 3D manipulator objects (layerMask 5 for UI)
        //!
        //! @param pos position of the tap
        //!
        private bool Tapped3DUI(Vector2 pos, int layerMask = 1 << 5)
        {
            if (Physics.Raycast(Camera.main.ScreenPointToRay(pos), out _, Mathf.Infinity, layerMask))
                return true;

            return false;
        }

        //!
        //! Function that overwrites the main cameras rotation by the attitude sensors values.
        //! Connected to VPETMap.Look which triggers when the input system fires a attitude sensor performed event.  
        //!
        private void updateCameraRotation(InputAction.CallbackContext ctx)
        {
            Transform cam = Camera.main.transform;
            cam.localRotation = ctx.ReadValue<Quaternion>() * Quaternion.Euler(0f, 0f, 180f);
            cam.rotation = m_cameraMainOffset * m_invAttitudeSensorOffset * cam.rotation;
        }

        //!
        //! Function that stores the current main camera and attitude sensors rotation offset.
        //!
        public void setCameraAttitudeOffsets()
        {
            m_cameraMainOffset = Camera.main.transform.rotation;
            if (AttitudeSensor.current != null)
                m_invAttitudeSensorOffset = Quaternion.Inverse(AttitudeSensor.current.attitude.ReadValue() * Quaternion.Euler(0f, 0f, 180f));
        }

        //!
        //! Function that fires an event for announcing change of current camera
        //!
        public void updateCameraCommand()
        {
            updateCameraUICommand.Invoke(this, true);
        }

        //!
        //! Function that toggles the main camera rotation overwrite by attitude sensor.
        //!
        private void useAttitude()
        {
            if (m_cameraControl == CameraControl.ATTITUDE)
            {
                m_inputs.VPETMap.Look.performed -= updateCameraRotation;
                m_oldcameraControl = CameraControl.NONE;
                m_cameraControl = CameraControl.NONE;
            }
            else if (m_cameraControl == CameraControl.NONE)
            {
                setCameraAttitudeOffsets();
                m_inputs.VPETMap.Look.performed += updateCameraRotation;
                m_oldcameraControl = CameraControl.ATTITUDE;
                m_cameraControl = CameraControl.ATTITUDE;
            }
        }

        public void ControllerSelect(Vector2 pos)
        {
            objectSelectionEvent?.Invoke(this, pos);
        }

       
    }

}
