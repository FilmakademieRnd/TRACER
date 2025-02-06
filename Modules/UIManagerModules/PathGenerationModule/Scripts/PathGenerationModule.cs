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
//! @folder "PathGenerationModuke"
//! @brief Implementation of the TRACER PathGenerationModule to generate paths for SceneCharacterObjects and send them to AnimHost
//! @author Thomas "Kruegbert" Krüger
//! @version 1
//! @date 06.02.2025



using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace tracer{
        
    //!
    //! APPENDIX
    //! we could make a more sophisticated PathGeneration within Unity via their tool (go around obstactles, etc)
    //!

    public class PathGenerationModule : UIManagerModule{

        public const string     LOCATION_CANVAS_PREFAB      = "Prefabs/PathGeneration_Canvas";
        public const string     LOCATION_PATH_PARTICLE      = "Prefabs/PathTargetClickParticle";
        public const string     LOCATION_LINEPOOL_PREFAB    = "Prefabs/PathLineRenderer";
        //public const string     LOCATION_WPPOOL_PREFAB      = "Prefabs/PathPositionKey";    //this will be(come) SceneObjectPath (should be editable, but not send anything out)
        //public const string     LOCATION_ANGLEPOOL_PREFAB   = "Prefabs/PathRotationKey";
        
        public const int        CANVAS_SORTING_ORDER        = 15;        
        public const string     BUTTON_NAME_LINEAR_PATH     = "Creation/Button_GenerateLinearPath";
        //public const string     BUTTON_NAME_WAYPOINT_PATH   = "Creation/Button_ManualWaypointPath";
        //public const string     BUTTON_NAME_WAYPOINT_ADD    = "Creation/Button_ManualWaypoint_Add";
        //public const string     BUTTON_NAME_WAYPOINT_REM    = "Creation/Button_ManualWaypoint_Rem";
        //public const string     BUTTON_NAME_WAYPOINT_QUIT   = "Creation/Button_ManualWaypoint_Quit";
        public const string     BUTTON_NAME_SEND_PATH       = "Creation/Button_SendPathToAnimHost";
        //public const string     BUTTON_NAME_EDIT_PATH   = "Creation/Button_ShowEditPath"; //click it again to finish
        //public const string     BUTTON_NAME_PLAY_ANIM   = "Creation/Button_PlayReceivedAnim";
        public const string     BLOCKER_NAME_PLACE          = "InputBlocker";
        //public const string     TEXT_NAME_UI_IUNFO          = "UIFurtherInfoText";

        //!
        //! Event linked to the UI command of de/activating this ui
        //!
        public event EventHandler<bool> pathGenerationUIActiveEvent;    

        //!
        //! is the measure ui active or not
        //!
        private bool isActive = false;

        //!
        //! the measure canvas holder
        //!
        private GameObject pathCreationCanvasHolder;
        //!
        //! object that should prevent selecting new objects during other phases
        //!
        private GameObject inputBlockingCanvas;

        //!
        //! selected SceneObject we want to place (for editing the path points or pointing into the world for the path generation)
        //!
        private SceneObject scenePathObjectToPlace;
        //!
        //! the standard Color of selected SceneObject we want to place (during selection it will become green!)
        //!
        private Color sceneObjectPathStandardColor = Color.white;

        //!
        //! The UI button for logging the camera to an object.
        //!
        private MenuButton m_pathGenSelectButton;


        #region Selfdescribing Buttons
        private Button button_generateLinearPath;
        //private Button button_createWaypoints;
        //private Button button_addWaypoint;
        //private Button button_removeWaypoint;
        private Button button_sendPathToAnimHost;

        #endregion


        //!
        //! Constructor
        //! @param name Name of this module
        //! @param Manager reference for this module
        //!
        public PathGenerationModule(string _name, Manager _manager) : base(_name, _manager){
            load = false;
        }

        #region Public Functions
        public bool IsPathGenerationModuleActive(){ return isActive; }

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
            Debug.Log("<color=orange>Init PathGenerationModule</color>");
        }

        //! 
        //! Function called before Unity destroys the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Cleanup(object sender, EventArgs e){
            base.Cleanup(sender, e);
        }
        #endregion

        //it should be enabled only if we select a SceneCharacterObject |OR| any of its connected/shown path data (SceneObjectPath)
        private void TogglePathGenerationUI(){
            isActive = !isActive;

            if(isActive){
                CreatePathGenerationUI();
                //trigger it once, because if we already selected an object, it would not fire up this listener
                OnSelectionChanged(null, manager.SelectedObjects);
            }else
                DestroyPathGenerationUI();

            AddOrRemoveListener(isActive);

            pathGenerationUIActiveEvent?.Invoke(this, isActive);

            if(!isActive){
                //revert color
                if(scenePathObjectToPlace)
                    scenePathObjectToPlace.GetComponent<MeshRenderer>().material.color = sceneObjectPathStandardColor;
                //check if path object was selected and undo if so
                if(manager.isThisOurSelectedObject(scenePathObjectToPlace)){
                    manager.clearSelectedObject();
                }
            }
            Debug.Log("<color=orange>TogglePathGenerationUI: "+isActive+"</color>");
        }

        private void ShowPathGeneration(){
             if (m_pathGenSelectButton == null){
                m_pathGenSelectButton = new MenuButton("", TogglePathGenerationUI, new List<UIManager.Roles>() { UIManager.Roles.SET });
                m_pathGenSelectButton.setIcon("Images/button_pathgeneration");
                manager.addButton(m_pathGenSelectButton);
             }
        }

        private void RemovePathGeneration(){
            //hide the menu button
            if (m_pathGenSelectButton != null){
                DeactivateSelectionSensitiveButtons();
                HideAllButtons();
                TogglePathGenerationUI();

                manager.removeButton(m_pathGenSelectButton);
                m_pathGenSelectButton = null;

                DestroyPathGenerationUI();
            }
        }

        private void CreatePathGenerationUI(){
            pathCreationCanvasHolder = GameObject.Instantiate(Resources.Load(LOCATION_CANVAS_PREFAB) as GameObject);
            pathCreationCanvasHolder.GetComponent<Canvas>().sortingOrder = CANVAS_SORTING_ORDER;

            inputBlockingCanvas = pathCreationCanvasHolder.transform.Find(BLOCKER_NAME_PLACE).gameObject;
            inputBlockingCanvas.SetActive(false);

            button_generateLinearPath = pathCreationCanvasHolder.transform.Find(BUTTON_NAME_LINEAR_PATH).GetComponent<Button>();
            button_generateLinearPath.onClick.AddListener(OnClick_StartCreation_Line);

            button_sendPathToAnimHost = pathCreationCanvasHolder.transform.Find(BUTTON_NAME_SEND_PATH).GetComponent<Button>();
            button_sendPathToAnimHost.onClick.AddListener(OnClick_SendPathToAnimHost);

            HideAllButtons();
            ShowCreationButtons();
        }

        private void HideAllButtons(){
            HideCreationButtons();
            button_sendPathToAnimHost.gameObject.SetActive(false);
        }

        private void DestroyPathGenerationUI(){
            GameObject.Destroy(pathCreationCanvasHolder);
        }

        private void AddOrRemoveListener(bool addListener){
            if(addListener){
                manager.selectionChanged += OnSelectionChanged;
            }else{
                manager.selectionChanged -= OnSelectionChanged;
                //remove listener if we are within this mode, just to be sure
                core.getManager<InputManager>().inputPressEnd -= OnPointerDown_GeneratePath;
            }
        }


        #region Manager Callbacks
        //!
        //! Function called when the selected objects changed.
        //! check if its another SceneCharacterObject (undo current state and setup fresh) or whether its a SceneObjectPath of the current object (otherwise remove ui)
        //!
        //! @param o The UI manager.
        //! @param sceneObjects The list containing the selected objects. 
        //!
        private void OnSelectionChanged(object _o, List<SceneObject> _sceneObjects){
            //Debug.Log("<color=yellow>Measurement.OnSelectionChanged: "+_sceneObjects.Count+"</color>");
            //ignore during placement
            if(inputBlockingCanvas.activeSelf)
                return;

            if (_sceneObjects.Count < 1){
                //remove ui
                RemovePathGeneration();
            }else{
                //check if we selected a SceneCharacterObject or any of its SceneObjectPath "connections"
                //see MeasureModule OnSelectionChanged.263
                //Debug.Log("selected "+_sceneObjects[0].gameObject.name);
                if(!_sceneObjects[0].GetComponentInParent<SceneCharacterObject>()){
                    RemovePathGeneration();
                    Debug.Log("<color=black>no SceneCharacterObject selected</color>");
                    //reset info text too
                    //uiPathGenerationLogText.text = "";
                    return;
                }

                ShowPathGeneration();

                scenePathObjectToPlace = _sceneObjects[0];

                //see MeasurementModule.ShowCaseSensitiveButtons
            }
        }

        //!
        //! Function that is called when the input manager registers a pointer down event
        //! callback only active if want a path to this point
        //!
        //! @param sender A reference to the input manager.
        //! @param point The point in screen space the pointer down event happened.
        //!
        private void OnPointerDown_GeneratePath(object sender, Vector2 point){
            //check if any ui (despite our blocker) was hit
            GameObject uiGo = TimelineModule.GetGameObjectAtUIRaycast(point);
            if(uiGo != inputBlockingCanvas.gameObject){
            //if(SceneRaycastHelper.DidHitSpecificUI(point, button_placeSelectedObjectViaRay.gameObject)){
                //Debug.Log("Hit Button to stop placing - so dont do any raycast positioning queries!");
                return;
            }

            inputBlockingCanvas.SetActive(false);            

            List<MeshRenderer> ignoreTheseForRaycasting = new();
            //ignore (selection) itself
            ignoreTheseForRaycasting.AddRange(scenePathObjectToPlace.GetComponents<MeshRenderer>());
            //also ignore all SceneCharacterObject (TODO: performance improvement - no need to query this all over again all the time)
            foreach(SceneCharacterObject sceneChar in UnityEngine.Object.FindObjectsOfType<SceneCharacterObject>()){
                ignoreTheseForRaycasting.AddRange(sceneChar.GetComponents<MeshRenderer>());
            }
            //ignore all "background" objects (we assume tag "Finish")
            foreach(GameObject g in GameObject.FindGameObjectsWithTag("Finish")){
                ignoreTheseForRaycasting.AddRange(g.GetComponentsInChildren<MeshRenderer>());
            }

            //TODO for performance we would not need to gather all MeshRenderer on every click (only if we modify any)
            if (SceneRaycastHelper.RaycastIntoScene(core.getManager<SceneManager>().scnRoot, point, out RaycastHit hit, ignoreTheseForRaycasting.ToArray())){
                //if we hit another SceneObjectPath, select this for placing, instead of change  the current position
                /*SceneObject sceneObjectWeHit = hit.transform.GetComponent<SceneObject>();
                if(!sceneObjectWeHit)
                    sceneObjectWeHit = hit.transform.GetComponentInParent<SceneObject>();

                if (sceneObjectWeHit){
                    Debug.Log("Hit SceneObject check if valid"); //dont "hit" measurement/path objects! (deny hitting them from above!)
                    //see MeasurementModule
                    if(sceneObjectWeHit.GetType() == typeof(SceneObjectMeasurement)){ //|| typeof(SceneObjectPath)
                        OnSelectionChanged(null, new List<SceneObject>() { sceneObjectWeHit });
                        inputBlockingCanvas.SetActive(true);
                        return;
                    }
                }*/

                //Generate Path to this point
                //CreatePath(abstractParam._parent.gameObject.transform.position, hit.point);
                //Debug.Log("<color=green>Path End: "+hit.point+"</color>");
                //ShowAvailablePath(true, _manager.SelectedObjects[0].GetComponent<SceneCharacterObject>().pathPos);
                //show "send" button
                

                //place this object: sceneObjectToPlace (BEWARE: setValue always sets local position and rotation!)
                scenePathObjectToPlace.transform.position = hit.point;
                //align to hit normal with upwards vector
                scenePathObjectToPlace.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                //move on normal, so the object is not within the object
                scenePathObjectToPlace.transform.position += scenePathObjectToPlace.transform.up * scenePathObjectToPlace.transform.localScale.y / 2f;   
            }
            core.StartCoroutine(DelayDisableInputBlockingCanvas());
        }  

        #endregion
        private void DeactivateSelectionSensitiveButtons(){
            //always visible

            //buttons for deleting specific waypoints, add them, send path to animhost or play the animation
            //they need some other stuff to happen before, thats why they are "case sensitive"

            button_sendPathToAnimHost.gameObject.SetActive(false);
        }

        private void ShowCreationButtons(){
            button_generateLinearPath.gameObject.SetActive(true);
            //waypoint path
        }

        private void HideCreationButtons(){
            button_generateLinearPath.gameObject.SetActive(false);
            //waypoint path
        }

        private void ResetAllButtons(){
            button_generateLinearPath.GetComponentsInChildren<Image>()[1].color = Color.white; //0 is BG, 1 is IMAGE
            button_generateLinearPath.onClick.RemoveListener(OnClick_StopPlaceViaRay);
            button_sendPathToAnimHost.GetComponentsInChildren<Image>()[1].color = Color.white; //0 is BG, 1 is IMAGE
            button_sendPathToAnimHost.onClick.RemoveListener(OnClick_StopPlaceViaRay);
        }


        private void StartPlaceViaRay(Button buttonThatTriggeredThis){
            //deny any other input action (ui element, gizmo, de-selection)
            //-> maybe enable an overlay that catches all hits?! -> would then need to listen on inputPressStartedUI
            buttonThatTriggeredThis.GetComponentsInChildren<Image>()[1].color = Color.green; //0 is BG, 1 is IMAGE

            //no need to "save the selected object" - since we really should not be able to select another object if we are in this mode
            //otherwise we could not measure a big object that is a movable scene object (e.g. a car)
            inputBlockingCanvas.SetActive(true);

            HideAllButtons();
            buttonThatTriggeredThis.gameObject.SetActive(true);

            //TODO enable overlay that indicates what to do (frame + text AND "abort"/"finish") (enable overlay for as long as user press another of our button)

            //Save Standard Color
            //sceneObjectPathStandardColor = scenePathObjectToPlace.GetComponent<MeshRenderer>().material.color;
            //scenePathObjectToPlace.GetComponent<MeshRenderer>().material.color = Color.green;

            //hide gizmo and TRS ui --> we simulate this by de-selecting!
            manager.clearSelectedObject();
            //add event to input manager
            core.getManager<InputManager>().inputPressEnd += OnPointerDown_GeneratePath; //needed UI, because of inputBlockingCanvas

            buttonThatTriggeredThis.onClick.AddListener(OnClick_StopPlaceViaRay);
        }

        #region Button Events
        private void OnClick_StopPlaceViaRay(){
            ResetAllButtons(); //since (it seems) we cannot dynamically pass parameters (and a delegate could not be removed from the listener besides removing all, which we do not want to)
            
            core.getManager<InputManager>().inputPressEnd -= OnPointerDown_GeneratePath;

            //re-enable gizmos by simulate a "reselection"
            manager.clearSelectedObject();
            manager.addSelectedObject(scenePathObjectToPlace);

            //show correct buttons
            HideCreationButtons();
            //see MeasurementModule.ShowCaseSensitiveButtons

            core.StartCoroutine(DelayDisableInputBlockingCanvas());
        }

        private IEnumerator DelayDisableInputBlockingCanvas(){
            //this NEEDS to be done delayed, after the clear & addSelection event. Otherwise the triggered SelectionModule.SelectFunction function
            //will be executed via InputManager.TapFunction which runs into else (TappedUI and Tapped3DUI not correct, since the point)
            yield return new WaitForEndOfFrame();
            inputBlockingCanvas.SetActive(false);
        }

        private void OnClick_StartCreation_Line(){
            //this will simply let the user click anywhere for the raycasting
            StartPlaceViaRay(button_generateLinearPath);
        }

        private void OnClick_SendPathToAnimHost(){
            
        }
        #endregion

    }
}
