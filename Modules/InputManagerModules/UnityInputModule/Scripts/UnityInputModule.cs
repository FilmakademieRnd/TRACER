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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;  //one has to add this to "Assembly Definition Reference" in the "ModulesAssembly"

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
        //! The latest main input position (primary touch, mouse pos)
        //!
        private Vector2 m_pos;
        //!
        //! the position buffer for further calculations (two finger camera manipulation e.g. zoom, ...)
        //!
        public InputManager.SeparateBufferClass m_posBuffer;

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
            //enable input
            m_inputs = new Inputs();
            m_inputs.VPETMap.Enable();

            //add listener
            m_inputs.VPETMap.Position.performed             += ProcessPositionInput;
            m_inputs.VPETMap.OnPrimaryPointerDown.performed += ProcessMainTriggerDown;
        }

        #endregion

        #region GENERAL
        //!
        //! tracks the positions of our primary input (primary touch, mouse pos)
        //! and writes them into a buffer to allow further calculations (delta, speed, etc)
        //!
        private void ProcessPositionInput(InputAction.CallbackContext obj){ 
            // Get the position
            m_pos = m_inputs.VPETMap.Position.ReadValue<Vector2>();

            m_posBuffer.SetBufferOnce(m_pos);
            // Update buffer
            m_posBuffer.OverrideBuffer(m_pos);
        }

        //!
        //! Input touch/click
        //! mapped to primary touch and left mouse click as tap interaction (below 0.2s, no hold)
        //! ignores 2d ui hits (they have their own event)
        //!
        private void ProcessMainTriggerDown(InputAction.CallbackContext c){
            
            //--- DEBUG
            Debug.Log("<color=yellow>MAIN TRIGGER DOWN</color>");
            Ray debugRay = Camera.main.ScreenPointToRay(m_pos);
            Debug.DrawRay(debugRay.origin, debugRay.direction*100, Color.yellow, 2f);
            //---------- END DEBUG

            //Since we use Unity's Canvas and UI-Elements for Events, we straight skip any input hitting any of these
            if(IsPosOver2dUiElement(m_pos))
                return;

            /*if(TappedUI(point)){
                Debug.Log("HIT 2D UI");
                m_inputLayerType = InputLayerType.UI;
                inputPressStartedUI?.Invoke(this, point);
            }else if(Tapped3DUI(point)){
                Debug.Log("HIT 3D UI");
                m_inputLayerType = InputLayerType.WORLD;
                inputPressStarted?.Invoke(this, point);
            }else{
                Debug.Log("hit nothing");
                m_inputLayerType = InputLayerType.SCREEN;
                inputPressStarted?.Invoke(this, point);
            }*/

        }
        #endregion

        #region HELPER
        //!
        //! returns true if pos is over any UI element
        //!  (it goes over all raycaster in the scene - ideally that would be GraphicRaycaster from the 2D UI)
        //!
        //! @param pos position of the click/tap
        //!
        private bool IsPosOver2dUiElement(Vector2 pos){
            PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current){ position = pos };
            List<RaycastResult> m_raycastList = new List<RaycastResult>(5);
            EventSystem.current.RaycastAll(eventDataCurrentPosition, m_raycastList);

            return m_raycastList.Count > 0;
        }
        #endregion


        #region MOUSE

        #endregion

        #region KEYBOARD

        #endregion

        #region TOUCH

        #endregion

        #region CONTROLLER

        #endregion

    }
}