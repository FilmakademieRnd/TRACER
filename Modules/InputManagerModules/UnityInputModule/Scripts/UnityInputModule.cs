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

        public const float MAX_DOUBLECLICK_GAP = 0.25f;

        //!
        //! Enumeration describing possible input hits.
        //!
        public enum InputLayerType{
            NONE = 10,      //we hit absolutely nothing
            UI2D = 20,      //we hit any 2d ui from a canvas
            UI3D = 30,      //we hit a selectable objects 3d ui, e.g. a world-gizmo
            COL3D = 33,     //we hit a selectable 3d world-object's collider (unity collider)
            PIX3D = 36,     //we hit a selectable 3d world-object's pixel
            SCREEN = 40     //touch/click behaviour for user-camera (rotating, moving)
        }

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
        private InputManager.SeparateBufferClass m_posBuffer;

        //!
        //! timer for our last click to check if we have a double-click
        //!
        private float m_lastClickTime;

        //!
        //! the layer we hit with our primary input, to determine a valid double click (hit the same layer)
        //!
        private InputManager.InputLayerType primaryInputLayerHit = InputManager.InputLayerType.SCREEN;

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
            m_inputs.VPETMap.OnPrimaryInputClick.performed  += ProcessPrimaryInputClick;

            //variable init
            m_posBuffer = new InputManager.SeparateBufferClass();
        }

        //!
        //! Destructor, cleaning up event registrations. 
        //!
        public override void Dispose(){
            base.Dispose();

            // Unsubscribe
            m_inputs.VPETMap.Position.performed             -= ProcessPositionInput;
            m_inputs.VPETMap.OnPrimaryInputClick.performed  -= ProcessPrimaryInputClick;
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
            //necessary to set pos within the input module? -> NO ONLY WRAPPER

            m_posBuffer.SetBufferOnce(m_pos);
            // Update buffer
            m_posBuffer.OverrideBuffer(m_pos);
        }

        //!
        //! Input click/touch
        //! mapped to primary touch and left mouse click as tap interaction (below 0.2s, no hold)
        //! ignores 2d ui hits (they have their own event)
        //!
        private void ProcessPrimaryInputClick(InputAction.CallbackContext c){

            //TODO: make this 'layerWeAre' globally available
            //  then utilize within Dual/Triple Move (Drag/Pinch) Input
            //layer may get overwritten from further input
            //e.g. touch pinch may use 2d (timeline) or world (cam zoom) if we dont want objects to get scaled by this
            //call and set on IM before anything else
            InputManager.InputLayerType layerWeAreAt = DetermineLayerWeHit();
            //? maybe check all layer and send all information (2dui hit, 3dui hit, selectable hit)

            if (WasDoubleClick(layerWeAreAt)){
                ProcessDoubleClickInput(c);
                return;
            }

            SetLastClickTime(layerWeAreAt);
            

            //--- DEBUG
            Debug.Log("<color=yellow>primary input click</color>");
            Ray debugRay = Camera.main.ScreenPointToRay(m_pos);
            Debug.DrawRay(debugRay.origin, debugRay.direction*100, Color.yellow, 2f);
            //---------- END DEBUG

            switch (layerWeAreAt){
                case InputManager.InputLayerType.UI:
                    //Since we use Unity's Canvas and UI-Elements for Events, we straight skip any input hitting any of these
                    Debug.Log("<color=red>primary input @ 2d ui</color>");
                    return;
                case InputManager.InputLayerType.WORLD:
                    Debug.Log("<color=orange>primary input @ 3d world ui</color>");
                    return;
                case InputManager.InputLayerType.SCREEN:
                    //further checks with collider & pixel functions we have
                    Debug.Log("<color=cyan>primary input @ screen (hit nothing)</color>");
                    return;
            }

        }

        //!
        //! our own double click check via primary click/tap
        //!
        private void ProcessDoubleClickInput(InputAction.CallbackContext c){
            
            //--- DEBUG
            Debug.Log("<color=yellow>primary input double-click</color>");
            Ray debugRay = Camera.main.ScreenPointToRay(m_pos);
            Debug.DrawRay(debugRay.origin, debugRay.direction*100, Color.yellow, 2f);
            //---------- END DEBUG

            //manager.ProcessFocus(SceneObject, pos) -> check if Focus is allowed -> invoke
            //  invoke will be executed within e.g. CameraNavigationModule, SelectionModule

            ResetLastClickTime();

        }
        #endregion

        #region HELPER

        //!
        //! use our primary input position (mouse/touch) to check what we could have hit
        //!
        private InputManager.InputLayerType DetermineLayerWeHit(){
            if(IsPosOver2dUiElement(m_pos))
                return InputManager.InputLayerType.UI;
            if(IsPosOver3dUiElement(m_pos))
                return InputManager.InputLayerType.WORLD;
            
            //TODO
            // do we need to use the current SelectionModule functionality (SelectFunction")
            // via collider and pixel for further tests? YES (take care to not call it excessively!)

            return InputManager.InputLayerType.SCREEN;
        }

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

        //!
        //! returns true if pos is over any 3D manipulator object (layerMask 5 for UI)
        //!
        private bool IsPosOver3dUiElement(Vector2 pos){
            return Physics.Raycast(Camera.main.ScreenPointToRay(pos), out _, Mathf.Infinity, 1 << 5);
        }


        //!
        //! sets current primary input click time and layer for further double-click checks
        //! TODO: remove layertype and put into other function!
        private void SetLastClickTime(InputManager.InputLayerType _primaryInputLayerHit){
            primaryInputLayerHit = _primaryInputLayerHit;
            m_lastClickTime = Time.time; 
        }

        //!
        //! reset the time we use to check for double click in various cases
        //! e.g. if we executed a double click or performed other inputs
        //!
        private void ResetLastClickTime(){
            m_lastClickTime = 0; 
        }

        //!
        //! make a double click check (time within gap, same layer as before)
        //! todo: we could also add a position-delta to check
        //!
        private bool WasDoubleClick(InputManager.InputLayerType _primaryInputLayerHit){
            if(primaryInputLayerHit != _primaryInputLayerHit)   //if layer is different, reset time - no double-click!
                ResetLastClickTime();
            return Time.time - m_lastClickTime < MAX_DOUBLECLICK_GAP; 
        }
        #endregion

        /*
        #region MOUSE

        #endregion

        #region KEYBOARD

        #endregion

        #region TOUCH

        #endregion

        #region CONTROLLER

        #endregion
        */

    }
}