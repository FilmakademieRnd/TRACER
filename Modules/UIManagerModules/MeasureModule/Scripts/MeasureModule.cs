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

//! @file "MeasureModule.cs"
//! @brief Implementation of the TRACER MeasureModule, ui interface for ingame measurement
//! @author Thomas "Kruegbert" Krüger
//! @version 0
//! @date 02.01.2025


using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace tracer{
        
    //!
    //! UI to enable the ingame distance interface
    //!     BEWARE: this ui could be active AND the other ui when selected a scene object to edit it (even the Tineline could be active too!)
    //!     TODO and add/remove measurement points
    //!

    public class MeasureModule : UIManagerModule{

        public const string     LOCATION_CANVAS_PREFAB      = "Prefabs/MeasurementCanvas";
        public const string     LOCATION_LINEPOOL_PREFAB    = "Prefabs/MeasurementPool_Line";
        public const int        CANVAS_SORTING_ORDER        = 15;
        public const string     BUTTON_NAME_PLACE           = "SelectionSensitive/Button_PlaceViaRay";
        public const string     BUTTON_NAME_CREATE_LINE     = "Creation/Button_CreateLine";
        public const string     BUTTON_NAME_ADD_WP          = "SelectionSensitive/Button_AddWaypoint";
        public const string     BUTTON_NAME_REM_WP          = "SelectionSensitive/Button_RemoveWaypoint";
        public const string     BLOCKER_NAME_PLACE          = "SelectionSensitive/InputBlocker";
        public const string     TEXT_NAME_UI_DISTANCE       = "UIDistanceViz";

        //!
        //! Event linked to the UI command of de/activating this ui
        //!
        public event EventHandler<bool> measurementUIActiveEvent;    

        //!
        //! is the measure ui active or not
        //!
        private bool isActive = false;

        //!
        //! the measure canvas holder
        //!
        private GameObject measureCanvasHolder;
        //!
        //! object that should prevent selecting new objects during other phases
        //!
        private GameObject inputBlockingCanvas;
        //!
        //! object that would display info on the current distance/angle/area
        //!
        private TextMeshProUGUI uiDistanceText;

        //!
        //! selected SceneObject we want to place
        //!
        private SceneObject sceneObjectToPlace;
        //!
        //! to utilite the place function from other functions and execute additional code
        //!
        private UnityEvent additionalPlaceEvent;

        #region Selfdescribing Buttons
        private Button button_placeSelectedObjectViaRay;
        private Button button_createLine;
        private Button button_addWaypoint;
        private Button button_removeWaypoint;

        #endregion


        //!
        //! Constructor
        //! @param name Name of this module
        //! @param Manager reference for this module
        //!
        public MeasureModule(string _name, Manager _manager) : base(_name, _manager)
        {
            //load = false;
        }

        #region Public Functions
        public bool IsMeasureModuleActive(){ return isActive; }

        #endregion

        #region Setup
        //!
        //! Function when Unity is loaded, create the top most ui button
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //!
        protected override void Init(object _sender, EventArgs _e)
        {
            Debug.Log("<color=orange>Init MeasureModule</color>");
            MenuButton measureUIButton = new MenuButton("", ToggleMeasureUI, new List<UIManager.Roles>() { UIManager.Roles.SET });
            measureUIButton.setIcon("Images/button_measure_off");
            manager.addButton(measureUIButton);
        }

        //! 
        //! Function called before Unity destroys the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Cleanup(object sender, EventArgs e)
        {
            base.Cleanup(sender, e);
        }
        #endregion


        private void ToggleMeasureUI(){
            isActive = !isActive;

            if(isActive)
                CreateMeasureUI();
            else
                DestroyMeasureUI();

            AddOrRemoveListener(isActive);

            measurementUIActiveEvent?.Invoke(this, isActive);
            Debug.Log("<color=orange>ToggleMeasureUI: "+isActive+"</color>");
        }

        private void CreateMeasureUI(){
            measureCanvasHolder = GameObject.Instantiate(Resources.Load(LOCATION_CANVAS_PREFAB) as GameObject);
            measureCanvasHolder.GetComponent<Canvas>().sortingOrder = CANVAS_SORTING_ORDER;

            button_placeSelectedObjectViaRay = measureCanvasHolder.transform.Find(BUTTON_NAME_PLACE).GetComponent<Button>();
            button_placeSelectedObjectViaRay.onClick.AddListener(OnClick_StartPlaceViaRay);

            inputBlockingCanvas = measureCanvasHolder.transform.Find(BLOCKER_NAME_PLACE).gameObject;
            inputBlockingCanvas.SetActive(false);

            uiDistanceText = measureCanvasHolder.transform.Find(TEXT_NAME_UI_DISTANCE).GetComponent<TextMeshProUGUI>();
            uiDistanceText.text = "";

            button_createLine = measureCanvasHolder.transform.Find(BUTTON_NAME_CREATE_LINE).GetComponent<Button>();
            button_createLine.onClick.AddListener(OnClick_StartCreation_Line);

            button_addWaypoint = measureCanvasHolder.transform.Find(BUTTON_NAME_ADD_WP).GetComponent<Button>();
            button_addWaypoint.onClick.AddListener(OnClick_AddWaypoint);
            button_removeWaypoint = measureCanvasHolder.transform.Find(BUTTON_NAME_REM_WP).GetComponent<Button>();
            button_removeWaypoint.onClick.AddListener(OnClick_RemoveWaypoint);
        }

        private void DestroyMeasureUI(){
            GameObject.Destroy(measureCanvasHolder);
        }

        private void AddOrRemoveListener(bool addListener){
            if(addListener){
                manager.selectionChanged += OnSelectionChanged;
            }else{
                manager.selectionChanged -= OnSelectionChanged;
            }
        }


        #region Manager Callbacks
        //!
        //! Function called when the selected objects changed.
        //! Will adjust the selection-sensitive buttons. Won't get called if inputBlockingCanvas is active
        //!
        //! @param o The UI manager.
        //! @param sceneObjects The list containing the selected objects. 
        //!
        private void OnSelectionChanged(object _o, List<SceneObject> _sceneObjects){
            //Debug.Log("<color=yellow>Measurement.OnSelectionChanged: "+_sceneObjects.Count+"</color>");
            if (_sceneObjects.Count < 1){
                DeactivateSelectionSensitiveButtons();
            }else{
                //Debug.Log("selected "+_sceneObjects[0].gameObject.name);
                MeasurePool selectedMeasurePool = _sceneObjects[0].GetComponentInParent<MeasurePool>();
                if(!selectedMeasurePool || !selectedMeasurePool.IsSceneObjectFromMeasurement(_sceneObjects[0])){
                    DeactivateSelectionSensitiveButtons();
                    Debug.Log("<color=black>no measure object selected</color>");
                    return;
                }
                sceneObjectToPlace = _sceneObjects[0];
                button_placeSelectedObjectViaRay.interactable = true;

                selectedMeasurePool.SetDistanceText(uiDistanceText);
                switch(selectedMeasurePool.measureType){
                    case MeasurePool.MeasureTypeEnum.line:
                        button_addWaypoint.gameObject.SetActive(false);
                        button_removeWaypoint.gameObject.SetActive(false);
                        break;
                    case MeasurePool.MeasureTypeEnum.angle:
                        button_addWaypoint.gameObject.SetActive(false);
                        button_removeWaypoint.gameObject.SetActive(false);
                        break;
                    case MeasurePool.MeasureTypeEnum.travel:
                        button_addWaypoint.gameObject.SetActive(false);
                        button_removeWaypoint.gameObject.SetActive(false);
                        break;
                    case MeasurePool.MeasureTypeEnum.waypoints:
                        button_addWaypoint.gameObject.SetActive(true);
                        button_removeWaypoint.gameObject.SetActive(true);
                        //enable adding/removing waypoints
                        //- adding should wait for click to query and place it there (but how is "adding" enabled? we would need to select another waypoint...)
                        //- removing should only be enabled if we select a waypoint 
                        break;
                }
            }
        }

        //!
        //! Function that is called when the input manager registers a pointer down event
        //! callback only active if "place via ray" button is active
        //!
        //! @param sender A reference to the input manager.
        //! @param point The point in screen space the pointer down event happened.
        //!
        private void OnPointerDown_Place(object sender, Vector2 point){
            if(SceneRaycastHelper.DidHitSpecificUI(point, button_placeSelectedObjectViaRay.gameObject)){
                //Debug.Log("Hit Button to stop placing - so dont do any raycast positioning queries!");
                return;
            }

            inputBlockingCanvas.SetActive(false);            

            RaycastHit hit;
            if(SceneRaycastHelper.RaycastIntoScene(core.getManager<SceneManager>().scnRoot, point, out hit)){
                //place this object: sceneObjectToPlace
                //sceneObjectToPlace.position.setValue(hit.point); || THIS SET LOCAL-POSITION, IF PARENT IS MOVE ANYWHERE, THIS FUCKS UP EVERYTHING!! WHY IN GODS NAME DO WE USE LOCAL POSITION????
                sceneObjectToPlace.transform.position = hit.point;
                //align to hit normal with upwards vector
                //sceneObjectToPlace.rotation.setValue(Quaternion.FromToRotation(Vector3.up, hit.normal));    //SAME AS ABOVE - WHYYYYYYY?
                sceneObjectToPlace.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

                additionalPlaceEvent?.Invoke();
            }
            
            inputBlockingCanvas.SetActive(true);
        }

        #endregion
        private void DeactivateSelectionSensitiveButtons(){
            button_placeSelectedObjectViaRay.interactable = false;
        }

        #region Button Events
        private void OnClick_StartPlaceViaRay(){
            button_placeSelectedObjectViaRay.GetComponentsInChildren<Image>()[1].color = Color.green; //0 is BG, 1 is IMAGE
            //deny any other input action (ui element, gizmo, de-selection)
            //-> maybe enable an overlay that catches all hits?! -> would then need to listen on inputPressStartedUI

            //no need to "save the selected object" - since we really should not be able to select another object if we are in this mode
            //otherwise we could not measure a big object that is a movable scene object (e.g. a car)
            inputBlockingCanvas.SetActive(true);

            //add event to input manager
            core.getManager<InputManager>().inputPressStartedUI += OnPointerDown_Place; //needed UI, because of the above input blocking

            //enable overlay that indicates what to do (frame + text AND "abort"/"finish") (enable overlay for as long as user press another of our button)

            button_placeSelectedObjectViaRay.onClick.RemoveListener(OnClick_StartPlaceViaRay);
            button_placeSelectedObjectViaRay.onClick.AddListener(OnClick_StopPlaceViaRay);
        }

        private void OnClick_StopPlaceViaRay(){
            button_placeSelectedObjectViaRay.GetComponentsInChildren<Image>()[1].color = Color.white; //0 is BG, 1 is IMAGE
            //enable other buttons again

            inputBlockingCanvas.SetActive(false);

            core.getManager<InputManager>().inputPressStartedUI -= OnPointerDown_Place;

            button_placeSelectedObjectViaRay.onClick.RemoveListener(OnClick_StopPlaceViaRay);
            button_placeSelectedObjectViaRay.onClick.AddListener(OnClick_StartPlaceViaRay);
        }

        private void OnClick_StartCreation_Line(){
            //button_createLine.GetComponentsInChildren<Image>()[1].color = Color.green; //0 is BG, 1 is IMAGE
            //inputBlockingCanvas.SetActive(true);
            
            //Instaniate Prefab (out of view) and 
            GameObject linePool = GameObject.Instantiate(Resources.Load(LOCATION_LINEPOOL_PREFAB) as GameObject);
            foreach(Transform child in linePool.GetComponentInChildren<Transform>()){
                if (child.gameObject.tag == "editable"){
                    //if (core.isServer){
                        core.getManager<SceneManager>().simpleSceneObjectList.Add((SceneObject)SceneObject.Attach(child.gameObject, core.getManager<NetworkManager>().cID));
                    //}
                }
                core.StartCoroutine(QuickColorHighlight(child.GetComponent<MeshRenderer>(), Color.green));
            }

            linePool.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 1f;
            linePool.GetComponent<MeasurePool>().Init();
            linePool.GetComponent<MeasurePool>().SetMeasureUIAsActive();
    
            //select it (not necessary)
            manager.getModule<SelectionModule>().SetSelectedObjectViaScript(linePool.GetComponentInChildren<SceneObject>());
            
            //start placing specific objects (or simply let this be done via the ingame behaviour [at least for line])
            //core.getManager<InputManager>().inputPressStartedUI += OnPointerDown_CreateLine;
        }

        private void OnClick_AddWaypoint(){
            //Duplicate current selected waypoint
            GameObject newWaypoint = GameObject.Instantiate(sceneObjectToPlace.gameObject);
            //remove old sceneobject comp
            Component.DestroyImmediate(newWaypoint.GetComponent<SceneObject>());
            core.getManager<SceneManager>().simpleSceneObjectList.Add((SceneObject)SceneObject.Attach(newWaypoint, core.getManager<NetworkManager>().cID));
            newWaypoint.transform.parent = sceneObjectToPlace.transform.parent;
            newWaypoint.GetComponentInParent<MeasurePool>().AddMeasurementObject(newWaypoint);
            //set as object we want to place
            sceneObjectToPlace = newWaypoint.GetComponent<SceneObject>();

            additionalPlaceEvent = new UnityEvent();
            inputBlockingCanvas.SetActive(true);
            core.getManager<InputManager>().inputPressStartedUI += OnPointerDown_Place;
            button_addWaypoint.onClick.RemoveListener(OnClick_AddWaypoint);

            additionalPlaceEvent.AddListener(WaypointPlaced);
        }

        private void OnClick_RemoveWaypoint(){
            sceneObjectToPlace.GetComponentInParent<MeasurePool>().RemoveMeasurementObject(sceneObjectToPlace.gameObject);
            sceneObjectToPlace.GetComponentInParent<MeasurePool>().TriggerMeasureChange();
            //Destroying SceneObjects currently not supported. Implement as well as adding!
            GameObject.Destroy(sceneObjectToPlace.gameObject);
            
        }
        #endregion

        private void WaypointPlaced(){
            inputBlockingCanvas.SetActive(false);
            button_addWaypoint.onClick.AddListener(OnClick_AddWaypoint);
            core.getManager<InputManager>().inputPressStartedUI -= OnPointerDown_Place;
            core.StartCoroutine(QuickColorHighlight(sceneObjectToPlace.GetComponent<MeshRenderer>(), Color.green));
            sceneObjectToPlace.GetComponentInParent<MeasurePool>().TriggerMeasureChange();
            additionalPlaceEvent = new UnityEvent();
        }

        private IEnumerator QuickColorHighlight(MeshRenderer _mrToHighlight, Color _colorForHighlight, float _speedMultiplier = 1f){
            if(!_mrToHighlight)
                yield break;

            float t = 0f;
            int times = 2;
            Material m = _mrToHighlight.material;
            Color colorWas = m.color;
            for(int x = 0; x<times; x++){
                //quick in
                while(t<1f){
                    t += Time.deltaTime * 4f * _speedMultiplier;
                    m.color = Color.Lerp(colorWas, _colorForHighlight, t);
                    yield return null;
                }
                //stay
                yield return new WaitForSeconds(0.2f/_speedMultiplier);
                //quick out
                while(t>0f){
                    t -= Time.deltaTime * 4f * _speedMultiplier;
                    m.color = Color.Lerp(colorWas, _colorForHighlight, t);
                    yield return null;
                }
            }
            m.color = colorWas;
        }

    }
}
