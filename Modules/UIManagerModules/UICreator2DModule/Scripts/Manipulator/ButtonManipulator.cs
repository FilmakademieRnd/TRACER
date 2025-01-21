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
using UnityEngine;

namespace tracer{
    public class ButtonManipulator : Manipulator{
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
                            //show button as element to delete the path, recreate path, add path (and maybe distribute?)
                            //do we have the possibility to have buttons there?
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_deletePath"), 0);
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_recreatePath"), 0);
                            //_snapSelect.addElement(Resources.Load<Sprite>("Images/button_addPath"), 0);
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_distributePath"), 0);
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
        }

        private void executeElement(object sender, int value)
        {
            Debug.Log("execute value "+value+" on button manipulator "+_currentManipulation+" to execute");
            //setValue(null, true);
            switch(_currentManipulation){
                case 0: 
                    byte recentSceneId = abstractParam._parent.gameObject.GetComponent<SceneObject>()._sceneID;
                    abstractParam._parent.gameObject.GetComponent<SceneObject>().fireAndForgetSceneId(recentSceneId, 1);
                    SetPathTarget();
                    break;
                case 1:
                case 2:
                    //send 
                    //Parameter<int> position = new Parameter<int>(transform.localPosition, "position", this);
                    //ParameterObject nonDynamicPo = ParameterObject.Attach(new GameObject("DynamicPO only for sending"), 254);
                    //ParameterObject nonDynamicPo = abstractParam._parent;
                    //short idWas = nonDynamicPo.FireAndForget(0);
                    //Debug.Log("new parameterobject id is "+nonDynamicPo._id);

                    //RPCParameter<int> rpcPara = new Parameter<int>(1, "sendToWaitForPath", abstractParam._parent);
                    //rpcPara.setValue(3);    //en bloc animation request
                    //rpcPara.value = 3;
                    //nonDynamicPo.FireAndForget(idWas);
                    RPCParameter<int> para = abstractParam._parent.gameObject.GetComponent<SceneObject>().animHostGen;
                    byte sceneIdWas = para._parent.gameObject.GetComponent<SceneObject>()._sceneID;
                    short objectIdWas = abstractParam._parent.gameObject.GetComponent<SceneObject>()._id;
                    para._parent.gameObject.GetComponent<SceneObject>().fireAndForgetSceneId(255, 1);
                    short idWas = para._id;
                    para.FireAndForget(0);
                    //trigger
                    para.value = 3;
                    //reset values

                    //vvv DOES NOT WORK vvv
                    //Test sending dynamic created rpc
                    // RPCParameter<int> dynamicRPC4Path = new RPCParameter<int>(0, "dynamicRPC4Path", abstractParam._parent);
                    // idWas = dynamicRPC4Path._id;
                    // dynamicRPC4Path.FireAndForget(0);
                    // //trigger
                    // dynamicRPC4Path.value = 5;
                    // //reset values
                    break;
            }
            
        }

        #region QUICK&DIRTY: CREATE PATH
        private void SetPathTarget(){
            _manager.core.getManager<InputManager>().inputPressEnd -= OnPointerEnd;
            _manager.core.getManager<InputManager>().inputPressEnd += OnPointerEnd;
        }
        void OnDestroy(){
            _manager.core.getManager<InputManager>().inputPressEnd -= OnPointerEnd;
        }
        private void OnPointerEnd(object sender, Vector2 point){
            MeshRenderer[] ignoreTheseForRaycasting = null;
            //ignore (selection) itself
            //ignoreTheseForRaycasting = sceneObjectToPlace.GetComponents<MeshRenderer>();

            //TODO for performance we would not need to gather all MeshRenderer on every click (only if we modify any)
            if (SceneRaycastHelper.RaycastIntoScene(_manager.core.getManager<SceneManager>().scnRoot, point, out RaycastHit hit, ignoreTheseForRaycasting)){
                //TARGET POS
                //abstractParam._parent.gameObject.transform.position = hit.point;
                CreatePath(abstractParam._parent.gameObject.transform.position, hit.point);
                //_manager.core.StartCoroutine(CreatePath(abstractParam._parent.gameObject.transform.position, hit.point));
                Debug.Log("<color=green>Path End: "+hit.point+"</color>");
                //align to hit normal with upwards vector
                //abstractParam._parent.gameObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            }
            _manager.core.getManager<InputManager>().inputPressEnd -= OnPointerEnd;
            _manager.clearSelectedObject();
            _manager.addSelectedObject(abstractParam._parent.gameObject.GetComponent<SceneObject>());
        }

        private IAnimationParameter m_activeParameter = null;

        private void CreatePath(Vector3 start, Vector3 end){
            //use start y
            end.y = start.y;
            //create animated parameter list
            //-- v3 position
            m_activeParameter =  _manager.SelectedObjects[0].parameterList[3] as IAnimationParameter;   //ignore TRS (0) -> use 3
            AbstractKey[] keyList = new AbstractKey[2];
            Key<Vector3> kv3 = new Key<Vector3>(0f, start);
            keyList[0] = kv3;
            // m_activeParameter.createKey(kv3);
            // yield return null;  
            //otherwise we'll get a null error (out of range) on UpdateSenderModule.405: Span<byte> newSpan = msgSpan.Slice(start, length);
            //if we create multiple keys at once and trigger an InvokeHasChanged multiple times per frame! 
            
            kv3 = new Key<Vector3>(30f, end);
            keyList[1] = kv3;
            // m_activeParameter.createKey(kv3);
            // yield return null;
            m_activeParameter.createKeyList(keyList);
            
            //-- q rotation
            m_activeParameter =  _manager.SelectedObjects[0].parameterList[4] as IAnimationParameter;   //ignore TRS (1) -> use 4
            Key<Quaternion> qr = new Key<Quaternion>(0f, abstractParam._parent.gameObject.transform.rotation);
            keyList[0] = qr;
            // m_activeParameter.createKey(qr);
            // yield return null;
            
            qr = new Key<Quaternion>(30f, Quaternion.Euler(5,5,5));
            keyList[1] = qr;
            //m_activeParameter.createKey(qr);
            m_activeParameter.createKeyList(keyList);
            
            //yield return null;
        }


        #endregion
    }
}