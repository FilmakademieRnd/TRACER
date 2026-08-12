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

//! @file "UICreator3DPointOnFloor.cs"
//! @brief early implementation of TRACER 3D UI point on floor module
//! @author Paulo Scatena
//! @author Thomas Krüger
//! @version 1
//! @date 19.05.2026
//! @revision updated with overhauled input manager, although this behaviour is not used and should absolutely be revised!
// NOT ANYWHERE NEAR USABLE OR CORRECT...

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace tracer{
    //!
    //! early implementation of TRACER 3D UI point on floor module
    //!
    public class UICreator3DPointOnFloor : UIManagerModule
    {
        // Selected object to manipulate
        private SceneObject selObj;

        //Vector3 planeVec = Vector3.zero;
        Plane helperPlane;
        //GameObject manipulator;

        GameObject pointToMoveModifier;
        GameObject noClickCanvas;

        Vector3 lastHitPoint;
        Vector3 targetTranslation;
        readonly float translationDamping = 1.0f;

        //!
        //! A reference to the TRACER input manager.
        //!
        private InputManager m_inputManager;

        //!
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public UICreator3DPointOnFloor(string name, Manager manager) : base(name, manager){
            // Disable module
            load = false;
        }

        //!
        //! Init m_callback for the UICreator3DPointOnFloor module.
        //!
        protected override void Init(object sender, EventArgs e){
            
            Debug.Log("Init point on floor module");


            // Subscribe to selection change
            manager.selectionChanged += SelectionUpdate;
            // TODO: add a "button" within the above function

            // Grabbing from the input manager directly
            m_inputManager = core.getManager<InputManager>();
            m_inputManager.clickOtherEvent  += ClickFunction;
            m_inputManager.dragOtherEvent   += DragFunction;

            // Instantiate widget
            InstantiateModifier();

            // make the plane on ground
            helperPlane = new Plane(Vector3.up, new Vector3(0,-2,0));
        }

        //!
        //! Destructor, cleaning up event registrations. 
        //!
        public override void Dispose(){
            base.Dispose();

            if(load || m_inputManager == null)
                return;

            m_inputManager.clickOtherEvent  -= ClickFunction;
            m_inputManager.dragOtherEvent   -= DragFunction;
        }

        //!
        //! Function to connect input managers input event for moving a selection to point on floor
        //!
        //! @param evt the InputData
        //!
        private void ClickFunction(object sender, InputManager.ClickEventArgs evt){  
            if (selObj == null)
                return;

            if(evt.Level != InputManager.InputLevel.Primary)
                return;
            
            // TODO: utilize EvaluationHelper!

            //MOVE OBJECT TO DESTINATION
            Ray ray = Camera.main.ScreenPointToRay(evt.Position);
            if (!helperPlane.Raycast(ray, out float enter))
                return;

            // check phase
            switch (evt.State){
                case InputManager.InputState.Started:
                case InputManager.InputState.Ongoing:
                case InputManager.InputState.Canceled:
                    break;
                case InputManager.InputState.Ended:
                    // TODO: utilize EvaluationHelper!
                    targetTranslation = ray.GetPoint(enter);
                    // using its monobehaviour quality
                    //selObj.StopAllCoroutines();
                    selObj.StartCoroutine(SmoothMove());
                    break;
            }
        }

        //!
        //! Function to connect input managers input event for moving a selection to point on floor
        //!
        //! @param evt the InputData
        //!
        private void DragFunction(object sender, InputManager.DragEventArgs evt){  
            if (selObj == null)
                return;

            if(evt.Level != InputManager.InputLevel.Primary)
                return;
            
            // TODO: utilize EvaluationHelper!

            //MOVE OBJECT TO DESTINATION
            Ray ray = Camera.main.ScreenPointToRay(evt.Position);
            if (!helperPlane.Raycast(ray, out float enter))
                return;

            //Get the point that is clicked
            Vector3 hitPoint = ray.GetPoint(enter);
            // check phase
            switch (evt.State){
                case InputManager.InputState.Started:
                    // show gizmo
                    pointToMoveModifier.transform.position = hitPoint;
                    pointToMoveModifier.SetActive(true);
                    break;
                case InputManager.InputState.Ongoing:

                    // move manip
                    pointToMoveModifier.transform.position = hitPoint;
                    lastHitPoint = hitPoint;
                    lastHitPoint.y = selObj.transform.position.y;
                    break;
                case InputManager.InputState.Canceled:
                case InputManager.InputState.Ended:

                    pointToMoveModifier.SetActive(false);
                    targetTranslation = lastHitPoint;

                    // using its monobehaviour quality
                    //selObj.StopAllCoroutines();
                    selObj.StartCoroutine(SmoothMove());
                    break;
            }
        }


        // Soft translate
        IEnumerator SmoothMove(){
            float time = 0;
            // We create a loop to control for how many time it will run
            while (time <= 3){
                time += Time.deltaTime;
                Debug.Log(time);
                selObj.transform.position = Vector3.Lerp(selObj.transform.position, targetTranslation, Time.deltaTime * translationDamping);
                yield return null;
            }
        }

        //!
        //! Updates the selection with the first selected object available
        //! Being called when selection has changed.
        //!
        private void SelectionUpdate(object sender, List<SceneObject> sceneObjects){

            // Log
            //Debug.Log("Selection changed");

            if (sceneObjects.Count > 0){
                // Grab object
                selObj = sceneObjects[0];
                //Debug.Log(selObj);

                // Bring up the non click screen
                noClickCanvas.SetActive(true);
            }
            //else // empty selection
            //{
            //    HideAxes();
            //    modeTRS = -1;
            //    SetManipulatorMode(null, -1);
            //}

        }

        private void InstantiateModifier(){
            // Click widget
            GameObject resourcePrefab = Resources.Load<GameObject>("Prefabs/PointToMoveModifier");
            pointToMoveModifier = GameObject.Instantiate(resourcePrefab);
            pointToMoveModifier.SetActive(false);

            // Anti click canvas - temporary hack
            resourcePrefab = Resources.Load<GameObject>("Prefabs/TransparentCanvas");
            noClickCanvas = GameObject.Instantiate(resourcePrefab);
            noClickCanvas.SetActive(false);

            //do via disallow (de-)selection and 
        }

    }

}