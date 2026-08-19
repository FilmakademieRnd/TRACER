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

//! @file "UICreator3DModule.cs"
//! @brief implementation of TRACER 3D UI scene creator module
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @author Paulo Scatena
//! @version 0
//! @date 07.03.2022

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace tracer
{
    //!
    //! implementation of TRACER 3D UI scene creator module
    //!
    public class UICreator3DModule : UIManagerModule
    {
        //!
        //! Reference to the translation manipulator
        //!
        private GameObject manipT;

        //!
        //! Reference to the rotation manipulator
        //!
        private GameObject manipR;

        //!
        //! Reference to the scale manipulator
        //!
        private GameObject manipS;

        //!
        //! Active (clicked) manipulator object
        //! 
        GameObject manipulator;

        //!
        //! Reference to the last active manipulator (for hide/unhide cases)
        //!
        private GameObject lastActiveManip;

        //!
        //! Selected object to be manipulated
        //!
        private SceneObject selObj;

        //!
        //! List of selected objects to be manipulated (for multi-selection)
        //!
        List<SceneObject> selObjs = new();

        //!
        //! List of the positional offset of selected objects (for spatial preservation)
        //!
        List<Vector3> objOffsets = new();

        // Review: will it DEFINITELY always be 0, 1, 2 or should it be checked? (currently it is checked)
        //!
        //! Index of parameter T
        //!
        private int tIndex;
        //!
        //! Index of parameter R
        //!
        private int rIndex;
        //!
        //! Index of parameter S
        //!
        private int sIndex;

        //!
        //! Position (T) parameter
        //!
        Parameter<Vector3> posParam;

        //!
        //! Rotation (R) parameter
        //!
        Parameter<Quaternion> rotParam;

        //!
        //! Scale (S) parameter
        //!
        Parameter<Vector3> scaParam;

        //!
        //! UI scale multiplier
        //!
        float uiScale = 1;

        //!
        //! Auxiliary vector for raycasting
        //!
        Vector3 planeVec = Vector3.zero;

        //!
        //! Auxiliary plane for raycasting
        //!
        Plane helperPlane;


        //!
        //! stored variables for drag viz
        //!
        private Vector3 helperPlaneCenter, lockedlocalAxis, lockedGlobalAxis;
        private bool isSingleAxis = false;

        //!
        //! Internal reference of manipulator parts - translate X
        //!
        GameObject manipTx;

        //!
        //! Internal reference of manipulator parts - translate Y
        //!
        GameObject manipTy;

        //!
        //! Internal reference of manipulator parts - translate Z
        //!
        GameObject manipTz;

        //!
        //! Internal reference of manipulator parts - scale X
        //!
        GameObject manipSx;

        //!
        //! Internal reference of manipulator parts - scale Y
        //!
        GameObject manipSy;

        //!
        //! Internal reference of manipulator parts - scale Z
        //!        
        GameObject manipSz;

        //!
        //! Internal reference of manipulator parts - scale XY
        //!
        GameObject manipSxy;

        //!
        //! Internal reference of manipulator parts - scale XZ
        //!        
        GameObject manipSxz;

        //!
        //! Internal reference of manipulator parts - scale YZ
        //!
        GameObject manipSyz;

        //!
        //! 
        //!

        //!
        //! Auxiliary vector for storing the click offset
        //! 
        Vector3 hitPosOffset = Vector3.zero;


        //!
        //! Reference to initial scale value for parameter change
        //!
        Vector3 initialSca = Vector3.one;

        //!
        //! Stores manipulator position in its object local space (to save multiple calls)
        //!
        Vector3 localManipPosition;

        //!
        //! Buffer quaternion for visualizing multi object rotation
        //!
        Quaternion visualRot = Quaternion.identity;

        //!
        //! Mode of operation of TRS manipulator
        //!
        int modeTRS = -1;

        //!
        //! store mode to restore when unhiding
        //!
        private int savedTrsMode = -1;

        //!
        //! Auxiliary preconstructed vector - XY plane
        //!
        readonly Vector3 vecXY = new(1, 1, 0);

        //!
        //! Auxiliary preconstructed vector - XZ plane
        //!
        readonly Vector3 vecXZ = new(1, 0, 1);

        //!
        //! Auxiliary preconstructed vector - YZ plane
        //!
        readonly Vector3 vecYZ = new(0, 1, 1);

        //!
        //! Reference of main camera
        //!
        Camera mainCamera;

        //!
        //! do so only when selection changed, to not do this runtime all the time
        //! add to do this for ALL other calcs in GetModifierScale and subscribe to changes of FielOfView as well!
        //! 
        private float camMathValues;

        //!
        //! Event emitted when parameter has changed
        //!
        public event EventHandler<AbstractParameter> doneEditing;

        //!
        //! A reference to the TRACER input manager.
        //!
        private InputManager m_inputManager;

        //only used for manipulation mode Viewport - to update the gizmo alignemnt accordingly
        private Vector3 lastCamPos;
        private Quaternion lastCamRot;

        //!
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public UICreator3DModule(string name, Manager manager) : base(name, manager){

        }

        //!
        //! Destructor, cleaning up event registrations. 
        //!
        public override void Dispose()
        {
            base.Dispose();

            // Unsubscribe
            manager.selectionChanged -= SelectionUpdate;
            manager.settings.uiScale.hasChanged -= updateUIScale;

            // NEW INPUT EVENTS
            m_inputManager.dragOtherEvent -= DragFunction;
            m_inputManager.clickOtherEvent -= ClickFunction;

            /*m_inputManager.fingerGestureEvent -= updateGizmoScale;
            m_inputManager.updateCameraUICommand -= updateGizmoScale;*/
            core.updateEvent -= OnCoreUpdateEvent;

            UICreator2DModule UI2DModule = manager.getModule<UICreator2DModule>();
            UIManager m_UIManager = core.getManager<UIManager>();
            if (UI2DModule != null)
            {
                UI2DModule.parameterChanged -= SetManipulatorMode;
            }
            manager.uiCameraLockChanged -= SetCameraManipulator;

            // [REVIEW]
            // Direct access to a module should be prevented!
            //this.doneEditing -= manager.core.getManager<SceneManager>().getModule<UndoRedoModule>().addHistoryStep;
            //this.doneEditing -= core.getManager<NetworkManager>().getModule<UpdateSenderModule>().queueUndoRedoMessage;
        }

        //!
        //! Init m_callback for the UICreator3D module.
        //! Called after constructor. 
        //!
        protected override void Init(object sender, EventArgs e){
            //Debug.Log("Init 3D module");
            mainCamera = Camera.main;

            // Subscribe to selection change
            manager.selectionChanged += SelectionUpdate;
            manager.manipulationLayerChanged += ManipulationLayerChanged;

            // Subscribe to manipulator change
            UICreator2DModule UI2DModule = manager.getModule<UICreator2DModule>();
            UI2DModule.parameterChanged += SetManipulatorMode;

            // Subscribe to camera change?
            // manager.uiCameraLockChanged += SetCameraManipulator;
            // subscribe only within SelectionUpdate

            // Grabbing from the input manager directly
            m_inputManager = core.getManager<InputManager>();

            // NEW INPUT EVENTS
            m_inputManager.dragOtherEvent += DragFunction;
            // [TEST] implementation to change axis manipulation via right-click too
            m_inputManager.clickOtherEvent += ClickFunction;

            // Hookup to input events
            /*m_inputManager.fingerGestureEvent += updateGizmoScale;
            m_inputManager.updateCameraUICommand += updateGizmoScale;*/

            //TODO: have UI (?) Event for CamChanged (pos)
            //just do this in update
            core.updateEvent += OnCoreUpdateEvent;

            // Grabbing scene scale
            uiScale = manager.settings.uiScale.value;
            manager.settings.uiScale.hasChanged += updateUIScale;

            // Instantiate TRS widgest but keep them hidden
            InstantiateAxes();
            HideAxes();

            _dragViz = new DragVisualizer();
            _dragRotateViz = new DragRotateVisualizer();

            this.doneEditing += manager.core.getManager<SceneManager>().getModule<UndoRedoModule>().addHistoryStep;
            this.doneEditing += core.getManager<NetworkManager>().getModule<UpdateSenderModule>().queueUndoRedoMessage;
        }
        

        #region NEW INPUT EVENTS

        //!
        //! Callback from TRACER _core when Unity calls it's render update
        //! [!REVISE] only do on updates? cam update, gizmo manipulation, animation?
        //!
        private void OnCoreUpdateEvent(object sender, EventArgs e){
            if(!selObj)
                return;

            UpdateManipScale();

            if(manager.ManipulationLayer == UIManager.ManipulationLayerEnum.VIEWPORT && lastActiveManip) {
                Transform camTr = mainCamera.transform;
                if(camTr.position != lastCamPos || camTr.rotation != lastCamRot) {
                    UpdateGizmoAxisLayer(lastActiveManip.transform, selObj.transform, Camera.main.transform, manager.ManipulationLayer);
                }
                lastCamPos = camTr.position;
                lastCamRot = camTr.rotation;
            }

        }

        private DragVisualizer _dragViz;
        private DragRotateVisualizer _dragRotateViz;

        //!
        //! Function to connect input managers input event for dragging a sceneObjects gizmo
        //!
        //! @param evt the InputData
        //!
        private void DragFunction(object sender, InputManager.DragEventArgs evt){

            // [REVIEW]
            // if no specific gizmo is shown, use prim - move, sec - rot, tert - scale
            // (if so, add function parameter to use as _manipulator type_)
            // update viz accordingly? 

            // right now, only Primary
            if (evt.Level != InputManager.InputLevel.Primary) return;

            // check phase
            switch (evt.State){
                case InputManager.InputState.Started:
                    //Debug.Log("Primary Drag Started");
                    SetupManipulatorForTransformations(evt.StartPosition);
                    CalculateStartOffset(evt.StartPosition);
                    //better for viz?
                    //HideGizmo();
                    break;
                case InputManager.InputState.Ongoing:
                    //Debug.Log("Primary Drag ongoing");
                    ExecuteManipulatorTransformation(evt.Position);
                    break;
                case InputManager.InputState.Canceled:
                case InputManager.InputState.Ended:
                    //Debug.Log("Primary Drag ended");
                    //TODO: dont execute via controller-right-stick drag, since we only every rotate the cam with it
                    FinalizeManipulatorTransformation(evt.Position);
                    //better for viz?
                    //ShowGizmo();
                    break;
            }
        }

        //!
        //! helper to cycle through axis manipuation modes with click, not only controller or keyboard
        //!
        private void ClickFunction(object sender, InputManager.InputEventArgs evt){

            // right now, only Primary
            if (evt.Level != InputManager.InputLevel.Secondary) return;

            // check phase
            switch (evt.State){
                case InputManager.InputState.Started:
                case InputManager.InputState.Ongoing:
                case InputManager.InputState.Canceled:
                    break;
                case InputManager.InputState.Ended:
                    manager.CycleManipulationMode();
                    break;
            }
        }

        #endregion




        //!
        //! Function to select the manipulator and prepare for transformations.
        //! Called with the start of click from InputManager
        //! @param sender m_callback sender
        //! @param e event reference
        //!
        private void SetupManipulatorForTransformations(Vector2 point){
            // grab the hit manip
            manipulator = EvaluationHelper.Instance.EvaluateManipulator(point);
            if (manipulator){
                Debug.Log("HIT MANIPULATOR");
                
                // make a plane based on it
                planeVec = manipulator.transform.forward;
                helperPlaneCenter = selObj.transform.position; //manipulator.GetComponent<Collider>().bounds.center;
                helperPlane = new Plane(planeVec, helperPlaneCenter);
                //Debug.DrawRay(center, planeVec * 10, Color.red, 1);

                // if root modifier - plane normal is camera axis
                if (manipulator.tag == "gizmoCenter")
                    helperPlane = new Plane(mainCamera.transform.forward, helperPlaneCenter);

                // HACK - if translate single axis - plane normal is camera axis projected on the axis plane
                if (manipulator == manipTx || manipulator == manipTy || manipulator == manipTz ||
                    manipulator == manipSx || manipulator == manipSy || manipulator == manipSz
                )
                    helperPlane = new Plane(Vector3.ProjectOnPlane(mainCamera.transform.forward, manipulator.transform.up), helperPlaneCenter);

                // semi hack - if manip = main rotator - free rotation
                if (manipulator == manipR)
                    //{
                    //freeRotationColl = manipulator.GetComponent<Collider>();
                    // make the collision plane a bit in front of the object
                    helperPlane = new Plane(mainCamera.transform.forward, helperPlaneCenter - .2f * Vector3.Distance(mainCamera.transform.position, selObj.transform.position) * mainCamera.transform.forward);
                //}

                // store manipulator position in its object local space (to save multiple calls)
                localManipPosition = selObj.transform.parent.transform.InverseTransformPoint(manipulator.transform.position);
            }
            
            // hack - storing initial scale in case of ui operation
            if (selObj){
                //Debug.Log("<color=green> selected obj "+selObj.gameObject.name+"</color>");
                Parameter<Vector3> sca = (Parameter<Vector3>)selObj.parameterList[sIndex];
                initialSca = sca.value;
            }
        }


        private void CalculateStartOffset(Vector2 clickPos) {
            if(!manipulator)
                return;

            //Create a ray from the Mouse click position
            Ray ray = mainCamera.ScreenPointToRay(clickPos);
            if (helperPlane.Raycast(ray, out float hitDistance)){
                //Get the point that is clicked
                Vector3 hitPoint = ray.GetPoint(hitDistance);
                Vector3 projectedVec = hitPoint;

                switch (modeTRS){ 
                    // drag object - translate
                    case 0: // multi obj dev
                        // dirty temp hack - identify if single axis
                        isSingleAxis = manipulator == manipTx || manipulator == manipTy || manipulator == manipTz;
                        lockedlocalAxis = lockedGlobalAxis = Vector3.zero;
                        if (isSingleAxis){
                            lockedGlobalAxis = new Vector3(manipulator == manipTx ? 1 : 0, manipulator == manipTy ? 1 : 0, manipulator == manipTz ? 1 : 0);
                            Transform manipulatorRoot = manipulator.transform.root;
                            lockedlocalAxis = 
                                manipulatorRoot.right * lockedGlobalAxis.x +
                                manipulatorRoot.up * lockedGlobalAxis.y + 
                                manipulatorRoot.forward * lockedGlobalAxis.z;
                            
                            projectedVec = Vector3.Project(hitPoint - manipulator.transform.position, manipulator.transform.up) + manipulator.transform.position;
                        }

                        // store the offset between clicked point and center of obj
                        hitPosOffset = projectedVec - manipulator.transform.position;
                        // for multi move
                        foreach (SceneObject obj in selObjs){
                            objOffsets.Add(obj.transform.position - manipulator.transform.position);
                        }
                        break;
                    // drag rotate - manip version
                    case 1:
                        // Convert to object space
                        hitPoint = selObj.transform.parent.transform.InverseTransformPoint(hitPoint);
                        hitPosOffset = hitPoint - localManipPosition;
                        rotationDragWorldStartVec = selObj.transform.parent.TransformDirection(hitPosOffset);
                        break;
                    // drag object - scale
                    case 2:
                        // temp hack - identify if single axis
                        if (manipulator == manipSx || manipulator == manipSy || manipulator == manipSz){
                            projectedVec = Vector3.Project(hitPoint - manipS.transform.position, manipulator.transform.up) + manipS.transform.position;
                        }

                        hitPosOffset = projectedVec;
                        break;
                }
            }
            m_inputManager.SetAllowCamNavigation(false);
        }

        private Vector3 rotationDragWorldStartVec;  //used just for visualization of how "far" we drag the rotation

        //!
        //! Function to be performed on click/touch drag
        //! It subscribes (at PressStart) to the event triggered at every position update from InputManager
        //! @param sender m_callback sender
        //! @param e event reference
        //!
        private void ExecuteManipulatorTransformation(Vector2 point){

            //Debug.Log("<color=black>Move</color>");

            if (manipulator == null || selObj == null)
                return;

            //Debug.Log("<color=green> selObj "+selObj.gameObject.name+"</color>");
            //Debug.Log("<color=green> mode:  "+modeTRS+"</color>");
            
            //Create a ray from the Mouse click position
            Ray ray = mainCamera.ScreenPointToRay(point);
            if(!helperPlane.Raycast(ray, out float enter))
                return;

            //Get the point that is clicked
            Vector3 hitPoint = ray.GetPoint(enter);

            // drag object - translate
            if (modeTRS == 0){ // multi obj dev
                //Debug.Log("<color=green> ray hit helperPlane!</color>");

                Vector3 projectedVec = hitPoint;
                // dirty temp hack - identify if single axis
                if (isSingleAxis){    
                    projectedVec = Vector3.Project(hitPoint - manipulator.transform.position, manipulator.transform.up) + manipulator.transform.position;
                }

                // adjust
                projectedVec -= hitPosOffset;

                _dragViz.UpdateVisuals(helperPlaneCenter, helperPlane.normal, projectedVec, isSingleAxis, lockedlocalAxis, lockedGlobalAxis);

                //Debug.Log("<color=green> projectedVec: "+projectedVec+"</color>");

                // Actual translation operation
                // For a single object
                if (selObjs.Count == 1){
                    Vector3 localVec = selObj.transform.parent.InverseTransformPoint(projectedVec);
                    Parameter<Vector3> pos = (Parameter<Vector3>)selObj.parameterList[tIndex];
                    pos.setValue(localVec);
                    //Debug.Log("<color=green> set value for single selection!</color>");
                }
                // For multiple objects
                else{
                    for (int i = 0; i < selObjs.Count; i++){
                        Vector3 localVec = selObjs[i].transform.parent.InverseTransformPoint(projectedVec + objOffsets[i]);
                        Parameter<Vector3> pos = (Parameter<Vector3>)selObjs[i].parameterList[tIndex];
                        pos.setValue(localVec);
                    }
                }
            }


            // drag rotate - manip version
            else if (modeTRS == 1){
                // Convert to object space
                hitPoint = selObj.transform.parent.InverseTransformPoint(hitPoint);
                // get orientation quaternion
                Quaternion rotQuat = new Quaternion();

                rotQuat.SetFromToRotation(hitPosOffset, hitPoint - localManipPosition);

                // Strengthen free rotation
                bool isFreeRotation = false;
                if (manipulator == manipR){
                    rotQuat *= rotQuat;
                    isFreeRotation = true;
                }

                //****** ONLY FOR VISUALIZATION
                // Convert the local drag vectors into World Space so the visualizer can draw them accurately
                Vector3 worldCurrentVec = selObj.transform.parent.TransformDirection(hitPoint - localManipPosition);

                // Call the visualizer
                _dragRotateViz.UpdateVisuals(
                    helperPlaneCenter, 
                    helperPlane.normal, 
                    rotationDragWorldStartVec, 
                    worldCurrentVec, 
                    isFreeRotation,
                    selObj.transform
                );
                //*****************************

                // Actual rotation operation
                // For a single object
                if (selObjs.Count == 1){
                    Parameter<Quaternion> rot = (Parameter<Quaternion>)selObj.parameterList[rIndex];
                    rot.setValue(rotQuat * rot.value);
                }
                // For multiple objects
                else
                {
                    for (int i = 0; i < selObjs.Count; i++)
                    {
                        // Effect on position
                        Vector3 srcPos = selObjs[i].transform.position;
                        Vector3 pivotPoint = manipR.transform.position;
                        Vector3 dstPos = rotQuat * (srcPos - pivotPoint) + pivotPoint;
                        Vector3 localVec = selObjs[i].transform.parent.InverseTransformPoint(dstPos);
                        Parameter<Vector3> pos = (Parameter<Vector3>)selObjs[i].parameterList[tIndex];
                        pos.setValue(localVec);

                        // Rotation
                        Parameter<Quaternion> rot = (Parameter<Quaternion>)selObjs[i].parameterList[rIndex];
                        rot.setValue(rotQuat * rot.value);
                    }

                    // Make gizmo follow
                    visualRot *= rotQuat;
                    TransformManipR(visualRot);
                }

                // update offset
                hitPosOffset = hitPoint - localManipPosition;
            }

            // drag object - scale
            else if (modeTRS == 2){
                
                Vector3 projectedVec = hitPoint;
                // temp hack - identify if single axis
                if (manipulator == manipSx || manipulator == manipSy || manipulator == manipSz){
                    projectedVec = Vector3.Project(hitPoint - manipS.transform.position, manipulator.transform.up) + manipS.transform.position;
                }

                Parameter<Vector3> sca;

                //actual scale things - tracer assets
                Vector3 deltaClick = projectedVec - hitPosOffset + manipS.transform.position;
                Vector3 localDelta = manipS.transform.InverseTransformPoint(deltaClick);

                // hack to see if it's main controller and so would use uniform scale - average values
                if (manipulator == manipS)
                    localDelta = Vector3.one * (localDelta.x + localDelta.y + localDelta.z) / 3f;

                Vector3 scaleOffset = Vector3.one + localDelta;
                sca = (Parameter<Vector3>)selObj.parameterList[sIndex];
                sca.setValue(Vector3.Scale(initialSca, scaleOffset));
            }
        }

        //!
        //! Function to finalize manipulator operation
        //! Called with the end (cancellation) of click from InputManager
        //! @param sender m_callback sender
        //! @param e event reference
        //!
        private void FinalizeManipulatorTransformation(Vector2 point){
            Debug.Log("FinalizeManipulatorTransformation");
            // Hack - restore scale
            // restore position instead
            if (modeTRS == 2){
                manipSx.transform.localPosition = Vector3.zero;
                manipSy.transform.localPosition = Vector3.zero;
                manipSz.transform.localPosition = Vector3.zero;
                manipSxy.transform.localPosition = Vector3.zero;
                manipSxz.transform.localPosition = Vector3.zero;
                manipSyz.transform.localPosition = Vector3.zero;
            }

            // for multi selection
            objOffsets.Clear();
            if (selObjs.Count > 1){
                // restore rotation gizmo orientation
                visualRot = Quaternion.identity;
                TransformManipR(visualRot);
            }

            // if(selObj)
            //     Debug.Log("selObj: "+selObj.gameObject.name);
            // if(manipulator)
            //     Debug.Log("manipulator: "+manipulator.gameObject.name);
            // Debug.Log("modeTRS: "+modeTRS);

            if(selObj && manipulator){
                switch (modeTRS)
                {
                    case 0:
                        manager.Manipulation3dDone(selObj.parameterList[0]);
                        if(selObj && selObj.GetType() != typeof(SceneObjectMeasurement))    //SceneObjectMeasurement is only locally, so we dont execute these network events
                            doneEditing?.Invoke(this, selObj.parameterList[0]);
                        break;
                    case 1:
                        manager.Manipulation3dDone(selObj.parameterList[1]);
                        if(selObj && selObj.GetType() != typeof(SceneObjectMeasurement))    //SceneObjectMeasurement is only locally, so we dont execute these network events
                            doneEditing?.Invoke(this, selObj.parameterList[1]);
                        break;
                    case 2:
                        manager.Manipulation3dDone(selObj.parameterList[2]);
                        if(selObj && selObj.GetType() != typeof(SceneObjectMeasurement))    //SceneObjectMeasurement is only locally, so we dont execute these network events
                            doneEditing?.Invoke(this, selObj.parameterList[2]);
                        break;
                    default:
                        break;
                }
            }
            m_inputManager.SetAllowCamNavigation(true);
            manipulator = null;
            _dragViz.Cleanup();
            _dragRotateViz.Cleanup();
        }

        private void HideGizmo() {
            
        }

        private void ShowGizmo() {
            
        }

        //!
        //! Function that does nothing.
        //! Being called when selection has changed.
        //!
        private void SelectionUpdate(object sender, List<SceneObject> sceneObjects){

            // Log
            //Debug.Log("<i>UICreator3DModule.SelectionUpdate()</i> "+sceneObjects.Count);

            if (sceneObjects.Count > 0){
                // Grab object
                selObj = sceneObjects[0];
                // by reference
                selObjs = sceneObjects;
                // by clone
                //selObjs = new List<SceneObject>(sceneObjects);

                //Debug.Log(selObj);
                GrabParameterIndex();

                // Unsubscribe from parameter updates
                if (posParam != null)
                    posParam.hasChanged -= UpdateManipulatorPosition;
                if (rotParam != null)
                    rotParam.hasChanged -= UpdateManipulatorRotation;
                if (scaParam != null)
                    scaParam.hasChanged -= UpdateManipulatorScale;

                // Reset parameters
                posParam = null;
                rotParam = null;
                scaParam = null;

                posParam = (Parameter<Vector3>)selObj.parameterList[tIndex];
                rotParam = (Parameter<Quaternion>)selObj.parameterList[rIndex];
                scaParam = (Parameter<Vector3>)selObj.parameterList[sIndex];
                // Subscribe to change
                posParam.hasChanged += UpdateManipulatorPosition;
                rotParam.hasChanged += UpdateManipulatorRotation;
                scaParam.hasChanged += UpdateManipulatorScale;

                // Start with translation
                // todo: confirm this design choice
                if (modeTRS == -1)
                    SetManipulatorMode(null, 0);

                // development for multi
                if (sceneObjects.Count > 1)
                    SetMultiManipulatorMode(null, 0);

                // Subscribe to possible change selection via camera (lock look through or in camera space)
                manager.uiCameraLockChanged += SetCameraManipulator;

                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(Camera.main, selObj.transform.position);
                core.StartCoroutine(AnimateFloatingText(""+manager.ManipulationLayer, screenPoint));

            }else{ // empty selection
                // Clean selection
                selObj = null;
                selObjs.Clear();

                //HideAxes();
                //modeTRS = -1;
                SetManipulatorMode(null, -1);

                manager.uiCameraLockChanged -= SetCameraManipulator;
            }

            camMathValues = Screen.dpi / (Screen.width + Screen.height);
        }
        private void ManipulationLayerChanged(object sender, UIManager.ManipulationLayerEnum newManipulationLayer) {
            if(lastActiveManip){
                switch ((TRSModeEnum)savedTrsMode) {
                    case TRSModeEnum.SCALE:     newManipulationLayer = UIManager.ManipulationLayerEnum.LOCAL; break;
                    case TRSModeEnum.TRANSLATE:
                    case TRSModeEnum.ROTATE:
                    default:
                        break;
                }
            }
            if(modeTRS > -1 && selObj){
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(Camera.main, selObj.transform.position);
                core.StartCoroutine(AnimateFloatingText(""+manager.ManipulationLayer, screenPoint));
            }
            
            if(lastActiveManip && selObj)
                UpdateGizmoAxisLayer(lastActiveManip.transform, selObj.transform, Camera.main.transform, newManipulationLayer);
        }


        void GrabParameterIndex()
        {
            //Debug.Log("INDEXES");
            for (int i = 0; i < selObj.parameterList.Count; i++)
            {
                if (selObj.parameterList[i].name.Equals("rotation"))
                    rIndex = i;
                if (selObj.parameterList[i].name.Equals("position"))
                    tIndex = i;
                if (selObj.parameterList[i].name.Equals("scale"))
                    sIndex = i;
            }
        }

        //!
        //! Initial call for gizmo prefabs instantiation
        //!
        private void InstantiateAxes()
        {
            // Tranlation
            GameObject resourcePrefab = Resources.Load<GameObject>("Prefabs/gizmoTranslate");
            manipT = GameObject.Instantiate(resourcePrefab);
            // Rotation
            resourcePrefab = Resources.Load<GameObject>("Prefabs/gizmoRotate");
            manipR = GameObject.Instantiate(resourcePrefab);
            // Scale
            resourcePrefab = Resources.Load<GameObject>("Prefabs/gizmoScale");
            manipS = GameObject.Instantiate(resourcePrefab);

            // Grab its parts
            manipTx = manipT.transform.GetChild(0).GetChild(0).gameObject;
            manipTy = manipT.transform.GetChild(0).GetChild(1).gameObject;
            manipTz = manipT.transform.GetChild(0).GetChild(2).gameObject;

            manipSx = manipS.transform.GetChild(0).GetChild(0).gameObject;
            manipSy = manipS.transform.GetChild(0).GetChild(1).gameObject;
            manipSz = manipS.transform.GetChild(0).GetChild(2).gameObject;
            manipSxy = manipS.transform.GetChild(0).GetChild(3).gameObject;
            manipSxz = manipS.transform.GetChild(0).GetChild(4).gameObject;
            manipSyz = manipS.transform.GetChild(0).GetChild(5).gameObject;
        }

        //!
        //! Hide specific manipulator
        //!
        private void HideAxis(GameObject manip)
        {
            manip.SetActive(false);
        }

        //!
        //! Hide all manipulators
        //!
        private void HideAxes()
        {
            if (manipT) HideAxis(manipT);
            if (manipR) HideAxis(manipR);
            if (manipS) HideAxis(manipS);
            modeTRS = -1;
        }

        //!
        //! Unhide last active manipulator
        //!
        private void UnhideAxis()
        {
            lastActiveManip.SetActive(true);
            //also update scale!
            lastActiveManip.transform.localScale = GetModifierScale();
            modeTRS = savedTrsMode;
        }

        //!
        //! Show specific manipulator
        //!
        private void ShowAxis(GameObject manip)
        {
            manip.SetActive(true);
            lastActiveManip = manip;
        }

        //!
        //! Update the transform gizmo based on one or more selected objects
        //!
        private void TransformAxisMulti(GameObject manip)
        {
            // average position
            Vector3 averagePos = Vector3.zero;
            foreach (SceneObject obj in selObjs)
            {
                averagePos += obj.transform.position;
            }
            averagePos /= selObjs.Count;
            
            manip.transform.SetPositionAndRotation(averagePos, selObj.transform.rotation);
            if (selObjs.Count > 1)
                manip.transform.rotation = Quaternion.identity;


            // Adjust scale
            manip.transform.localScale = GetModifierScale();
        }

        public void UpdateGizmoAxisLayer(Transform gizmoRoot, Transform targetObject, Transform cameraTransform, UIManager.ManipulationLayerEnum mode) {
            // Orient the Gizmo arrows based on the current mode
            switch (mode) {
                case UIManager.ManipulationLayerEnum.LOCAL:
                    // The arrows perfectly match the object's internal tilt and spin
                    gizmoRoot.rotation = targetObject.rotation;
                    break;

                case UIManager.ManipulationLayerEnum.GLOBAL:
                    // The arrows lock to the absolute World grid (Identity = No rotation)
                    gizmoRoot.rotation = Quaternion.identity;
                    break;

                case UIManager.ManipulationLayerEnum.VIEWPORT:
                    // The arrows point away from the camera, but stay flat on the floor
                    Vector3 flatCamForward = cameraTransform.forward;
                    flatCamForward.y = 0f; // no vertical pitch

                    // Safety check: Prevent errors if the camera looks EXACTLY straight down
                    if (flatCamForward.sqrMagnitude > 0.0001f) {
                        flatCamForward.Normalize();
                        
                        // LookRotation mathematically builds a Quaternion where "Forward" is our flatCamForward, 
                        // and "Up" is the world's absolute Up.
                        gizmoRoot.rotation = Quaternion.LookRotation(flatCamForward, Vector3.up);
                    }else{
                        // Fallback if the camera is pointing straight at the floor: 
                        // Use the camera's UP vector (flattened) as the new forward
                        Vector3 flatCamUp = cameraTransform.up;
                        flatCamUp.y = 0f;
                        gizmoRoot.rotation = Quaternion.LookRotation(flatCamUp.normalized, Vector3.up);
                    }

                    if((TRSModeEnum)savedTrsMode == TRSModeEnum.ROTATE){
                        // Multiplies the current rotation by a local 90-degree spin on the Y-axis, swapping X and Z visually.
                        gizmoRoot.rotation *= Quaternion.Euler(0f, 90f, 0f);
                    }
                    break;
            }
        }

        //!
        //! Update the transform gizmo scale
        //!
        private void UpdateManipScale()
        {
            if (lastActiveManip)
                lastActiveManip.transform.localScale = GetModifierScale();
        }

        //!
        //! Update the rotate transform gizmo 
        //!
        private void TransformManipR(Quaternion rot)
        {
            manipR.transform.rotation = rot;
        }

        //!
        //! Update the rotate transform gizmo 
        //!
        private void SetMultiManipulatorMode(object sender, int manipulatorMode)
        {
            // Start in translate mode
            HideAxes();
            ShowAxis(manipT);
            // transform axis for both
            Vector3 averagePos = Vector3.zero;
            foreach (SceneObject obj in selObjs)
            {
                averagePos += obj.transform.position;
            }
            averagePos /= selObjs.Count;
            manipT.transform.position = averagePos;
            // neutral rotation for global mode
            manipT.transform.rotation = Quaternion.identity;
            manipT.transform.localScale = GetModifierScale();
            modeTRS = 0;
            savedTrsMode = modeTRS;
            // Incomplete function - lacking manipulator mode
        }

        //!
        //! Set the mode of operation of the manipulator and its respective event subscriptions
        //!
        private void SetManipulatorMode(object sender, int manipulatorMode){
            // Disable manipulator
            if (manipulatorMode < 0 || manipulatorMode > 2){
                HideAxes();
                modeTRS = -1;
                // Place manipulator out of range to avoid unwanted click recognition when it's activated
                // [REVIEW]
                // float max might not be the best choice for hiding an object
                // [THOMAS]
                // of fking course its not. so many errors "Invalid worldAABB. Object is too large or too far away from the origin."
                // it should just remain hidden via the above!
                // if (manipT)
                //     manipT.transform.position = float.MaxValue * Vector3.one;
                return;
            }

            if (selObj){
                SetModeTRS((TRSModeEnum)manipulatorMode);
            }

        }

        //!
        //! Update the manipulator position according to position parameter changes
        //!
        public void UpdateManipulatorPosition(object sender, Vector3 position)
        {
            manipT.transform.localScale = GetModifierScale();
            // for one or more selected objects 
            Vector3 averagePos = Vector3.zero;
            foreach (SceneObject obj in selObjs)
            {
                averagePos += obj.transform.position;
            }
            if(selObjs.Count>0)
                averagePos /= selObjs.Count;
            
            manipT.transform.position = averagePos;
            manipR.transform.position = averagePos;
            manipS.transform.position = averagePos;
        }

        //!
        //! Update the manipulator rotation according to rotation parameter changes
        //!
        public void UpdateManipulatorRotation(object sender, Quaternion rotation){
            // only update here if single selection
            if (selObjs.Count == 1) {
                //Debug.Log("UpdateManipulatorRotation");
                //this will happen ongoing while we hold to rotate
                //and it is completely wrong for other ManipulationLayers like global or viewport!
                Quaternion q = selObj.transform.rotation;
                manipT.transform.localRotation = q;
                manipR.transform.localRotation = q;
                manipS.transform.localRotation = q;

                switch ((TRSModeEnum)savedTrsMode) {
                    case TRSModeEnum.ROTATE:
                        //global/viewport: does not rotate the gizmo...
                        UpdateGizmoAxisLayer(manipR.transform, selObj.transform, Camera.main.transform, manager.ManipulationLayer);
                        break;
                    case TRSModeEnum.SCALE:
                    case TRSModeEnum.TRANSLATE:
                    default:
                        break;
                }     
            }
        }

        //!
        //! Update the manipulator scale according to scale parameter changes
        //!
        public void UpdateManipulatorScale(object sender, Vector3 scale)
        {
            Vector3 vecOfsset = Vector3.Scale(scale, VecInvert(initialSca));
            Vector3 localDelta = vecOfsset - Vector3.one;

            // Grab "dimension" of delta
            float UniX = NonZero(localDelta.x);
            float UniY = NonZero(localDelta.y);
            float UniZ = NonZero(localDelta.z);

            // Main axes
            manipSx.transform.localPosition = Vector3.Scale(localDelta, Vector3.right) * 1.64f;
            manipSy.transform.localPosition = Vector3.Scale(localDelta, Vector3.up) * 1.64f;
            manipSz.transform.localPosition = Vector3.Scale(localDelta, Vector3.forward) * 1.64f;
            // Multi axes
            manipSxy.transform.localPosition = UniX * UniY * Vector3.Scale(localDelta, vecXY);
            manipSxz.transform.localPosition = UniX * UniZ * Vector3.Scale(localDelta, vecXZ);
            manipSyz.transform.localPosition = UniY * UniZ * Vector3.Scale(localDelta, vecYZ);
        }

        //!
        //! Function coupled with UI camera operation to hide/unhide the manipulator
        //!
        private void SetCameraManipulator(object sender, bool hide){
            if (hide)
                HideAxes();
            else
                UnhideAxis();
        }

        //!
        //! Helper function for non-zero evaluation
        //!
        private float NonZero(float number)
        {
            // Following short-version is not working
            // return Mathf.Approximately(number, 0.0f) ? 0.0f : 1.0f;
            // Tolerance needs to be higher than Mathf.Epsilon 
            if (number >= -1E-06 && number <= 1E-06)
            {
                return 0f;
            }
            return 1f;
        }

        //!
        //! Helper function for vector inversion by component
        //!
        private Vector3 VecInvert(Vector3 vec)
        {
            return new Vector3(1 / vec.x, 1 / vec.y, 1 / vec.z);
        }

        //!
        //! Helper function for transform gizmo scale adjustment according to screen and UI scale parameter
        //!
        private Vector3 GetModifierScale(){
            if (!selObj)
                return Vector3.one;
         
            return Vector3.one * uiScale 
                       * (Vector3.Distance(mainCamera.transform.position, selObj.transform.position)
                       * (4.0f * Mathf.Tan(0.5f * (Mathf.Deg2Rad * mainCamera.fieldOfView)))
                       * camMathValues);

        }

        public enum TRSModeEnum {
            TRANSLATE = 0, ROTATE = 1, SCALE = 2, NONE
        }

        public void SetModeTRS(TRSModeEnum mode) {
            if (selObj != null && mode != TRSModeEnum.NONE){
                GameObject manipulator = null;
                UIManager.ManipulationLayerEnum manipLayer = manager.ManipulationLayer;;
                switch (mode) {
                    case TRSModeEnum.TRANSLATE: manipulator = manipT; break;
                    case TRSModeEnum.ROTATE:    manipulator = manipR; break;
                    case TRSModeEnum.SCALE:     manipulator = manipS; manipLayer = UIManager.ManipulationLayerEnum.LOCAL; break;
                }
                HideAxes();
                ShowAxis(manipulator);
                TransformAxisMulti(manipulator);
                
                UpdateGizmoAxisLayer(manipulator.transform, selObj.transform, Camera.main.transform, manipLayer);

                modeTRS = (int)mode;
                savedTrsMode = modeTRS;
            }
        }

        //!
        //! Function coupled to user UI scale changes to update the gizmo scale
        //!
        private void updateUIScale(object sender, float e)
        {
            uiScale = e;
            UpdateManipScale();
        }

        //!
        //! Function coupled to camera operations to update the gizmo scale
        //!
        private void updateGizmoScale(object sender, bool e)
        {
            UpdateManipScale();
        }

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
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.color = Color.white;
            txt.rectTransform.position = startPos;
            
            // Add shadow for readability
            Outline outline = textGO.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1, -1);

            float duration = 2f;
            float elapsed = 0f;

            while (elapsed < duration){
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Float up and fade out
                txt.rectTransform.position = startPos + new Vector2(0, t * 20f);
                txt.color = new Color(1f, 1f, 1f, 1f - Mathf.Pow(t, 2f));
                outline.effectColor = new Color(0, 0, 0, 1f - Mathf.Pow(t, 2f));
                
                yield return null;
            }
            UnityEngine.GameObject.Destroy(textGO);
        }
        #endregion   
    }
}