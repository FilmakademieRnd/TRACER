/*
VPET - Virtual Production Editing Tools
tracer.research.animationsinstitut.de
https://github.com/FilmakademieRnd/VPET

Copyright (c) 2023 Filmakademie Baden-Wuerttemberg, Animationsinstitut R&D Lab

This project has been initiated in the scope of the EU funded project
Dreamspace (http://dreamspaceproject.eu/) under grant agreement no 610005 2014-2016.

Post Dreamspace the project has been further developed on behalf of the
research and development activities of Animationsinstitut.

In 2018 some features (Character Animation Interface and USD support) were
addressed in the scope of the EU funded project SAUCE (https://www.sauceproject.eu/)
under grant agreement no 780470, 2018-2022

This program is free software; you can redistribute it and/or modify it under
the terms of the MIT License as published by the Open Source Initiative.

This program is distributed in the hope that it will be useful, but WITHOUT
ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
FOR A PARTICULAR PURPOSE. See the MIT License for more details.

You should have received a copy of the MIT License along with
this program; if not go to
https://opensource.org/licenses/MIT
*/

//! @file "ButtonManipulator.cs"
//! @brief button manipulator used for path creation ui button
//! @author Thomas Krpger
//! @version 0
//! @date 21.01.2025

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace tracer{
    public class ButtonManipulator : Manipulator{

        public const string     LOCATION_INPUT_BLOCKING_CANVAS_PREFAB       = "Prefabs/InputBlocking_Canvas";
        public const string     LOCATION_PATH_LINERENDERER_PREFAB           = "Prefabs/PathLineRenderer";

        //!
        //! current mode: delete path, create new path, add path
        //!
        private int _currentManipulation;

        //!
        //! Reference to _snapSelect
        //!
        private SnapSelect _snapSelect;

        //!
        //! Reference to TRACER uiSettings
        //!
        private UIManager _manager;

        //!
        //! object that should prevent selecting new objects during other phases (see MeasureModule)
        //!

        private GameObject inputBlockerInCanvas;


        ~ButtonManipulator()
        {
            if (abstractParam != null)
                _snapSelect.editingEnded -= InvokeDoneEditing;
        }

        //!
        //! function to initalize the button manipulator
        //!
        public override void Init(AbstractParameter p, UIManager m)
        {
            abstractParam = p;
            _manager = m;
            _snapSelect = this.GetComponent<SnapSelect>();
            _snapSelect.uiSettings = _manager.uiAppearanceSettings;
            _snapSelect.manager = _manager;

            AbstractParameter.ParameterType type = abstractParam.tracerType;

            Debug.Log("<color=red>INIT ButtonManipulator. Type "+type.ToString()+" and paramName: "+abstractParam.name+"</color>");

            switch (type)
            {
                case AbstractParameter.ParameterType.BOOL:
                    //add bool for path that can be deleted, also store the path in an additional list (pos-v3, rot-q)
                    Parameter<bool> paramBool = (Parameter<bool>)abstractParam;
                    // paramBool.hasChanged += _snapSelect.setParam;
                    // _snapSelect.setSensitivity(100f);
                    // _snapSelect._loop = false;
                    switch (paramBool.name) {
                        case "createPath":
                            inputBlockerInCanvas = GameObject.Instantiate(Resources.Load(LOCATION_INPUT_BLOCKING_CANVAS_PREFAB) as GameObject).transform.GetChild(0).gameObject;
                            inputBlockerInCanvas.SetActive(false);
                            //show button as element to delete the path, recreate path, add path (and maybe distribute?)
                            //do we have the possibility to have buttons there?
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_generatePath"), 0);
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_deletePath"), 0);
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_triggerAnimHostPathGen"), 0);
                        break;
                    }   
                    break;
                case AbstractParameter.ParameterType.FLOAT:
                case AbstractParameter.ParameterType.VECTOR2:
                case AbstractParameter.ParameterType.VECTOR3:
                case AbstractParameter.ParameterType.QUATERNION:
                default:
                    Helpers.Log("Parameter Type cannot be edited with ButtonManipulator.");
                    break;
            }
        }

        //!
        //! function connecting the events
        //!
        private void Start()
        {
            _snapSelect.parameterChanged += changeButtonToShow;
            //_snapSelect.valueChanged += setValue;
            //_snapSelect.editingEnded += InvokeDoneEditing;
            _snapSelect.elementClicked += executeElement;
            //_snapSelect.dragable = false;
        }

        //!
        //! function to perform axis drag
        //! @param axis axis to be used
        //! @param value drag value
        //!
        private void setValue(object sender, bool val){
            AbstractParameter.ParameterType type = abstractParam.tracerType;
            switch (type){
                case AbstractParameter.ParameterType.BOOL:
                    Parameter<bool> paramBool = (Parameter<bool>)abstractParam;
                    Debug.Log("just trigger this 'button' on "+paramBool._parent.gameObject.name);
                    paramBool.setValue(!paramBool.value);

                    //QUICK AND DIRTY: CREATE PATH HERE
                    break;
                case AbstractParameter.ParameterType.VECTOR2:
                case AbstractParameter.ParameterType.VECTOR3:
                case AbstractParameter.ParameterType.QUATERNION:
                default:
                    Helpers.Log("Parameter Type cannot be edited with ButtonManipulator.");
                    break;
            }
        }

        //!
        //! function changing the axis
        //! @param sender source of the event
        //! @param new axis index
        //!
        private void changeButtonToShow(object sender, int index)
        {
            _currentManipulation = index;
            Debug.Log("changed button manipulator to show "+index);
            //this could also be used as "to activate" element
        }

        private void executeElement(object sender, int value)
        {
            Debug.Log("execute value "+value+" on button manipulator "+_currentManipulation+" to execute");
            
            SceneCharacterObject sco = abstractParam._parent.gameObject.GetComponent<SceneCharacterObject>();
            if(!sco)
                return;

            switch(_currentManipulation){
                case 0: 
                    byte dontChangeID = sco._sceneID;
                    sco.OverrideTracerValues(dontChangeID, 1, 0);
                    SetListenerForPathTarget();
                    inputBlockerInCanvas.SetActive(true);
                    ShowAvailablePath(true, sco.pathPos);
                    break;
                case 1:
                    sco.pathPos.clearKeys();   //ignore TRS (0) -> use 3
                    sco.pathRot.clearKeys();   //ignore TRS (1) -> use 4
                    m_activeParameter.clearKeys();
                    ShowAvailablePath(false, null);
                    break;
                case 2:
                    //save
                    sco.SaveParametersBeforeOverwrite();

                    sco.SendOutAnimHostTrigger();
                    // //set
                    // sco.OverrideTracerValues(255, 1, 0);
                    // //trigger send
                    // sco.TriggerAnimHost();
                    
                    //reset values
                    //(too early?)
                    sco.ResetOverwrittenTracerValues();

                    //DESELECT, SO WE ARE NOT LOCKED
                    //_manager.clearSelectedObject();

                    break;
            }
            
        }

        private LineRenderer pathLine;
        private void ShowAvailablePath(bool show, Parameter<Vector3> paraPathPos){
            return;

            if(!show){
                if(pathLine){
                    Destroy(pathLine.gameObject);
                    pathLine = null;
                }
                return;
            }else{
                Vector3 upwardsVizOffset = Vector3.up * 0.2f;
                Vector3 startPos = abstractParam._parent.gameObject.GetComponent<SceneCharacterObject>().transform.position;
                List<Vector3> pathList = new();
                if(paraPathPos.getKeys() != null && paraPathPos.getKeys().Count > 0){
                    foreach(var ak in paraPathPos.getKeys()){
                        pathList.Add(
                            //only local pos right now
                            startPos + 
                            ((Key<Vector3>)ak).value + upwardsVizOffset
                        );
                    }
                }
                
                if(!pathLine){
                    pathLine = GameObject.Instantiate(Resources.Load(LOCATION_PATH_LINERENDERER_PREFAB) as GameObject).GetComponentInChildren<LineRenderer>();
                    //new GameObject("PathLineRendererObject").AddComponent<LineRenderer>();
                    //pathLine.widthMultiplier = 0.1f;
                    //Material defaultParticleMaterial = Resources.Load(LOCATION_INPUT_BLOCKING_CANVAS_PREFAB) as Material;
                    //pathLine.SetPositions(new Vector3[]{Vector3.zero, Vector3.zero});
                }
                pathLine.positionCount = pathList.Count;
                pathLine.SetPositions(pathList.ToArray());
                if(pathList.Count > 1)
                    StartCoroutine(VisualizePath());
            }
        }

        private IEnumerator VisualizePath(){
            float t = 0f;
            Gradient lineGradient = pathLine.colorGradient;
            GradientAlphaKey[] alphaKeys = lineGradient.alphaKeys;
            //IN
            while(pathLine && t<1f){
                t += Time.deltaTime*2f;
                for(int x = 0; x<alphaKeys.Length; x++){
                    alphaKeys[x].alpha = t;
                }
                lineGradient.alphaKeys = alphaKeys;
                pathLine.colorGradient = lineGradient;
                yield return null;
            }
            //OUT
            while(pathLine && t>0f){
                t -= Time.deltaTime/4f;
                for(int x = 0; x<alphaKeys.Length; x++){
                    alphaKeys[x].alpha = t;
                }
                lineGradient.alphaKeys = alphaKeys;
                pathLine.colorGradient = lineGradient;
                yield return null;
            }
        }


        #region QUICK&DIRTY: CREATE PATH
        private void SetListenerForPathTarget(){
            _manager.core.getManager<InputManager>().inputPressEnd -= OnPointerEnd;
            _manager.core.getManager<InputManager>().inputPressEnd += OnPointerEnd;
        }
        void OnDestroy(){
            _manager.core.getManager<InputManager>().inputPressEnd -= OnPointerEnd;
            if(pathLine){
                Destroy(pathLine.gameObject);
                pathLine = null;
            }
        }
        private void OnPointerEnd(object sender, Vector2 point){
            GameObject uiGo = TimelineModule.GetGameObjectAtUIRaycast(point);
            if(uiGo != inputBlockerInCanvas.gameObject){
                Debug.Log("OnPointerEnd hit something else than our input blocker");
                //hit something else then our input blocking canvas - so ignore and reset
                _manager.core.getManager<InputManager>().inputPressEnd -= OnPointerEnd;
                inputBlockerInCanvas.SetActive(false);
                return;
            }

            inputBlockerInCanvas.SetActive(false);
            Debug.Log("OnPointerEnd");

            MeshRenderer[] ignoreTheseForRaycasting = null;
            //ignore (selection) itself
            ignoreTheseForRaycasting = _manager.SelectedObjects[0].GetComponentsInChildren<MeshRenderer>();

            //TODO for performance we would not need to gather all MeshRenderer on every click (only if we modify any)
            if (SceneRaycastHelper.RaycastIntoScene(_manager.core.getManager<SceneManager>().scnRoot, point, out RaycastHit hit, ignoreTheseForRaycasting)){
                //TARGET POS
                CreatePath(abstractParam._parent.gameObject.transform.position, hit.point);
                Debug.Log("<color=green>Path End: "+hit.point+"</color>");
                //align to hit normal with upwards vector
                //abstractParam._parent.gameObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

                ShowAvailablePath(true, _manager.SelectedObjects[0].GetComponent<SceneCharacterObject>().pathPos);
            }

            _manager.core.getManager<InputManager>().inputPressEnd -= OnPointerEnd;
            inputBlockerInCanvas.SetActive(true);
            StartCoroutine(DisableInputBlockerAfter(1));
        }

        private IEnumerator DisableInputBlockerAfter(int yieldFrames){
            for(int x = 0; x<yieldFrames; x++)
                yield return new WaitForEndOfFrame();
            inputBlockerInCanvas.SetActive(false);
        }

        private IAnimationParameter m_activeParameter = null;

        private void CreatePath(Vector3 start, Vector3 end){
            //set to use same height, use start y
            end.y = start.y;
            //create animated parameter list
            
            //ALWAYS NEWS
            float endTime = 300;
            Vector3 centerPosForTangent = (start+end)/2f;

            //offset from local start
            end = (end-start);

            //local position, not world!
            start.x = 0f;
            start.y = 0f;
            start.z = 0f;
            
            end.y = start.y;

            end = Quaternion.Inverse(abstractParam._parent.gameObject.transform.rotation) * end;

            float tangentTime = endTime / 2f;   //mitte

            //-- v3 position
            m_activeParameter =  _manager.SelectedObjects[0].parameterList[3] as IAnimationParameter;   //ignore TRS (0) -> use 3
            m_activeParameter.clearKeys();
            AbstractKey[] keyList = new AbstractKey[]{
                new Key<Vector3>(0f, start, tangentTime, centerPosForTangent, tangentTime, centerPosForTangent),                  //start position
                new Key<Vector3>(endTime, end, tangentTime, centerPosForTangent, tangentTime, centerPosForTangent)      //end position
            };
            m_activeParameter.createKeyList(keyList);
            
            //-- q rotation
            m_activeParameter =  _manager.SelectedObjects[0].parameterList[4] as IAnimationParameter;   //ignore TRS (1) -> use 4
            m_activeParameter.clearKeys();
            //this should right now be a rotation, looking from startpos to endpos
            Quaternion endRotation = Quaternion.LookRotation((end-start).normalized);
            Object.FindObjectOfType<SceneCharacterObject>().calculatedButtonRot = endRotation;
            keyList = new AbstractKey[]{
                new Key<Quaternion>(0f, Quaternion.identity, tangentTime, abstractParam._parent.gameObject.transform.rotation, tangentTime, abstractParam._parent.gameObject.transform.rotation),   //start rotation
                new Key<Quaternion>(endTime, endRotation, tangentTime, endRotation, tangentTime, endRotation)                                           //end rotation
            };
            m_activeParameter.createKeyList(keyList);
        }

        private List<Vector3> CreatePathViaNavMesh(Vector3 start, Vector3 end){
            List<Vector3> pathData = new();
            pathData.Add(start);

            RaycastHit hit;
            if(!Physics.Raycast(start, (end-start).normalized, out hit, (end-start).magnitude)){
                pathData.Add(end);
                return pathData;
            }

            //CreateIterativePath(end, pathData, hit);
            //cast ray from start to end - without intersection - use line
            

            //if it has an intersection - try first right, second left and check if we can find another way
            return null;
        }

        //right now only on plane-ground
       /* private bool CreateIterativePath(Vector3 targetPos, List<Vector3> path, RaycastHit objectWeHit){
            float characterSize = 3f;
            if(Vector3.Distance(path[^1], targetPos) < characterSize){
                path.Add(targetPos);
                return true;
            }

            Vector3 directionToFace;
            if(path.Count < 2)
                directionToFace = (targetPos-path[^1]).normalized;
            else
                directionToFace = (path[path.Count-1]-path[path.Count-2]).normalized;

            RaycastHit hit;
            if(!Physics.Raycast(start, directionToFace, out hit, directionToFace.magnitude)){
                pathData.Add(end);
                return pathData;
            }


            RaycastHit hit;
            Vector3 newStart = objectWeHit.point-directionToFace * characterSize;
            if(!Physics.Raycast(newStart, directionToCheck, out hit, obstacleSize)){
                if()
            }else{
                //not a valid path
            }

            //go left or right, depending on the angle
            Vector3 directionToCheck;
            if(Vector3.Angle(-directionToFace, objectWeHit.normal) < 90){  //??
                //right
                directionToCheck = Vector3.Cross(directionToFace, Vector3.up);
            }else{
                //left
                directionToCheck = Vector3.Cross(directionToFace, Vector3.down);
            }

            float obstacleSize = objectWeHit.collider.bounds.size.x;
        }*/

        #endregion
    }
}