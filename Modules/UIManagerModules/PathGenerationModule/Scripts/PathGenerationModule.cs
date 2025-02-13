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
        public const string     LOCATION_PATH_PARTICLE      = "Prefabs/PathTarget_Particles";
        public const string     LOCATION_LINEPOOL_PREFAB    = "Prefabs/Path_LineRenderer";
        //public const string     LOCATION_WPPOOL_PREFAB      = "Prefabs/PathPositionKey";    //this will be(come) SceneObjectPath (should be editable, but not send anything out)
        //public const string     LOCATION_ANGLEPOOL_PREFAB   = "Prefabs/PathRotationKey";
        
        public const int        CANVAS_SORTING_ORDER        = 15;        
        public const string     BUTTON_NAME_GENERATE_PATH   = "Options/Button_CreatePath";
        public const string     BUTTON_NAME_CLICK_TARGET    = "Options/Button_ClickToTarget";    //other UI to be more understandable, but same functionality
        public const string     BUTTON_NAME_SEND_PATH       = "Options/Button_TriggerAnimHostCharAnimation";
        public const string     BUTTON_NAME_EDIT_PATH       = "Options/Button_EditPath"; //click it again to finish
        public const string     BUTTON_NAME_PLAY_PATHANIM   = "Options/Button_PlayPathAnim";

        //these are case sensitive buttons in the bottom right corner
        public const string     BUTTON_NAME_WAYPOINT_PLACE  = "PathEditing/Button_ManualWaypointPath";
        public const string     BUTTON_NAME_WAYPOINT_ADD    = "PathEditing/Button_ManualWaypoint_Add";
        public const string     BUTTON_NAME_WAYPOINT_REM    = "PathEditing/Button_ManualWaypoint_Rem";
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
        private GameObject inputBlocker;
        //!
        //! the line renderer to visualize the path
        //!
        private LineRenderer pathLineRenderer;
        //!
        //! selected SceneCharacterObject we use for this path generation or editing
        //!
        private SceneCharacterObject sceneCharacterForPath;
        //!
        //! selected SceneObject we want to place (for editing the path points or pointing into the world for the path generation)
        //!
        private SceneObject scenePathObjectToPlace; //TODO: make ScenePathObject (like we did with SceneMeasureObject)
        //!
        //! the standard Color of selected SceneObject we want to place (during selection it will become green!)
        //!
        private Color sceneObjectPathStandardColor = Color.white;

        //!
        //! The UI button for logging the camera to an object.
        //!
        private MenuButton m_pathGenSelectButton;


        #region Selfdescribing Buttons
        private Button button_generatePath;
        private Button button_clickToPathTarget;    //for viz wizzle
        private Button button_callAnimHost;
        private Button button_editPath;
        private Button button_playPathAnim;         //the one we received

        //private Button button_placeWaypointViaRay;
        //private Button button_addWaypoint;
        //private Button button_removeWaypoint;

        #endregion

        //!
        //! whether the path is currently streamed or played by us
        //!
        private bool pathAnimIsPlaying = false;

        //!
        //! Constructor
        //! @param name Name of this module
        //! @param Manager reference for this module
        //!
        public PathGenerationModule(string _name, Manager _manager) : base(_name, _manager){
            //load = false;
        }

        #region Public Functions
        public bool IsPathGenerationModuleActive(){ return isActive; }

        #endregion

        #region Setup
        //!
        //! Function when Unity is loaded, to listening on selecting a character for the topmost ui
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //!
        protected override void Init(object _sender, EventArgs _e)
        {
            Debug.Log("<color=orange>Init PathGenerationModule</color>");
            manager.selectionChanged += OnSelectionChanged;
        }

        //! 
        //! Function called before Unity destroys the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Cleanup(object sender, EventArgs e){
            base.Cleanup(sender, e);
            manager.selectionChanged -= OnSelectionChanged;
        }
        #endregion

        private void ShowTopMenuUI(SceneCharacterObject _sco){
            if (m_pathGenSelectButton == null){
                m_pathGenSelectButton = new MenuButton("", TogglePathOptionsUI, new List<UIManager.Roles>() { UIManager.Roles.SET });
                m_pathGenSelectButton.setIcon("Images/button_pathgeneration");
                manager.addButton(m_pathGenSelectButton);
            }
            if(sceneCharacterForPath)
                sceneCharacterForPath.onPathPositionChanged -= PathDataChanged;

            bool newCharacterSelected = sceneCharacterForPath != _sco;
            sceneCharacterForPath = _sco;
            sceneCharacterForPath.onPathPositionChanged += PathDataChanged;

            if(newCharacterSelected){
                DestroyPathObjects();
                DestroyPathLineRenderer();

                GeneratePathObjects();
                GeneratePathLineRenderer();
            }            

            //see MeasurementModule.ShowCaseSensitiveButtons
        }

        private void RemoveTopMenuUI(){
            //hide the menu button
            if (m_pathGenSelectButton != null){
                Debug.Log("REMOVE PATH GENERATION TOP MENU UI");
                if(isActive)
                    TogglePathOptionsUI();

                manager.removeButton(m_pathGenSelectButton);
                m_pathGenSelectButton = null;
            }
        }

        private void TogglePathOptionsUI(){
            isActive = !isActive;

            if(isActive){
                CreatePathOptionUI();
                RefreshOptionButtonsViz();
                SetButtonInteractability(true, false);

                GeneratePathObjects();
                GeneratePathLineRenderer();

                //trigger it once, because if we already selected an object, it would not fire up this listener
                //OnSelectionChanged(null, manager.SelectedObjects);
            }else{
                //remove listener if we are within this mode, just to be sure
                core.getManager<InputManager>().inputPressEnd -= OnPointerDown_GeneratePath;

                if(sceneCharacterForPath)
                    sceneCharacterForPath.onPathPositionChanged -= PathDataChanged;

                DestroyPathObjects();
                DestroyPathLineRenderer();
                DestroyPathOptionUI();
                StopPathAnimations();
            }

            pathGenerationUIActiveEvent?.Invoke(this, isActive);

            //Debug.Log("<color=yellow>TogglePathGenerationUI: "+isActive+"</color>");
        }

        private void CreatePathOptionUI(){
            pathCreationCanvasHolder = GameObject.Instantiate(Resources.Load(LOCATION_CANVAS_PREFAB) as GameObject);
            pathCreationCanvasHolder.GetComponent<Canvas>().sortingOrder = CANVAS_SORTING_ORDER;

            inputBlocker = pathCreationCanvasHolder.transform.Find(BLOCKER_NAME_PLACE).gameObject;
            inputBlocker.SetActive(false);

            button_generatePath = pathCreationCanvasHolder.transform.Find(BUTTON_NAME_GENERATE_PATH).GetComponent<Button>();
            button_generatePath.onClick.AddListener(OnClick_StartCreation_ClickTarget);

            button_clickToPathTarget = pathCreationCanvasHolder.transform.Find(BUTTON_NAME_CLICK_TARGET).GetComponent<Button>();
            button_clickToPathTarget.onClick.AddListener(OnClick_StartCreation_ClickTarget);

            button_callAnimHost = pathCreationCanvasHolder.transform.Find(BUTTON_NAME_SEND_PATH).GetComponent<Button>();
            button_callAnimHost.onClick.AddListener(OnClick_SendPathToAnimHost);

            button_editPath = pathCreationCanvasHolder.transform.Find(BUTTON_NAME_EDIT_PATH).GetComponent<Button>();
            button_editPath.onClick.AddListener(OnClick_StartPathEditing);

            button_playPathAnim = pathCreationCanvasHolder.transform.Find(BUTTON_NAME_PLAY_PATHANIM).GetComponent<Button>();
            button_playPathAnim.onClick.AddListener(OnClick_PlayPathAnim);

        }

        public void PathDataChanged(object sender, EventArgs args){
            //update our already generated path objects
            //update the line renderer
            GeneratePathLineRenderer();
        }
        
        private void GeneratePathObjects(){
            if(sceneCharacterForPath.HasPath()){
                //foreach(Vector3 pathPos in sceneCharacterForPath.pathPos)
                //new gameobject
                //add ScenePathObject
                //tell index
                //rotate via pathRot too
            }
        }
        private void DestroyPathObjects(){
            //foreach(ScenePathObject pathObject in allOurGeneratedPathObject)
            //Destroy
        }
        private void GeneratePathLineRenderer(){
            if(!sceneCharacterForPath.HasPath()){
                DestroyPathLineRenderer();
                return;
            }
            if(!pathLineRenderer)
                pathLineRenderer = GameObject.Instantiate(Resources.Load(LOCATION_LINEPOOL_PREFAB) as GameObject).GetComponent<LineRenderer>();

            pathLineRenderer.SetPositions(sceneCharacterForPath.GetPathPositions());
        }
        private void DestroyPathLineRenderer(){
            if(pathLineRenderer){
                GameObject.Destroy(pathLineRenderer.gameObject);
            }
        }

        private void DestroyPathOptionUI(){
            GameObject.Destroy(pathCreationCanvasHolder);
        }

        private void StopPathAnimations(){
            if(!pathAnimIsPlaying)
                return;

            core.StopCoroutine(PlayPathCharacterAnimationAgain());
            if(manager.getModule<TimelineModule>() != null)
                manager.getModule<TimelineModule>().StopAnimationFromAnotherModule();
        }

        private void RefreshOptionButtonsViz(){
            if(sceneCharacterForPath.HasPath()){
                button_generatePath.gameObject.SetActive(false);
                button_clickToPathTarget.gameObject.SetActive(true);
            }else{
                button_clickToPathTarget.gameObject.SetActive(false);
                button_generatePath.gameObject.SetActive(true);
            }
        }

        private void SetButtonInteractability(bool val, bool resetColorToo = true){
            button_generatePath.interactable        = val;
            button_clickToPathTarget.interactable   = val;
            button_callAnimHost.interactable        = sceneCharacterForPath.HasPath();
            button_editPath.interactable            = sceneCharacterForPath.HasPath();
            button_playPathAnim.interactable        = sceneCharacterForPath.HasPath() && sceneCharacterForPath.HasPathAnimation();

            if(!resetColorToo)
                return;

            button_generatePath.GetComponentsInChildren<Image>()[1].color       = Color.white;
            button_clickToPathTarget.GetComponentsInChildren<Image>()[1].color  = Color.white;
            button_callAnimHost.GetComponentsInChildren<Image>()[1].color       = Color.white;
            button_editPath.GetComponentsInChildren<Image>()[1].color           = Color.white;
            button_playPathAnim.GetComponentsInChildren<Image>()[1].color       = Color.white;
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
            //Debug.Log("<color=yellow>PathGenerationModule.OnSelectionChanged: "+_sceneObjects.Count+"</color>");
            
            //ignore during placement
            if(inputBlocker && inputBlocker.activeSelf)
                return;

            if (_sceneObjects.Count < 1 || !_sceneObjects[0].GetComponentInParent<SceneCharacterObject>()){
                //check if we selected a SceneCharacterObject or any of its SceneObjectPath "connections"
                //see MeasureModule OnSelectionChanged.263
                //Debug.Log("selected "+_sceneObjects[0].gameObject.name);
                
                //remove ui
                RemoveTopMenuUI();
            }else{
                ShowTopMenuUI(_sceneObjects[0].GetComponentInParent<SceneCharacterObject>());
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
            if(uiGo != inputBlocker.gameObject){
            //if(SceneRaycastHelper.DidHitSpecificUI(point, button_placeSelectedObjectViaRay.gameObject)){
                //Debug.Log("Hit Button to stop placing - so dont do any raycast positioning queries!");
                return;
            }

            inputBlocker.SetActive(false);            

            List<MeshRenderer> ignoreTheseForRaycasting = new();
            //ignore (selection) itself
            ignoreTheseForRaycasting.AddRange(sceneCharacterForPath.GetComponents<MeshRenderer>());
            //also ignore all SceneCharacterObject (TODO: performance improvement - no need to query this all over again all the time)
            foreach(SceneCharacterObject sceneChar in UnityEngine.Object.FindObjectsOfType<SceneCharacterObject>()){
                ignoreTheseForRaycasting.AddRange(sceneChar.GetComponents<MeshRenderer>());
            }
            //ignore all "background" objects (we assume tag "Finish")
            foreach(GameObject g in GameObject.FindGameObjectsWithTag("Finish")){
                ignoreTheseForRaycasting.AddRange(g.GetComponentsInChildren<MeshRenderer>());
            }
            //ignore all ScenePathObjects, SceneMeasureObjects


            //TODO for performance we would not need to gather all MeshRenderer on every click (only if we modify any)
            if (SceneRaycastHelper.RaycastIntoScene(core.getManager<SceneManager>().scnRoot, point, out RaycastHit hit, ignoreTheseForRaycasting.ToArray())){
                inputBlocker.SetActive(true);
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

                //would not be visible, since we deactivate the object
                //core.StartCoroutine(PusleInputBlockerColor((Color.green+Color.green)/2f, 3f));  

                CreateLinearPathToTarget(sceneCharacterForPath.transform.position, hit.point);

                //PARTICLE
                GameObject pathTargetParticle = GameObject.Instantiate(Resources.Load(LOCATION_PATH_PARTICLE) as GameObject);
                pathTargetParticle.transform.position = hit.point;
                pathTargetParticle.transform.rotation = Quaternion.FromToRotation(-pathTargetParticle.transform.up, hit.normal);
                GameObject.Destroy(pathTargetParticle, 8f);

                GeneratePathLineRenderer();
                
                EndPathCreation();
                
                //CREATE A SCENEPATHOBJECT (ONLY LOCAL)
                //THEY ARE SPECIAL
                //THEY CAN BE HANDLED AND MOVED LIKE OTHER SCENE OBJECTS
                //... BUUUUUUUUUT
                //THEY CHANGE THE POS/ROT OF THE SCENECHARACTEROBJECT.pathPos/pathRot AnimatedParameter - knowing their indice
                //--> they will never send any lock-msg
                //if the character is locked, all these corresponding ScenePathObjects are locked as well

                //place this object: sceneObjectToPlace (BEWARE: setValue always sets local position and rotation!)
                // scenePathObjectToPlace.transform.position = hit.point;
                // //align to hit normal with upwards vector
                // scenePathObjectToPlace.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                // //move on normal, so the object is not within the object
                // scenePathObjectToPlace.transform.position += scenePathObjectToPlace.transform.up * scenePathObjectToPlace.transform.localScale.y / 2f;   
            }else{
                inputBlocker.SetActive(true);
                //visualize we do not hit anything by animating the InputBlocker color quickly
                core.StartCoroutine(PusleInputBlockerColor(Color.red, 3f));  
            }
        }  

        #endregion

        #region Path Generation
        private void CreateLinearPathToTarget(Vector3 start, Vector3 end){            
            //set to use same height, use start.y, since AnimHost use a planar calculation
            end.y = start.y;

            //AnimatedParameter Creation
            float endFrameTime = 300;
            
            //tangents, right now, use linear interpolation, so same tangent time, pos, center
            Vector3 centerPosForTangents = (start+end)/2f;
            float tangentTime = endFrameTime / 2f;            //tangent time is the middle

            // //AnimHost uses a direction, instead absolut position
            // Vector3 localEnd = (end-start);

            // //local start position (obvious 0,0,0 doo!)
            // Vector3 localStart = Vector3.zero;
            // localEnd.y = start.y;
            // localEnd = Quaternion.Inverse(sceneCharacterForPath.transform.rotation) * localEnd;

            //used to generate the AnimatedParameter KeyList
            IAnimationParameter m_activeParameter;

            //-- v3 position, ignore TRS (0,1,2) -> use 3
            m_activeParameter =  sceneCharacterForPath.parameterList[3] as IAnimationParameter;
            m_activeParameter.clearKeys();
            AbstractKey[] keyList = new AbstractKey[]{  
                new Key<Vector3>(0f,            start,  tangentTime, centerPosForTangents, tangentTime, centerPosForTangents),    //start position
                new Key<Vector3>(endFrameTime,  end,    tangentTime, centerPosForTangents, tangentTime, centerPosForTangents)     //end position
            };
            m_activeParameter.createKeyList(keyList);
            
            //-- q rotation,  ignore TRS (1) -> use 4
            m_activeParameter =  sceneCharacterForPath.parameterList[4] as IAnimationParameter;
            m_activeParameter.clearKeys();
            Quaternion startRotation = sceneCharacterForPath.transform.rotation;
            Quaternion endRotation = Quaternion.LookRotation((end-start).normalized);         //this rotation should right now look from start to end
            keyList = new AbstractKey[]{
                new Key<Quaternion>(0f, startRotation/*Quaternion.identity*/,   tangentTime, startRotation, tangentTime, startRotation),   //start rotation
                new Key<Quaternion>(endFrameTime, endRotation, tangentTime, endRotation,   tangentTime, endRotation)      //end rotation
            };

            m_activeParameter.createKeyList(keyList);
        }
        #endregion


        #region OnClick Events
        private void OnClick_StartCreation_ClickTarget(){
            //this will simply let the user click anywhere for the raycasting
            
            //if we click the button, stop function
            if(inputBlocker.activeSelf){
                AbortPlacementViaRay();
                return;
            }

            //no need to "save the selected object" - since we really should not be able to select another object if we are in this mode
            inputBlocker.SetActive(true);

            //SWITCH BUTTONS
            SetButtonInteractability(false);
            button_generatePath.gameObject.SetActive(false);
            button_clickToPathTarget.gameObject.SetActive(true);
            button_clickToPathTarget.interactable = true;
            button_clickToPathTarget.GetComponentsInChildren<Image>()[1].color   = Color.green;
                        
            //TODO enable overlay that indicates what to do (frame + text AND "abort"/"finish") (enable overlay for as long as user press another of our button)

            //hide gizmo and TRS ui --> we simulate this by de-selecting!
            manager.clearSelectedObject();
            
            //add event to input manager
            core.getManager<InputManager>().inputPressEnd += OnPointerDown_GeneratePath; //needed UI, because of inputBlockingCanvas
        }

        private void OnClick_SendPathToAnimHost(){
            manager.clearSelectedObject();
            sceneCharacterForPath.animHostGen.setValue(1);  //0 stop, 1 stream, 2 stream loop, 3 block
            //trigger function where we wait for the character to become unlocked (after locking) - to know if the animation has finished
        }

        private void OnClick_StartPathEditing(){
            button_editPath.onClick.RemoveListener(OnClick_StartPathEditing);
            button_editPath.onClick.AddListener(OnClick_StopPathEditing);

            button_editPath.GetComponentsInChildren<Image>()[1].color   = Color.green;
        }
        private void OnClick_StopPathEditing(){
            button_editPath.onClick.AddListener(OnClick_StartPathEditing);
            button_editPath.onClick.RemoveListener(OnClick_StopPathEditing);

            button_editPath.GetComponentsInChildren<Image>()[1].color   = Color.white;
        }

        private void OnClick_PlayPathAnim(){
            button_playPathAnim.interactable = false;
            button_playPathAnim.GetComponentsInChildren<Image>()[1].color   = Color.green;

            button_generatePath.interactable = false;
            button_clickToPathTarget.interactable = false;
            button_callAnimHost.interactable = false;
            button_editPath.interactable = false;

            //Start Coro
            core.StartCoroutine(PlayPathCharacterAnimationAgain());
        }
        #endregion

        #region Specific Button Functions

        private void AbortPlacementViaRay(){
            core.getManager<InputManager>().inputPressEnd -= OnPointerDown_GeneratePath;

            //re-enable gizmos by simulate a "reselection"
            //manager.clearSelectedObject();
            manager.addSelectedObject(sceneCharacterForPath);

            //show correct buttons
            RefreshOptionButtonsViz();
            SetButtonInteractability(true);

            core.StartCoroutine(DelayDisableInputBlockingCanvas());
        }

        private void EndPathCreation(){
            core.getManager<InputManager>().inputPressEnd -= OnPointerDown_GeneratePath;

            manager.addSelectedObject(sceneCharacterForPath);

            //show correct buttons
            RefreshOptionButtonsViz();
            SetButtonInteractability(true);

            core.StartCoroutine(DelayDisableInputBlockingCanvas());
        }
        private IEnumerator DelayDisableInputBlockingCanvas(){
            //this NEEDS to be done delayed, after the clear & addSelection event. Otherwise the triggered SelectionModule.SelectFunction function
            //will be executed via InputManager.TapFunction which runs into else (TappedUI and Tapped3DUI not correct, since the point)
            yield return new WaitForEndOfFrame();
            inputBlocker.SetActive(false);
        }

        private IEnumerator PusleInputBlockerColor(Color endColor, float speed){
            float t = 0f;
            RawImage image = inputBlocker.GetComponent<RawImage>();
            if(!image){
                Debug.LogWarning("Cannot execute PusleInputBlockerRed, since the "+BLOCKER_NAME_PLACE+" in the canvas has no RawImage component");
                yield break;
            }
            Color startColor = image.color;
            //IN
            while(t<1f && image){
                t += Time.deltaTime*speed;
                image.color = Color.Lerp(startColor, endColor, t*t);
                yield return null;
            }
            //OUT
            while(t<1f && image){
                t -= Time.deltaTime*speed;
                image.color = Color.Lerp(startColor, endColor, t*t);
                yield return null;
            }
            
            if(image){
                startColor.a = 0f;
                image.color = startColor;
            }
        }

        private IEnumerator PlayPathCharacterAnimationAgain(){
            AnimationManager animManager = core.getManager<AnimationManager>();
            if(core.getManager<AnimationManager>() == null){
                Debug.LogWarning("No AnimationManager exist. Cannot play anim");
                yield break;
            }

            float timer = sceneCharacterForPath.pathPos.getKeys()[0].time;
            float endFrameTime = sceneCharacterForPath.pathPos.getKeys()[^1].time;
            float framerate = 1f/30f;

            if(manager.getModule<TimelineModule>() == null){
                Debug.LogWarning("No TimelineModule exist. Will straightforward just change anim time here");
                pathAnimIsPlaying = true;
                animManager.timelineUpdated(timer);
                while (timer < endFrameTime){
                    yield return new WaitForSecondsRealtime(Mathf.FloorToInt(1000f / core.settings.framerate) / 1000f);
                    timer += framerate;
                    animManager.timelineUpdated(timer);
                }
            }else{
                Debug.Log("Replay animation via Timeline module");
                pathAnimIsPlaying = true;
                yield return core.StartCoroutine(manager.getModule<TimelineModule>().PlayAnimationFromAnotherModule(timer, endFrameTime));
            }
            
            StopPathAnim();
        }

        private void StopPathAnim(){
            pathAnimIsPlaying = false;

            button_playPathAnim.interactable = true;
            button_playPathAnim.GetComponentsInChildren<Image>()[1].color   = Color.white;

            button_generatePath.interactable = true;
            button_clickToPathTarget.interactable = true;
            button_callAnimHost.interactable = true;
            button_editPath.interactable = true;
        }
        #endregion
    }
}
