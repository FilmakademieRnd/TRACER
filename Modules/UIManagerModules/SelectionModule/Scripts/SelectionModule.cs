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
//! @date 14.04.2026
//! @changed outsourced color-array id calculation into IDExtractorModule

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace tracer{
    //!
    //! Module to be used per camera that provide selection from main camera.
    //! There can be multiple instances of this class, providing local camera space selection.
    //!
    public class SelectionModule : UIManagerModule{
        //!
        //! A reference to the TRACER scene manager.
        //!
        private SceneManager m_sceneManager;
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
            m_sceneManager = core.getManager<SceneManager>();
            m_inputManager = core.getManager<InputManager>();
            
            // hookup to input events
            m_inputManager.onPrimaryInteract3dUI += SelectViaIconFunction;
            m_inputManager.onPrimaryInteractSelectable += SelectFunction;
            m_inputManager.onPrimaryInteractWorld += DeSelectFunction;
            
        }

        //!
        //! Callback from TRACER _core when Unity calls OnDestroy.
        //! Used to cleanup resources used by the PixelSelector module.
        //!
        public override void Dispose(){
            base.Dispose();

            m_inputManager.onPrimaryInteract3dUI        -= SelectViaIconFunction;
            m_inputManager.onPrimaryInteractSelectable  -= SelectFunction;
            m_inputManager.onPrimaryInteractWorld += DeSelectFunction;
        }

        //!
        //! Function to connect input managers input event for selecting a sceneObject
        //!
        //! @param sender The input manager.
        //! @param args The obj we hit, screen coorinates and input delta from the input event.
        //!
        private void SelectFunction(object sender, InputManager.InputEventHandlerArgs args){
            SceneObject validCastedSceneObject = (args.obj as GameObject)?.GetComponent<SceneObject>();
            if (validCastedSceneObject != null){
                //Debug.Log("<color=green>validCastedSceneObject</color>");
                //TODO: move into FocusObjectModule (which only listens to DoubleClick)
                //CheckDoubleClick(args.obj);

                if(manager.isThisOurSelectedObject(validCastedSceneObject)){
                    return;
                }else{
                    manager.clearSelectedObjects();
                }

                AddSelectionByRole(validCastedSceneObject);
            }else{
                //Debug.Log("<color=red>NO validCastedSceneObject</color>");
                manager.clearSelectedObjects();
            }
        }

        //!
        //! Function to connect input managers input event for selecting a sceneObject via an IconHit
        //!
        //! @param sender The input manager.
        //! @param args The ui icon gameobject we hit, screen coorinates and input delta from the input event.
        //!
        private void SelectViaIconFunction(object sender, InputManager.InputEventHandlerArgs args){
            IconUpdate validCastedIconUpdateObject = (args.obj as GameObject)?.GetComponent<IconUpdate>();
            if (validCastedIconUpdateObject != null){
                Debug.Log("<color=green>validCastedIconUpdateObject</color>");
                //TODO: move into FocusObjectModule (which only listens to DoubleClick)
                //CheckDoubleClick(args.obj);

                if(manager.isThisOurSelectedObject(validCastedIconUpdateObject.m_parentObject)){
                    return;
                }else{
                    manager.clearSelectedObjects();
                }

                AddSelectionByRole(validCastedIconUpdateObject.m_parentObject);
            }else{
                Debug.Log("<color=red>NO validCastedIconUpdateObject</color>");
                manager.clearSelectedObjects();
            }
        }
        //!
        //! Function to connect input managers input event for de-selecting (hit nothing "selectable")
        //!
        //! @param sender The input manager.
        //! @param args The gameobject we hit, screen coorinates and input delta from the input event.
        //!
        private void DeSelectFunction(object sender, InputManager.InputEventHandlerArgs args){
            //nothing to do here, in other function this could help to 
            //e.g. show a line where we point to, etc

            GameObject validCastedGameObject = (args.obj as GameObject);
            if (validCastedGameObject != null){
                //show interaction "ghost" at target hit pos on gameobject (could blink) pos

            }else{
                //show interaction "ghost" at target pos?
            }
            manager.clearSelectedObjects();
        }
        
        //!
        //! Function to add the found selected object to the manager, depending by our Role
        //!
        private void AddSelectionByRole(SceneObject clickedSceneObject){
            switch (clickedSceneObject){
                case SceneObjectCamera:
                    if (manager.activeRole == UIManager.Roles.EXPERT ||
                        manager.activeRole == UIManager.Roles.DOP)
                        manager.addSelectedObject(clickedSceneObject);
                    break;
                case SceneObjectLight:
                    if (manager.activeRole == UIManager.Roles.EXPERT ||
                        manager.activeRole == UIManager.Roles.DOP ||
                        manager.activeRole == UIManager.Roles.LIGHTING ||
                        manager.activeRole == UIManager.Roles.SET)
                        manager.addSelectedObject(clickedSceneObject);
                    break;
                default:
                    if (manager.activeRole == UIManager.Roles.EXPERT ||
                        manager.activeRole == UIManager.Roles.SET)
                        manager.addSelectedObject(clickedSceneObject);
                    break;
            }
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
            switch (obj){
                case SceneObjectCamera:
                    if (manager.activeRole == UIManager.Roles.EXPERT ||
                        manager.activeRole == UIManager.Roles.DOP)
                        manager.addSelectedObject(obj);
                    break;
                case SceneObjectLight:
                    if (manager.activeRole == UIManager.Roles.EXPERT ||
                        manager.activeRole == UIManager.Roles.LIGHTING ||
                        manager.activeRole == UIManager.Roles.SET)
                        manager.addSelectedObject(obj);
                    break;
                default:
                    if (manager.activeRole == UIManager.Roles.EXPERT ||
                        manager.activeRole == UIManager.Roles.SET)
                        manager.addSelectedObject(obj);
                    break;
            }
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
