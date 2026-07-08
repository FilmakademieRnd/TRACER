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

//! @file "SelectionModule.cs"
//! @brief implementation of the TRACER SelectionModule, for 3D selectable SceneObjects
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @author Thomas Krüger
//! @version 1
//! @date 19.05.2026
//! @changed moved role-dependent selection into UIManager selection!

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace tracer{
    //!
    //! Module to be used per camera that provide selection from main camera.
    //! There can be multiple instances of this class, providing local camera space selection.
    //!
    public class SelectionModule : UIManagerModule{

        //!
        //! A reference to the TRACER input manager.
        //!
        private InputManager m_inputManager;
        
        //!
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public SelectionModule(string name, Manager manager) : base(name, manager){
        }

        //! 
        //! Function called when Unity initializes the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Init(object sender, EventArgs e){
            m_inputManager = core.getManager<InputManager>();
            
            // hookup to input events
            // m_inputManager.onPrimaryInteract3dUI += SelectViaIconFunction;
            // m_inputManager.onPrimaryInteractSelectable += SelectFunction;
            // m_inputManager.onPrimaryInteractWorld += DeSelectFunction;
            m_inputManager.Subscribe<InputManager.ClickOtherEvent>(SelectFunction);
            //add DoubleClick
            //add drag in terms of drawing a rectangle for multi-selection with different function
            
        }

        //!
        //! Callback from TRACER _core when Unity calls OnDestroy.
        //! Used to cleanup resources used by the PixelSelector module.
        //!
        public override void Dispose(){
            base.Dispose();

            // m_inputManager.onPrimaryInteract3dUI        -= SelectViaIconFunction;
            // m_inputManager.onPrimaryInteractSelectable  -= SelectFunction;
            // m_inputManager.onPrimaryInteractWorld += DeSelectFunction;
            m_inputManager.Unsubscribe<InputManager.ClickOtherEvent>(SelectFunction);
        }

        //!
        //! Function to connect input managers input event for selecting a sceneObject
        //!
        //! @param evt the InputData
        //!
        private void SelectFunction(InputManager.ClickOtherEvent evt){

            if (evt.Data.Level != InputManager.InputLevel.Primary) return;

            // check phase
            switch (evt.Data.State){
                case InputManager.InputState.Started:
                case InputManager.InputState.Ongoing:
                case InputManager.InputState.Canceled:
                    //nothing to do
                    break;
                case InputManager.InputState.Ended:
                    //check via evaluation helper what we hit
                    //this should already be buffered at our pos!
                    break;
            }

            SceneObject sceneObject = GetSceneObjectAtPosition(evt.Data.Position);

            if (sceneObject != null){
                
                if(manager.isThisOurSelectedObject(sceneObject)){
                    return;
                }else{
                    manager.clearSelectedObjects();
                }

                manager.addSelectedObject(sceneObject);
            }else{
                Debug.Log("<color=red>no valid SceneObject to select</color>");
                manager.clearSelectedObjects();
            }
        }

        private SceneObject GetSceneObjectAtPosition(Vector2 screenPos) {
            //Check for IconUpdate
            //[!REVISE]
            GameObject hitObject = EvaluationHelper.Instance.EvaluateGameObject(screenPos);

            if (hitObject) {
                IconUpdate icon = hitObject.GetComponent<IconUpdate>();
                if(icon && icon.m_parentObject)
                    return icon.m_parentObject;
            }

            return EvaluationHelper.Instance.EvaluateSceneObject(screenPos);
        }
        
        //!
        //! Function to check for a double-click/tap to focus on an object
        //! TODO: move into FocusObjectModule (which only listens to DoubleClick)
        //!
        /*private void CheckDoubleClick(SceneObject obj){
            if(!obj){
                manager.setLastClickedObject(null);
                return;
            }

            //Double-Click on the same obj -> focus on it
            if(m_inputManager.WasDoubleClick()){
                if(manager.LastClickedObject == obj){  //works with locked objects as well!
                    manager.focusOnLastClickedObject();
                }
            }
            manager.setLastClickedObject(obj);
        }*/

        //!
        //! Function to simulate Select
        //!
        public void SetSelectedObjectViaScript(SceneObject obj){
            if(!obj || manager.isThisOurSelectedObject(obj))
                return;
            manager.clearSelectedObjects();
            
            if(EvaluationHelper.Instance.IsSelectableWithRole(obj, manager.activeRole))
                manager.addSelectedObject(obj);
        }
        
        //!
        //! Retrieve the selectables present at the current location in camera screenspace, if any.
        //! 
        //! @param screenPosition The position to get the selectable at.
        //! @return The selectables at the specified screen position or null if there is none.
        //!
        public List<SceneObject> GetSelectableInRect(RectInt screenRect){
            int xMin = screenRect.xMin;
            int xMax = screenRect.xMax;
            int yMin = screenRect.yMin;
            int yMax = screenRect.yMax;

            HashSet<SceneObject> sceneObjects = new HashSet<SceneObject>();
            for (int x = xMin; x < xMax; x++){
                for (int y = yMin; y < yMax; y++){
                    sceneObjects.Add(manager.GetSelectableAtPixel(x, y));
                }
            }
            
            return sceneObjects.ToList();
        }
    }
}
