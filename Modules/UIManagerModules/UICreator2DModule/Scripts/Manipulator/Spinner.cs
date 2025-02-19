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

//! @file "Spinner.cs"
//! @brief base class of spinner manipulators
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @author Justus Henne
//! @version 0
//! @date 02.02.2022

using UnityEngine;

namespace tracer
{
    public class Spinner : Manipulator
    {
        //!
        //! currently edited axis of the parameter (e.g. x, y or z)
        //!
        private int _currentAxis;

        //!
        //! Reference to _snapSelect
        //!
        private SnapSelect _snapSelect;

        //!
        //! Reference to TRACER uiSettings
        //!
        private UIManager _manager;


        ~Spinner()
        {
            if (abstractParam != null)
                _snapSelect.editingEnded -= InvokeDoneEditing;
        }

        //!
        //! function to initalize the spinner
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
                    //Parameter<bool> paramBool = (Parameter<bool>)abstractParam;
                    // paramBool.hasChanged += _snapSelect.setParam;
                    // _snapSelect.setSensitivity(100f);
                    // _snapSelect._loop = false;
                    // switch (paramBool.name) {
                    //     case "createPath":
                    //         //show button as element to delete the path, recreate path, add path (and maybe distribute?)
                    //         //do we have the possibility to have buttons there?
                    //     break;
                    // }   
                    break;
                case AbstractParameter.ParameterType.FLOAT:
                    Parameter<float> paramFloat = (Parameter<float>)abstractParam;
                    paramFloat.hasChanged += _snapSelect.setParam;
                    _snapSelect.setSensitivity(100f);
                    _snapSelect._loop = false;                    
                    switch (paramFloat.name) {
                        case "intensity":
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_intensity"), paramFloat.value);
                            break;
                        case "range":
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_range"), paramFloat.value);
                            break;
                        case "aperture":
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_aperture"), paramFloat.value);
                            break;
                        case "aspectRatio":
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_aspect"), paramFloat.value);
                            break;
                        case "radius":
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_radius"), paramFloat.value);
                            break;
                        case "fov":
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_fov"), paramFloat.value);
                            break;
                        case "farClipPlane":
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_farClipPlane_text"), paramFloat.value);
                            break;
                        case "nearClipPlane":
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_nearClipPlane_text"), paramFloat.value);
                            break;
                        case "focalDistance":
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_focalDistance"), paramFloat.value);
                            break;
                        default:                            
                            _snapSelect.addElement("", paramFloat.value);
                            _snapSelect._loop = true;
                            break;
                    }                    
                break;
                case AbstractParameter.ParameterType.VECTOR2:
                    Parameter<Vector2> paramVec2 = (Parameter<Vector2>)abstractParam;
                    paramVec2.hasChanged += _snapSelect.setParam;
                    _snapSelect.setSensitivity(10f);
                    _snapSelect.addElement(Resources.Load<Sprite>("Images/button_x"), paramVec2.value.x);
                    _snapSelect.addElement(Resources.Load<Sprite>("Images/button_y"), paramVec2.value.y);                    
                    break;
                case AbstractParameter.ParameterType.VECTOR3:
                    Parameter<Vector3> paramVec3 = (Parameter<Vector3>)abstractParam;
                    switch (paramVec3.name) {
                        case "pathPositions":
                            //DONT SHOW SPINNER, SINCE WE WANT TO MANIPULATE AN ARBIRTRARY PATH OBJECT (SceneObjectUI)
                            break;
                        default:
                            paramVec3.hasChanged += _snapSelect.setParam;
                            _snapSelect.setSensitivity(10f);
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_x"), paramVec3.value.x);
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_y"), paramVec3.value.y);
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_z"), paramVec3.value.z);                    
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_xyz"), (paramVec3.value.x + paramVec3.value.y + paramVec3.value.z) / 3f);
                            break;
                    }
                    break;
                case AbstractParameter.ParameterType.QUATERNION:
                    Parameter<Quaternion> paramQuat = (Parameter<Quaternion>)abstractParam;
                    switch (paramQuat.name) {
                        case "pathRotations":
                            //DONT SHOW SPINNER, SINCE WE WANT TO MANIPULATE AN ARBIRTRARY PATH OBJECT (SceneObjectUI)
                            break;
                        default:
                            paramQuat.hasChanged += _snapSelect.setParam;
                            Vector3 rot = paramQuat.value.eulerAngles;
                            _snapSelect.setSensitivity(500f);
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_x"), rot.x);
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_y"), rot.y);
                            _snapSelect.addElement(Resources.Load<Sprite>("Images/button_z"), rot.z);
                            break;
                    }
                    break;
                default:
                    Helpers.Log("Parameter Type cannot be edited with Spinner.");
                    break;
            }
        }

        //!
        //! function connecting the events
        //!
        private void Start()
        {
            _snapSelect.parameterChanged += changeAxis;
            _snapSelect.valueChanged += setValue;
            _snapSelect.editingEnded += InvokeDoneEditing;
        }

        //!
        //! function to perform axis drag
        //! @param axis axis to be used
        //! @param value drag value
        //!
        private void setValue(object sender, float val)
        {
            AbstractParameter.ParameterType type = abstractParam.tracerType;
            switch (type)
            {
                case AbstractParameter.ParameterType.FLOAT:
                    Parameter<float> paramFloat = (Parameter<float>)abstractParam;
                    paramFloat.setValue(paramFloat.value + val);
                    break;
                case AbstractParameter.ParameterType.VECTOR2:
                    Parameter<Vector2> paramVec2 = (Parameter<Vector2>)abstractParam;
                    Vector2 valVec2 = paramVec2.value;
                    if (_currentAxis == 0)
                        paramVec2.setValue(new Vector2(valVec2.x + val, valVec2.y));
                    else
                        paramVec2.setValue(new Vector2(valVec2.x, valVec2.y + val));
                    break;
                case AbstractParameter.ParameterType.VECTOR3:
                    Parameter<Vector3> paramVec3 = (Parameter<Vector3>)abstractParam;
                    Vector3 valVec3 = paramVec3.value;
                    if (_currentAxis == 0)
                        paramVec3.setValue(new Vector3(valVec3.x + val, valVec3.y, valVec3.z));
                    else if (_currentAxis == 1)
                        paramVec3.setValue(new Vector3(valVec3.x, valVec3.y + val, valVec3.z));
                    else if (_currentAxis == 2)
                        paramVec3.setValue(new Vector3(valVec3.x, valVec3.y, valVec3.z + val));
                    else
                        paramVec3.setValue(new Vector3(valVec3.x + val, valVec3.y + val, valVec3.z + val));
                    break;
                case AbstractParameter.ParameterType.QUATERNION:
                    Parameter<Quaternion> paramQuat = (Parameter<Quaternion>)abstractParam;
                    Quaternion rot = Quaternion.identity;
                    if (_currentAxis == 0)
                        rot = Quaternion.Euler(val, 0, 0);
                    else if (_currentAxis == 1)
                        rot = Quaternion.Euler(0, val, 0);
                    else
                        rot = Quaternion.Euler(0, 0, val); ;
                    paramQuat.setValue(paramQuat.value * rot);
                    break;
                default:
                    Helpers.Log("Parameter Type cannot be edited with Spinner.");
                    break;
            }
        }

        //!
        //! function changing the axis
        //! @param sender source of the event
        //! @param new axis index
        //!
        private void changeAxis(object sender, int index)
        {
            _currentAxis = index;
        }
    }
}