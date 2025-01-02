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

//! @file "MeasurePool.cs"
//! @brief Implementation of the TRACER MeasurePool component, updating ingame distance interface
//! @author Thomas "Kruegbert" Krüger
//! @version 0
//! @date 02.01.2025

using System;
using TMPro;
using UnityEngine;

namespace tracer
{
    //!
    //! Dynamic component to take care of 
    //!     TODO lines (ab)
    //!     TODO angles
    //!     TODO waypoints (a..z)
    //!     TODO areas (a,b,c, ..)
    //! just via ingame objects we can move, carry, rotate
    //!
    //! react to callbacks to 
    //!     TODO snap to point (ui button > ingame click > raycast > hit > move to point & align)
    //!     TODO add/remove points
    //!

    public class MeasurePool : MonoBehaviour{

        /************ HIERARCHY SETUP ************
         *
         *  MeasurePool - Parent Empty Object (child of "Scene")
         *      SceneObject - child with tag "editable"
         *      ... depending of measureType there are multiple childs
         *
         */

        public enum MeasureTypeEnum{
            line = 0,
            angle = 10,
            travel = 20
        }

        public enum TextPositioningEnum{
            fixedPos = 0,
            atLastElement = 10,
            atCenter = 20
        }


        public MeasureTypeEnum measureType = MeasureTypeEnum.line;
        public float distanceMultiplier = 1f;

        [Header("Visuals")]
        [Tooltip("dont change if you dont know what this is")]
        public string textFormat = "N2";
        [Tooltip("could be mm, cm, °, degree - we can adjust the distanceMultiplier accordingly")]
        public string textAffix = "m";
        [Tooltip("GameObject that has TextMeshPro component we will use to show distance, angle or area")]
        public GameObject textPrefab;
        public TextPositioningEnum textPositioning = TextPositioningEnum.atLastElement;
        //TODO: public bool adjustTextSize to CamDistance (like the Gizmos, so that we can read it from far away )
        [Tooltip("GameObject that has LineRenderer component we will utilize")]
        public GameObject linePrefab;

        [Header("Type - Line")][Tooltip("these values are only necessary of this measure type was choosen")]
        public Transform startObject;   //null point from where we measure
        public Transform endObject;     //end point, above we show the current distance
        [Header("Type - Angle")][Tooltip("these values are only necessary of this measure type was choosen")]
        public Transform angleObjectA;
        public Transform angleObjectB,angleObjectC;
        
        [Header("Type - Travel")][Tooltip("these values are only necessary of this measure type was choosen")]
        public Transform travelObject;


        private UIManager manager;
        private MeasureModule module;
        private TextMeshPro textObj;
        private LineRenderer line;
        private float currentDistance = 0;
        private float currentAngle;
        private Vector3 previousTraveledPos;        //used for traveled distance calculation
        private float lastDistanceForLinePoint;     //used to only add new points above a treshold
        
        void Start(){
            manager = GameObject.FindWithTag("Core").GetComponent<Core>().getManager<UIManager>();
            module = manager.getModule<MeasureModule>();

            line = Instantiate(linePrefab).GetComponent<LineRenderer>();
            if(!line){
                Debug.LogWarning("There is no LineRenderer attached to linePrefab: "+linePrefab.name);
                this.enabled = false;
                return;
            }
            line.gameObject.SetActive(false);

            textObj = Instantiate(textPrefab).GetComponent<TextMeshPro>();
            if(!textObj){
                Debug.LogWarning("There is no TextMeshPro attached to textPrefab: "+textPrefab.name);
                this.enabled = false;
                return;
            }
            textObj.gameObject.SetActive(false);

            module.measurementUIActiveEvent += OnMeasureUIChanged;
        }

        #region EVENT CALLBACKS

        //!
        //! Callback when the measure ui is toggled
        //!
        private void OnMeasureUIChanged(object sender, bool _isActive){
            line.gameObject.SetActive(_isActive);
            textObj.gameObject.SetActive(_isActive);

            switch(measureType){
                case MeasureTypeEnum.line:
                    line.positionCount = 2;
                    if(_isActive){
                        //subscribe to the sceneObject.hasChanged event, to not always calculate the line!
                        startObject.GetComponent<SceneObject>().hasChanged += SceneObjectHasChanged;
                        endObject.GetComponent<SceneObject>().hasChanged += SceneObjectHasChanged;
                    }else{
                        startObject.GetComponent<SceneObject>().hasChanged -= SceneObjectHasChanged;
                        endObject.GetComponent<SceneObject>().hasChanged -= SceneObjectHasChanged;
                    }
                    break;
                case MeasureTypeEnum.angle:
                    line.positionCount = 3;
                    if(_isActive){
                        //subscribe to the sceneObject.hasChanged event, to not always calculate the line!
                        angleObjectA.GetComponent<SceneObject>().hasChanged += SceneObjectHasChanged;
                        angleObjectB.GetComponent<SceneObject>().hasChanged += SceneObjectHasChanged;
                        angleObjectC.GetComponent<SceneObject>().hasChanged += SceneObjectHasChanged;
                    }else{
                        angleObjectA.GetComponent<SceneObject>().hasChanged -= SceneObjectHasChanged;
                        angleObjectB.GetComponent<SceneObject>().hasChanged -= SceneObjectHasChanged;
                        angleObjectC.GetComponent<SceneObject>().hasChanged -= SceneObjectHasChanged;
                    }
                    break;
                case MeasureTypeEnum.travel:
                    line.positionCount = 1;
                    if(_isActive){
                        //subscribe to the sceneObject.hasChanged event, to not always calculate the line!
                        travelObject.GetComponent<SceneObject>().hasChanged += SceneObjectHasChanged;
                        previousTraveledPos = travelObject.position;
                        lastDistanceForLinePoint = 0;
                        line.SetPosition(0, previousTraveledPos);
                    }else{
                        travelObject.GetComponent<SceneObject>().hasChanged -= SceneObjectHasChanged;
                    }
                    currentDistance = 0f;
                    break;
            }

            if(_isActive)
                UpdateMeasurement();
        }

        //!
        //! Callback to update the measurements if a sceneobject has changed
        //!
        private void SceneObjectHasChanged(object sender, AbstractParameter parameter){
            
            //SceneObject sceneObject = (SceneObject) sender;
            UpdateMeasurement();
            
        }
        #endregion

        #region UNITY FUNCTION
        void Update(){
            if(!module.IsMeasureModuleActive())
                return;

            RotateText();
            //its faster and more easy to just align the text view here
            //instead of creating an event in CameraNavigationModule like CameraViewChangedEvent
            //subscribe to this event here, throw it on every CameraDolly,CameraOrbit and CameraPedestalTruck
            //and execute the below.
        }
        #endregion

        #region UPDATE MEASUREMENT
        private void UpdateMeasurement(){
            // if(!manager.IsMeasureModuleActive())
            //     return;

            switch(measureType){
                case MeasureTypeEnum.line:
                    line.SetPosition(0, startObject.position);
                    line.SetPosition(1, endObject.position);
                    break;
                case MeasureTypeEnum.angle:
                    line.SetPosition(0, angleObjectA.position);
                    line.SetPosition(1, angleObjectB.position);
                    line.SetPosition(2, angleObjectC.position);
                    break;
                case MeasureTypeEnum.travel:
                    //add travel points on the fly if distance is bigger a certain treshold
                    if(currentDistance - lastDistanceForLinePoint > 0.3f * distanceMultiplier){
                        lastDistanceForLinePoint = currentDistance;
                        line.positionCount++;
                        line.SetPosition(line.positionCount-1, previousTraveledPos);
                    }
                    break;
            }
            UpdateText();
        }

        private void UpdateText(){
            //place text as set in the settings
            switch(textPositioning){
                case TextPositioningEnum.fixedPos:
                    //right now, position from prefab, may define v3 for this case
                    switch(measureType){
                        case MeasureTypeEnum.line:
                            break;
                        case MeasureTypeEnum.angle:
                            break;
                        case MeasureTypeEnum.travel:
                            textObj.transform.position = line.GetPosition(0);
                            break;
                    }
                    break;
                case TextPositioningEnum.atLastElement:
                    switch(measureType){
                        case MeasureTypeEnum.line:
                            textObj.transform.position = endObject.position + Vector3.up;
                            break;
                        case MeasureTypeEnum.angle:
                            textObj.transform.position = angleObjectB.position + Vector3.up;
                            break;
                        case MeasureTypeEnum.travel:
                            textObj.transform.position = travelObject.position + Vector3.up;
                            break;
                    }
                    break;
                case TextPositioningEnum.atCenter:
                    switch(measureType){
                        case MeasureTypeEnum.line:
                            textObj.transform.position = (startObject.position + endObject.position)/2f + Vector3.up;
                            break;
                        case MeasureTypeEnum.angle:
                            textObj.transform.position = (angleObjectA.position + angleObjectB.position + angleObjectC.position)/3f;
                            break;
                        case MeasureTypeEnum.travel:
                            textObj.transform.position = (line.GetPosition(0)+travelObject.position)/2f + Vector3.up;
                            break;
                    }
                    break;
            }
            
            switch(measureType){
                case MeasureTypeEnum.line:
                    currentDistance = Vector3.Distance(startObject.position, endObject.position) * distanceMultiplier;
                    textObj.text = currentDistance.ToString(textFormat);
                    break;
                case MeasureTypeEnum.angle:
                    currentAngle = Vector2.SignedAngle(
                        (angleObjectB.position-angleObjectA.position).normalized,
                        (angleObjectB.position-angleObjectC.position).normalized
                    );
                    textObj.text = currentAngle.ToString(textFormat);
                    break;
                case MeasureTypeEnum.travel:
                    currentDistance += Vector3.Distance(previousTraveledPos, travelObject.position) * distanceMultiplier;
                    previousTraveledPos = travelObject.position;
                    textObj.text = currentDistance.ToString(textFormat);
                    break;
            }
            textObj.text += textAffix;

        }
        private void RotateText(){
            textObj.transform.LookAt(Camera.main.transform);
            textObj.transform.Rotate(0, 180, 0);    //we need to rotate it, otherwise its oriented wrongly
        }
        #endregion
    }
}
