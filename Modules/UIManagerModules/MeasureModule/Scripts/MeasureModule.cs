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

        public const string     LOCATION_CANVAS_PREFAB      = "Prefabs/Measurement_Canvas";
        public const string     LOCATION_LINEPOOL_PREFAB    = "Prefabs/MeasurementPool_Line";
        public const string     LOCATION_WPPOOL_PREFAB      = "Prefabs/MeasurementPool_Waypoints";
        public const string     LOCATION_ANGLEPOOL_PREFAB   = "Prefabs/MeasurementPool_Angle";
        public const string     LOCATION_TRVLPOOL_PREFAB    = "Prefabs/MeasurementPool_Travel";
        public const int        CANVAS_SORTING_ORDER        = 15;
        public const string     BUTTON_NAME_PLACE           = "SelectionSensitive/Button_PlaceViaRay";
        public const string     BUTTON_NAME_CREATE_LINE     = "Creation/Button_CreateLine";
        public const string     BUTTON_NAME_CREATE_WP       = "Creation/Button_CreateWaypoints";
        public const string     BUTTON_NAME_CREATE_ANGLE    = "Creation/Button_CreateAngle";
        public const string     BUTTON_NAME_CREATE_TRAVEL   = "Creation/Button_CreateTraveller";
        public const string     BUTTON_NAME_ADD_WP          = "SelectionSensitive/Button_AddWaypoint";
        public const string     BUTTON_NAME_REM_WP          = "SelectionSensitive/Button_RemoveWaypoint";
        public const string     BUTTON_NAME_RESET_DST       = "SelectionSensitive/Button_ResetDistance";
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
        //! the standard Color of selected SceneObject we want to place (during selection it will become green!)
        //!
        private Color objectToPlaceStandardColor;
        //!
        //! to utilite the place function from other functions and execute additional code
        //!
        private UnityEvent additionalPlaceEvent;

        #region Selfdescribing Buttons
        private Button button_placeSelectedObjectViaRay;
        private Button button_createLine;
        private Button button_createWaypoints;
        private Button button_createAngle;
        private Button button_createTraveller;
        private Button button_addWaypoint;
        private Button button_removeWaypoint;
        private Button button_resetDistance;        //reset distance during traveller and waypoints

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

            if(isActive){
                CreateMeasureUI();
                //trigger it once, because if we already selected an object, it would not fire up this listener
                OnSelectionChanged(null, manager.SelectedObjects);
            }else
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
            button_createWaypoints = measureCanvasHolder.transform.Find(BUTTON_NAME_CREATE_WP).GetComponent<Button>();
            button_createWaypoints.onClick.AddListener(OnClick_StartCreation_Waypoints);
            button_createAngle = measureCanvasHolder.transform.Find(BUTTON_NAME_CREATE_ANGLE).GetComponent<Button>();
            button_createAngle.onClick.AddListener(OnClick_StartCreation_Angle);
            button_createTraveller = measureCanvasHolder.transform.Find(BUTTON_NAME_CREATE_TRAVEL).GetComponent<Button>();
            button_createTraveller.onClick.AddListener(OnClick_StartCreation_Traveller);
            
            
            

            button_addWaypoint = measureCanvasHolder.transform.Find(BUTTON_NAME_ADD_WP).GetComponent<Button>();
            button_addWaypoint.onClick.AddListener(OnClick_AddWaypoint);
            button_removeWaypoint = measureCanvasHolder.transform.Find(BUTTON_NAME_REM_WP).GetComponent<Button>();
            button_removeWaypoint.onClick.AddListener(OnClick_RemoveWaypoint);

            button_resetDistance = measureCanvasHolder.transform.Find(BUTTON_NAME_RESET_DST).GetComponent<Button>();
            button_resetDistance.onClick.AddListener(OnClick_ResetDistance);

            HideAllButtons();
            ShowCreationButtons();
        }

        private void HideAllButtons(){
            HideCreationButtons();
            button_addWaypoint.gameObject.SetActive(false);
            button_removeWaypoint.gameObject.SetActive(false);
            button_resetDistance.gameObject.SetActive(false);
            button_placeSelectedObjectViaRay.gameObject.SetActive(false);
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
            //ignore during placing or other functions
            if(inputBlockingCanvas.activeSelf)
                return;

            //Debug.Log("<color=yellow>Measurement.OnSelectionChanged: "+_sceneObjects.Count+"</color>");
            if (_sceneObjects.Count < 1){
                DeactivateSelectionSensitiveButtons();
                ShowCreationButtons();
            }else{
                //Debug.Log("selected "+_sceneObjects[0].gameObject.name);
                MeasurePool selectedMeasurePool = _sceneObjects[0].GetComponentInParent<MeasurePool>();
                if(!selectedMeasurePool || !selectedMeasurePool.IsSceneObjectFromMeasurement(_sceneObjects[0])){
                    DeactivateSelectionSensitiveButtons();
                    ShowCreationButtons();
                    Debug.Log("<color=black>no measure object selected</color>");
                    //reset distance ui
                    uiDistanceText.text = "";
                    return;
                }
                sceneObjectToPlace = _sceneObjects[0];

                button_placeSelectedObjectViaRay.gameObject.SetActive(true);
                //if visible, always interactable button_placeSelectedObjectViaRay.interactable = true;
                selectedMeasurePool.SetDistanceText(uiDistanceText);

                HideCreationButtons();


                switch(selectedMeasurePool.measureType){
                    case MeasurePool.MeasureTypeEnum.line:
                        button_addWaypoint.gameObject.SetActive(false);
                        button_removeWaypoint.gameObject.SetActive(false);
                        button_resetDistance.gameObject.SetActive(false);
                        break;
                    case MeasurePool.MeasureTypeEnum.angle:
                        button_addWaypoint.gameObject.SetActive(false);
                        button_removeWaypoint.gameObject.SetActive(false);
                        button_resetDistance.gameObject.SetActive(false);
                        break;
                    case MeasurePool.MeasureTypeEnum.travel:
                        button_addWaypoint.gameObject.SetActive(false);
                        button_removeWaypoint.gameObject.SetActive(false);
                        button_resetDistance.gameObject.SetActive(true);

                        button_resetDistance.interactable = selectedMeasurePool.GetDistance() > 0;
                        break;
                    case MeasurePool.MeasureTypeEnum.waypoints:
                        button_addWaypoint.gameObject.SetActive(true);
                        button_removeWaypoint.gameObject.SetActive(true);
                        button_resetDistance.gameObject.SetActive(true);

                        button_removeWaypoint.interactable = selectedMeasurePool.GetMeasureObjectCount() > 1;
                        button_resetDistance.interactable = true;   //always enabled, because we have no callback implemented for moving this object
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

            MeshRenderer[] ignoreTheseForRaycasting;
            //ignore (selection) itself
            ignoreTheseForRaycasting = sceneObjectToPlace.GetComponents<MeshRenderer>();

            //TODO for performance we would not need to gather all MeshRenderer on every click (only if we modify any)
            if (SceneRaycastHelper.RaycastIntoScene(core.getManager<SceneManager>().scnRoot, point, out RaycastHit hit, ignoreTheseForRaycasting)){
                //if we hit another measurement-pool object, select this for placing, instead of change  the current position
                //no need to position a measure-object onto any other measure-object
                //TODO: if we are in placement-mode via waypoint-creation, just end placement-mode
                SceneObject sceneObjectWeHit = hit.transform.GetComponent<SceneObject>();
                if(!sceneObjectWeHit)
                    sceneObjectWeHit = hit.transform.GetComponentInParent<SceneObject>();

                if (sceneObjectWeHit){
                    Debug.Log("Hit SceneObject Check if it is a Measure-Object");
                    MeasurePool selectedMeasurePool = sceneObjectWeHit.GetComponentInParent<MeasurePool>();
                    if (selectedMeasurePool && selectedMeasurePool.IsSceneObjectFromMeasurement(sceneObjectWeHit)){
                        Debug.Log("\tYES!");
                        //revert color
                        sceneObjectToPlace.GetComponent<MeshRenderer>().material.color = objectToPlaceStandardColor;
                        //save color
                        objectToPlaceStandardColor = sceneObjectWeHit.GetComponent<MeshRenderer>().material.color;
                        sceneObjectWeHit.GetComponent<MeshRenderer>().material.color = Color.green;
                        OnSelectionChanged(null, new List<SceneObject>() { sceneObjectWeHit });
                        inputBlockingCanvas.SetActive(true);
                        return;
                    }
                }

                //place this object: sceneObjectToPlace (BEWARE: setValue always sets local position and rotation!)
                sceneObjectToPlace.transform.position = hit.point;
                //align to hit normal with upwards vector
                sceneObjectToPlace.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                //move on normal, so the object is not within the object
                sceneObjectToPlace.transform.position += sceneObjectToPlace.transform.up * sceneObjectToPlace.transform.localScale.y / 2f;

                //reset this before, e.g. if we have these events deactivating it
                inputBlockingCanvas.SetActive(true);

                additionalPlaceEvent?.Invoke();
            }else{
                inputBlockingCanvas.SetActive(true);
            }
        }

        /* did not work
        //ongoing placement test could not work with dynamically adding sphere and meshcollider
        //not even if we let them stay, because we would have too much meshcollider
        //try using render path preview
        private RenderTexture gpuTexture;
        private int dataWidth, dataHeight;
        private int scaleResolutionDivisorForPerformance = 1;
        private Texture2D depthTexture;
        private void OnPointerDown_PlaceOngoing_Start(object sender, Vector2 point){
            if(SceneRaycastHelper.DidHitSpecificUI(point, button_placeSelectedObjectViaRay.gameObject)){
                //Debug.Log("Hit Button to stop placing - so dont do any raycast positioning queries!");
                return;
            }

            inputBlockingCanvas.SetActive(false);            

            //ignore MeasurePool Objects, TextMeshes, UI

            //Render the current view via replacement shader - no need to do this ongoing, since we will not change the view during this ongoing check
            Camera camera = Camera.main;

            dataWidth = camera.pixelWidth / scaleResolutionDivisorForPerformance;
            dataHeight = camera.pixelHeight / scaleResolutionDivisorForPerformance;
            gpuTexture = RenderTexture.GetTemporary(dataWidth, dataHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            RenderTexture oldRenderTexture = camera.targetTexture;
            DepthTextureMode oldDepthMode = camera.depthTextureMode;

            camera.depthTextureMode = DepthTextureMode.Depth;
            camera.targetTexture = gpuTexture;
            Shader replacementShader = Shader.Find("Custom/ReplacementShaderForWorldPos");
            camera.RenderWithShader(replacementShader, "");
            
            RenderTexture oldActiveRenderTexture = RenderTexture.active;
            RenderTexture.active = gpuTexture;

            depthTexture = new Texture2D(dataWidth, dataHeight, TextureFormat.ARGB32, false, true);
            depthTexture.ReadPixels(new Rect(0, 0, dataWidth, dataHeight), 0, 0);
            //depthTexture.SetPixel((int)point.x, (int)point.y, Color.red);
            depthTexture.Apply();

            Color textureColorData = depthTexture.GetPixel(dataWidth, dataHeight);
            Debug.Log("Depth: "+textureColorData.r*1f);

            RenderTexture.active = oldActiveRenderTexture;
            camera.targetTexture = oldRenderTexture;
            camera.depthTextureMode = oldDepthMode;
            RenderTexture.ReleaseTemporary(gpuTexture);

            //debug
            GameObject debugGO = new GameObject("Debug RenderTexture");
            DebugRenderTexture debugRT = debugGO.AddComponent<DebugRenderTexture>();
            debugRT.SetTexture(depthTexture);

            // CameraClearFlags oldClearFlags = camera.clearFlags;
            // Color oldBackgroundColor = camera.backgroundColor;
            // RenderingPath oldRenderingPath = camera.renderingPath;
            // bool oldAllowMsaa = camera.allowMSAA;

            // camera.targetTexture = gpuTexture; // Render into render texture.
            // camera.clearFlags = CameraClearFlags.SolidColor; // Make sure non-rendered pixels have _id zero.
            // camera.backgroundColor = Color.clear;
            // camera.renderingPath = RenderingPath.Forward; // No gbuffer required.
            // camera.allowMSAA = false; // Avoid interpolated colors.

            Debug.Log("Rendered Replacement Texture ("+dataWidth+"x"+dataHeight+") once");
        }

        private void OnPointerDown_PlaceOngoing(object sender, Vector2 point){
            Debug.Log("OnPointerDown_PlaceOngoing point: "+point);
            //gather texture data which is the world pos!
            int scaledX = (int)point.x / scaleResolutionDivisorForPerformance;
            int scaledY = (int)point.y / scaleResolutionDivisorForPerformance;
            int indexWouldBe = scaledX + dataWidth * scaledY;
            
            if (indexWouldBe < 0 || indexWouldBe > (dataWidth*dataHeight))
                return;
            
            float depthColorValue01 = 1f-depthTexture.GetPixel(scaledX, scaledY).r;
            Debug.Log("RT color value: "+depthColorValue01);
            float depth = Mathf.Lerp(Camera.main.nearClipPlane, Camera.main.farClipPlane, depthColorValue01);
            Debug.Log("RT depth: "+depth);

            //depthTexture.SetPixel(scaledX, scaledY, Color.red);
            //depthTexture.Apply();

            //beware that hardcoded farplane of 1000 in the replacement shader - the z value here need to be divided by that!
            Vector3 screenToWorldPos = new Vector3(point.x, point.y, depth*1f); //dataHeight-point.y?
            Vector3 pos = Camera.main.ScreenToWorldPoint(screenToWorldPos);
                //new Vector3(textureColorData.r, textureColorData.g, textureColorData.b/1000f);
            sceneObjectToPlace.transform.position = pos;
            Debug.Log("World Pos from via RT color: "+pos);
            
            //Color c = depthTexture.GetPixel(scaledX, dataHeight-scaledY);
            //sceneObjectToPlace.transform.position = new Vector3(c.r, c.g, c.b);
        }*/
        

        #endregion
        private void DeactivateSelectionSensitiveButtons(){
            //always visible
            //button_placeSelectedObjectViaRay.interactable = false;
            button_placeSelectedObjectViaRay.gameObject.SetActive(false);

            //only possible for certain selected waypoint-pool-types
            button_addWaypoint.gameObject.SetActive(false);
            button_removeWaypoint.gameObject.SetActive(false);
            button_resetDistance.gameObject.SetActive(false);
        }

        private void ShowCreationButtons(){
            button_createLine.gameObject.SetActive(true);
            button_createWaypoints.gameObject.SetActive(true);
            button_createAngle.gameObject.SetActive(true);
            button_createTraveller.gameObject.SetActive(true);
        }

        private void HideCreationButtons(){
            button_createLine.gameObject.SetActive(false);
            button_createWaypoints.gameObject.SetActive(false);
            button_createAngle.gameObject.SetActive(false);
            button_createTraveller.gameObject.SetActive(false);
        }

        #region Button Events
        private void OnClick_StartPlaceViaRay(){
            button_placeSelectedObjectViaRay.GetComponentsInChildren<Image>()[1].color = Color.green; //0 is BG, 1 is IMAGE
            //deny any other input action (ui element, gizmo, de-selection)
            //-> maybe enable an overlay that catches all hits?! -> would then need to listen on inputPressStartedUI

            //no need to "save the selected object" - since we really should not be able to select another object if we are in this mode
            //otherwise we could not measure a big object that is a movable scene object (e.g. a car)
            inputBlockingCanvas.SetActive(true);

            HideAllButtons();
            button_placeSelectedObjectViaRay.gameObject.SetActive(true);

            //TODO enable overlay that indicates what to do (frame + text AND "abort"/"finish") (enable overlay for as long as user press another of our button)

            //Save Standard Color
            objectToPlaceStandardColor = sceneObjectToPlace.GetComponent<MeshRenderer>().material.color;
            sceneObjectToPlace.GetComponent<MeshRenderer>().material.color = Color.green;

            //hide gizmo and TRS ui --> we simulate this by de-selecting!
            manager.clearSelectedObject();
            //add event to input manager
            core.getManager<InputManager>().inputPressEnd += OnPointerDown_Place; //needed UI, because of the above input blocking
            //use the below for testing ongoing alignment
                // did not work properly
                // core.getManager<InputManager>().inputPressStartedUI += OnPointerDown_PlaceOngoing_Start;
                // core.getManager<InputManager>().inputMove           += OnPointerDown_PlaceOngoing;
                // core.getManager<InputManager>().inputPressEnd       += OnPointerDown_PlaceOngoing_End;

            button_placeSelectedObjectViaRay.onClick.RemoveListener(OnClick_StartPlaceViaRay);
            button_placeSelectedObjectViaRay.onClick.AddListener(OnClick_StopPlaceViaRay);
        }

        private void OnClick_StopPlaceViaRay(){
            button_placeSelectedObjectViaRay.GetComponentsInChildren<Image>()[1].color = Color.white; //0 is BG, 1 is IMAGE
            //enable other buttons again

            inputBlockingCanvas.SetActive(false);

            //revert color
            sceneObjectToPlace.GetComponent<MeshRenderer>().material.color = objectToPlaceStandardColor;

            //re-enable gizmos by simulate a "reselection"
            manager.clearSelectedObject();
            manager.addSelectedObject(sceneObjectToPlace);

            core.getManager<InputManager>().inputPressStartedUI -= OnPointerDown_Place;

            button_placeSelectedObjectViaRay.onClick.RemoveListener(OnClick_StopPlaceViaRay);
            button_placeSelectedObjectViaRay.onClick.AddListener(OnClick_StartPlaceViaRay);
        }

        private void OnClick_StartCreation_Line(){
            CreateMeasurePoolAtRuntime(GameObject.Instantiate(Resources.Load(LOCATION_LINEPOOL_PREFAB) as GameObject).GetComponent<MeasurePool>());
        }
        private void OnClick_StartCreation_Waypoints(){
            CreateMeasurePoolAtRuntime(GameObject.Instantiate(Resources.Load(LOCATION_WPPOOL_PREFAB) as GameObject).GetComponent<MeasurePool>());
        }
        private void OnClick_StartCreation_Angle(){
            CreateMeasurePoolAtRuntime(GameObject.Instantiate(Resources.Load(LOCATION_ANGLEPOOL_PREFAB) as GameObject).GetComponent<MeasurePool>());
        }
        private void OnClick_StartCreation_Traveller(){
            CreateMeasurePoolAtRuntime(GameObject.Instantiate(Resources.Load(LOCATION_TRVLPOOL_PREFAB) as GameObject).GetComponent<MeasurePool>());
        }

        private void OnClick_AddWaypoint(){
            //Duplicate current selected waypoint
            GameObject newWaypoint = GameObject.Instantiate(sceneObjectToPlace.gameObject);
            //remove old sceneobject comp
            Component.DestroyImmediate(newWaypoint.GetComponent<SceneObject>());
            core.getManager<SceneManager>().simpleSceneObjectList.Add((SceneObject)SceneObject.Attach(newWaypoint, core.getManager<NetworkManager>().cID));
            newWaypoint.transform.parent = sceneObjectToPlace.transform.parent;
            //insert after selected waypoint
            newWaypoint.GetComponentInParent<MeasurePool>().AddMeasurementObject(newWaypoint, sceneObjectToPlace.transform);
            //make sure color is correct, because coroutine could still "pingpong" the creation-color (TODO: make fool-proof, could still be the wrong color)
            sceneObjectToPlace.GetComponent<MeshRenderer>().material.color = objectToPlaceStandardColor;
            
            //place visual next to current selection (in distance relation of current cameras viewport)
            Vector3 viewportPosition = Camera.main.WorldToViewportPoint(newWaypoint.transform.position);
            viewportPosition.x += Mathf.Min((1f - viewportPosition.x)/2f, viewportPosition.x);
            newWaypoint.transform.position = Camera.main.ViewportToWorldPoint(viewportPosition); //newWaypoint.transform.right;

            //set as object we want to place
            sceneObjectToPlace = newWaypoint.GetComponent<SceneObject>();

            //Trigger same Behaviour
            OnClick_StartPlaceViaRay();
            //except that we stop it after placed once
            additionalPlaceEvent = new UnityEvent();
            additionalPlaceEvent.AddListener(OnClick_StopPlaceViaRay);
            additionalPlaceEvent.AddListener(WaypointPlaced);
        }

        private void OnClick_RemoveWaypoint(){
            Transform newWaypointToSelect = sceneObjectToPlace.GetComponentInParent<MeasurePool>().RemoveMeasurementObject(sceneObjectToPlace.gameObject);
            if(!newWaypointToSelect)
                return;

            sceneObjectToPlace.GetComponentInParent<MeasurePool>().TriggerMeasureChange();

            //Destroying SceneObjects currently not supported. Implement as well as adding!
            GameObject.Destroy(sceneObjectToPlace.gameObject);

            manager.clearSelectedObject();
            manager.addSelectedObject(newWaypointToSelect.GetComponent<SceneObject>());
        }

        private void OnClick_ResetDistance(){
            sceneObjectToPlace.GetComponentInParent<MeasurePool>().ResetDistance();
        }
        #endregion

        #region Additional Functions

        private void CreateMeasurePoolAtRuntime(MeasurePool _mp){
            foreach(Transform child in _mp.GetComponentInChildren<Transform>()){
                if (child.gameObject.tag == "editable"){
                    //if (core.isServer){
                        core.getManager<SceneManager>().simpleSceneObjectList.Add((SceneObject)SceneObject.Attach(child.gameObject, core.getManager<NetworkManager>().cID));
                    //}
                }
                core.StartCoroutine(QuickColorHighlight(child.GetComponent<MeshRenderer>(), Color.green));
            }

            Transform cameraTransform = Camera.main.transform;
            //position in front of camera
            _mp.transform.position = cameraTransform.position + cameraTransform.forward * 1f;
            //align to camera rotation
            _mp.transform.LookAt(cameraTransform);

            _mp.GetComponent<MeasurePool>().Init();
            _mp.GetComponent<MeasurePool>().SetMeasureUIAsActive();
    
            //select it (not necessary)
            manager.getModule<SelectionModule>().SetSelectedObjectViaScript(_mp.GetComponentInChildren<SceneObject>());
        }
        private void WaypointPlaced(){
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

        #endregion

    }
}
