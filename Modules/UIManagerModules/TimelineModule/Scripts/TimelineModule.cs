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

//! @file "MenuCreatorModule.cs"
//! @brief Implementation of the TimelineModule, creating icons for scene objects without geometry.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 29.03.2024

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace tracer
{
    public class TimelineModule : UIManagerModule
    {
        //TODO add these options to the settings
        //furthermore: add option to loop from first to last keyframe in current view
        private const int TIMELINE_START_MINIMUM = -10;     //the most minimum frameNr StartTime can become
        private const bool STOP_ON_LAST_KEYFRAME = false;   //if so, timeline stops playing on current selected last keyframe
        private const bool LOOP_IN_CURRENT_VIEW = true;     //while playing, loop if end time is reached
        private const bool SET_TIME_TO_MARKED_KEYFRAME = true; //if so, the red line jumps to the marked keyframe AND updates if we move it
        
        //private const bool ONLY_ALLOW_KEYFRAMES_AT_EMPTY_INTS = true; //minimum distance between keyframes is _ONE_ frame
        //needs to be calculated by the current frame-size because our times are arbitrary floats

        public int m_framerate = 30;
        public bool m_isPlaying = false;
        public bool m_invertScrollAndDrag = false;
        
        //!
        //! The text displayed above the timeline, showing the start frame number.
        //!
        private Text m_startFrameDisplay;
        //!
        //! The text displayed above the timeline, showing the end frame number.
        //!
        private Text m_endFrameDisplay;
        //!
        //! The text displayed above the timeline, showing the current frame number.
        //!
        private Text m_currentFrameDisplay;
        //!
        //! A reference to the play button of the timeline.
        //!
        private Button m_playButton;
        //!
        //! A reference to the previous key button of the timeline.
        //!
        private Button m_prevButton;
        //!
        //! A reference to the mext key button of the timeline.
        //!
        private Button m_nextButton;
        //!
        //! Prefab for the Unity canvas object.
        //!
        private GameObject m_canvas;
        //!
        //! A reference to the prefab of a keyframe box in the timeline.
        //!
        private GameObject m_keyframePrefab;
        //!
        //! A reference to the timeline GameObject.
        //!
        private GameObject m_timeLine;
        //!
        //! A reference to the timeline rect transform.
        //!
        private RectTransform m_timelineRect;
        //!
        //! A reference to the redline transform.
        //!
        private RectTransform m_redLine;
        //!
        //! bool whether we have a sceneObject selected to toggle the "add keyframe" button
        //!
        private bool m_sceneObjectSelected = false;
        //!
        //! The list in which all GUI keyframes gameObjects are registered.
        //!
        private List<GameObject> m_keyframeObjectList;
        //!
        //! The list in which all keyframes are registered.
        //!
        private List<KeyFrame> m_keyframeList;
        //!
        //! The index of the last active keyframe;
        //!
        private int m_activeKeyframeIndex = -1;
        //!
        //! The list containing all UI elemets of the current menu.
        //!
        private List<GameObject> m_uiElements;
        //!
        //! The current time of the animation.
        //!
        private float m_currentTime = 0;
        //!
        //! Flag that defines whether the time line is shown or not.
        //!
        private bool m_showTimeLine = false;
        //!
        //! Flag that defines whether a UI is selected or not.
        //!
        private bool m_isSelected = false;
        //!
        //! Flag that defines whether we did a zoom or moved the timeline
        //!
        private bool m_didSpecialAction = false;
        
        //!
        //! A reference to TRACER's input manager.
        //!
        private InputManager m_inputManager;
        //!
        //! A reference to TRACER's animation manager.
        //!
        private AnimationManager m_animationManager;
        //!
        //! The coroutine fired in play mode.
        //!
        private Coroutine m_playCoroutine;
        //!
        //! A reference the the snap select UI element.
        //!
        private SnapSelect m_snapSelect;
        //!
        //! A reference the the UI manipualtor element (spinner or color select)
        //!
        private Manipulator m_manipulator;
        //!
        //! A reference the current active parameter the key are displayed by the timeline.
        //!
        private IAnimationParameter m_activeParameter = null;
        //!
        //! All objects that were animated, to check for _lock
        //!
        private List<SceneObject> m_allAnimatedObjects = null;
        //!
        //! Time we touch/clicked, needed to check if we drag the time or just click
        //!
        private float m_initialTouchTime = 0f;
        //!
        //! did we call to lock and set all animated sceneobject to playedByTimeline? necessary for scrubbing
        //!
        private bool m_animatedSceneObjectsLockCalled = false;
        
        //!
        //! The visible start time of the timeline.
        //!
        private float m_startTime = 0;
        
        //!
        //! The UI button to add a key.
        //!
        private Button m_addKeyButton;
        //!
        //! The UI button to remove a key.
        //!
        private Button m_removeKeyButton;
        //!
        //! The UI button to remove all keys.
        //!
        private Button m_removeAllKeysButton;
        
        //!
        //! Getter/Setter for the start time of the timeline.
        //!
        private float StartTime
        {
            get { return m_startTime; }
            set
            {
                m_startTime = value;
                m_startFrameDisplay.text = Mathf.RoundToInt(m_startTime * m_framerate).ToString();
            }
        }
        //!
        //! The visible end time of the timeline.
        //!
        private float m_endTime = 5;
        //!
        //! Getter/Setter for the end time of the timeline.
        //!
        public float EndTime
        {
            get { return m_endTime; }
            set
            {
                m_endTime = value;
                m_endFrameDisplay.text = Mathf.RoundToInt(m_endTime * m_framerate).ToString();
            }
        }
        //!
        //! Initial position we touched, only important for zooming via alt + click
        //!
        private Vector2 m_initalTouchPos;
        //!
        //! pos buffer to calculate the drag movement delta which we already get from touches
        //!
        private Vector2 m_posDragBuffer;
        //!
        //! bool to execute a drag during the OnMove event (when clicking middle button)
        //!
        private bool m_isMiddleClickDrag = false;

        //!
        //! last time we pinched, to not switch between pinch and grab on touch, since it happens all the time
        //!
        private float m_timeLastZoomed = 0;
        //!
        //! last time we two finger grabbed the timeline, to not switch between pinch and grab on touch, since it happens all the time
        //!
        private float m_timeLastDragged = 0;
        //!
        //! last keyframe to use during playback for certain checks
        //!
        private KeyFrame lastKeyFrame;

        //!
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public TimelineModule(string name, Manager manager) : base(name, manager)
        {
            //load = false;
        }

        #region Setup

        //!
        //! Init Function
        //!
        protected override void Init(object sender, EventArgs e)
        {
            m_keyframeObjectList = new List<GameObject>();
            m_keyframeList = new List<KeyFrame>();
            m_uiElements = new List<GameObject>();

            m_canvas = Resources.Load("Prefabs/TimelineCanvas") as GameObject;
            m_keyframePrefab = Resources.Load("Prefabs/KeyFrameTemplate") as GameObject;

            MenuButton hideTimelineButton = new MenuButton("", toggleTimeLine, new List<UIManager.Roles>() { UIManager.Roles.SET });
            hideTimelineButton.setIcon("Images/button_timelineOnOff");
            manager.addButton(hideTimelineButton);

            m_inputManager = core.getManager<InputManager>();
            m_animationManager = core.getManager<AnimationManager>();
            manager.UI2DCreated += On2DUIReady;

            m_allAnimatedObjects = new();
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

            manager.UI2DCreated -= On2DUIReady;
        }


        //!
        //! Function that toggles whether icons are shown or not.
        //!
        private void toggleTimeLine()
        {
            if (m_showTimeLine)
            {
                m_showTimeLine = false;
                destroyTimeline();
            }
            else
            {
                m_showTimeLine = true;
                createTimeline(); 
                StartAnimGen();
            }
        }

        //!
        //! Function creating the timeline widget.
        //!
        //! @param sender A reference to the UI manager.
        //!
        void createTimeline()
        {
            GameObject menuCanvas = GameObject.Instantiate(m_canvas);
            menuCanvas.GetComponent<Canvas>().sortingOrder = 15;
            m_uiElements.Add(menuCanvas);

            Transform menuTimeLineTransform = menuCanvas.transform.GetChild(0);
            m_timelineRect = menuTimeLineTransform.GetComponent<RectTransform>();

            // get the time line GameObject
            m_timeLine = menuTimeLineTransform.gameObject;

            // get redline component
            m_redLine = menuTimeLineTransform.Find("RedLine").GetComponent<RectTransform>();

            // get text component
            m_startFrameDisplay = menuTimeLineTransform.Find("StartFrameNumber").GetComponent<Text>();

            // get text component
            m_endFrameDisplay = menuTimeLineTransform.Find("EndFrameNumber").GetComponent<Text>();

            // get text component
            m_currentFrameDisplay = m_redLine.GetChild(0).GetComponent<Text>();

            // get play button component
            m_playButton = menuTimeLineTransform.Find("PlayButton").GetComponent<Button>();

            // get prev button component
            m_prevButton = menuTimeLineTransform.Find("PrevButton").GetComponent<Button>();

            // get prev button component
            m_nextButton = menuTimeLineTransform.Find("NextButton").GetComponent<Button>();

            // get Ad Key button component
            m_addKeyButton = menuTimeLineTransform.Find("AddKeyButton").GetComponent<Button>();

            //get Remove Key button component
            m_removeKeyButton = menuTimeLineTransform.Find("RemoveKeyButton").GetComponent<Button>();

            //get Remove all Keys component
            m_removeAllKeysButton = menuTimeLineTransform.Find("ClearKeyButton").GetComponent<Button>();

            m_startFrameDisplay.text = Mathf.RoundToInt(m_startTime * m_framerate).ToString();
            m_endFrameDisplay.text = Mathf.RoundToInt(m_endTime * m_framerate).ToString();

            AddOrRemoveListener(true);

            if (manager.SelectedObjects.Count > 0)
                CreateFrames(m_activeParameter);

            m_sceneObjectSelected = manager.SelectedObjects.Count > 0;

            updateButtonInteractability();
            setTime(m_animationManager.time);
        }

        private void AddOrRemoveListener(bool addListener){
            if(addListener){
                m_playButton.onClick.                   AddListener(play);
                m_prevButton.onClick.                   AddListener(prevFrame);
                m_nextButton.onClick.                   AddListener(nextFrame);
                m_addKeyButton.onClick.                 AddListener(AddKey);
                m_removeKeyButton.onClick.              AddListener(RemoveKey);
                m_removeAllKeysButton.onClick.          AddListener(RemoveAllKeys);

                m_inputManager.inputPressStartedUI      += OnPointerDown; //OnBeginDrag;
                m_inputManager.inputPressEnd            += OnPointerEnd;
                m_inputManager.inputMove                += OnMove;
                m_inputManager.twoDragEvent             += OnTwoFingerDrag;
                
                m_inputManager.middleClickPressEvent    += OnMiddleClickPress;
                m_inputManager.middleClickMoveEvent     += OnMiddleClickHold;
                m_inputManager.middleClickReleaseEvent  += OnMiddleClickRelease;
                
                m_inputManager.pinchDetailedEvent       += OnPinchDetail;
                manager.selectionChanged                += OnSelectionChanged;

                manager.m_manipulation3dDoneEvent       += OnKeyframeValueManipulated;
            }else{
                m_playButton.onClick.                   RemoveListener(play);
                m_prevButton.onClick.                   RemoveListener(prevFrame);
                m_nextButton.onClick.                   RemoveListener(nextFrame);
                m_addKeyButton.onClick.                 RemoveListener(AddKey);
                m_removeKeyButton.onClick.              RemoveListener(RemoveKey);
                m_removeAllKeysButton.onClick.          RemoveListener(RemoveAllKeys);

                m_inputManager.inputPressStartedUI      -= OnPointerDown; //OnBeginDrag;
                m_inputManager.inputPressEnd            -= OnPointerEnd;
                m_inputManager.inputMove                -= OnMove;
                m_inputManager.twoDragEvent             -= OnTwoFingerDrag;
                
                m_inputManager.middleClickPressEvent    -= OnMiddleClickPress;
                m_inputManager.middleClickMoveEvent     -= OnMiddleClickHold;
                m_inputManager.middleClickReleaseEvent  -= OnMiddleClickRelease;
                
                m_inputManager.pinchDetailedEvent       -= OnPinchDetail;
                manager.selectionChanged                -= OnSelectionChanged;

                manager.m_manipulation3dDoneEvent       -= OnKeyframeValueManipulated;
            }
        }

        //!
        //! setup button to be interactable or not
        //!
        private void updateButtonInteractability(){
            //if not initialized
            if(!m_timelineRect)
                return;

            if(m_isPlaying){
                //no button should be pressable while we are playing, despite the stop button
                updateButtonBgColor(m_playButton.GetComponentInChildren<Image>(), Color.green); //first child will be BG
                m_removeKeyButton.interactable = false;
                m_removeAllKeysButton.interactable = false;
                m_nextButton.interactable = false;
                m_prevButton.interactable = false;
                m_addKeyButton.interactable = false;
                return;
            }else{
                updateButtonBgColor(m_playButton.GetComponentInChildren<Image>(), Color.black); //first child will be BG
            }

            if(m_keyframeObjectList.Count == 0){
                m_nextButton.interactable = false;
                m_prevButton.interactable = false;
                m_removeKeyButton.interactable = false;
                m_removeAllKeysButton.interactable = false;
            }else{
                m_nextButton.interactable = true;
                m_prevButton.interactable = true;
                m_removeAllKeysButton.interactable = true;
            }

            if(m_activeKeyframeIndex > -1){
                m_removeKeyButton.interactable = true;
                if(m_keyframeList[m_activeKeyframeIndex].key.time == m_currentTime)
                    m_addKeyButton.interactable = false;
                else{
                    m_addKeyButton.interactable = !checkIfTimeIsAtKeyframe();

                }
            }else{
                m_removeKeyButton.interactable = false;
                if(!checkIfTimeIsAtKeyframe())
                    m_addKeyButton.interactable = m_sceneObjectSelected;
                else
                    m_addKeyButton.interactable = false;
            }

            //m_addKeyButton.interactable = m_sceneObjectSelected;   
        }

        //!
        //! change the color of the image respectively and remain its alpha value
        //!
        private void updateButtonBgColor(Image i, Color c){
            Color color = i.color;
            //remain its alpha value   
            float alpha = color.a;
            color = c;
            color.a = alpha;
            i.color = color;
        }
        //!
        //! check over all keyframes if current time is the same and disallow creating!
        //!

        private bool checkIfTimeIsAtKeyframe(){
            bool currentTimeIsNearKeyframe = false;
            foreach(KeyFrame kf in m_keyframeList){
                if(Mathf.Abs(kf.key.time-m_currentTime) < 0.001f){
                    currentTimeIsNearKeyframe = true;
                    break;
                }
            }
            return currentTimeIsNearKeyframe;
        }

        //!
        //! Function to destroy all created UI elements of a timeline.
        //!
        private void destroyTimeline()
        {
            if (m_isPlaying){
                m_isPlaying = false;
                core.StopCoroutine(playCoroutine());
            }

            clearFrames();
            clearUI();

            AddOrRemoveListener(false);
        }

        //!
        //! Destroys all keyframe game objects within timeline.
        //!
        private void clearFrames()
        {
            foreach (GameObject g in m_keyframeObjectList)
                GameObject.Destroy(g);
            m_keyframeObjectList.Clear();
            m_keyframeList.Clear();
            m_activeKeyframeIndex = -1;
            updateButtonInteractability();
        }

        //!
        //! Destroys all timeline UI game objects exept keyframes.
        //!
        private void clearUI()
        {
            foreach (GameObject uiElement in m_uiElements)
                GameObject.Destroy(uiElement);
            m_uiElements.Clear();
        }

        #endregion

        //!
        //! set the current time (of the animation) in the timeline
        //! @param      time        current time at the red line of the timeline
        //!
        private void setTime(float time, bool clampTimeIntoScope = true){
            time = Mathf.Max(time, 0f);

            if(clampTimeIntoScope){
                if (time > m_endTime)
                    m_currentTime = m_endTime;
                else if(time < m_startTime)
                    m_currentTime = m_startTime;
                else
                    m_currentTime = time;
            }else{
                m_currentTime = time;
            }

            updatePositionOnTimeline();

            m_currentFrameDisplay.text = Mathf.RoundToInt(m_currentTime * m_framerate).ToString();
            m_animationManager.timelineUpdated(time);
        }

        private bool isTimelineTimeOutOfScope(){ return m_currentTime < StartTime || m_currentTime > EndTime; }
        private bool wouldTimelineTimeBeOutOfScope(float timeToBe){ return timeToBe < StartTime || timeToBe > EndTime; }

        //!
        //!  update position of time (red line), that could be out of timeline scope
        //!
        private void updatePositionOnTimeline(){
            if (m_currentTime > m_endTime){                 //HIDE
                if(m_redLine.gameObject.activeSelf){
                    m_redLine.gameObject.SetActive(false);
                    m_endFrameDisplay.color = Color.red;    //mark hidden side
                }
            }else if (m_currentTime < m_startTime){         //HIDE
                if(m_redLine.gameObject.activeSelf){
                    m_redLine.gameObject.SetActive(false);
                    m_startFrameDisplay.color = Color.red;  //mark hidden side
                }
            }else{                                          //SHOW
                if(!m_redLine.gameObject.activeSelf){
                    m_redLine.gameObject.SetActive(true);
                    m_startFrameDisplay.color = Color.white;
                    m_endFrameDisplay.color = Color.white;
                }
            }
            m_redLine.localPosition = new Vector3(mapToTimelinePosition(m_currentTime), 0, 0);
        }

        //!
        //! Function called when the selected objects changed.
        //! Will remove all keyframes from timeline UI is new selection is empty.
        //!
        //! @param o The UI manager.
        //! @param sceneObjects The list containing the selected objects. 
        //!
        private void OnSelectionChanged(object o, List<SceneObject> sceneObjects)
        {
            Debug.Log("<color=yellow>Timeline.OnSelectionChanged</color>");
            if (sceneObjects.Count < 1)
            {
                m_sceneObjectSelected = false;
                clearFrames();
                
                RemoveCallbacks(true);

                StopAnimGen();
            }

            if (sceneObjects.Count > 0)
            {
                //UpdateCallbacks();
                m_sceneObjectSelected = true;
                StartAnimGen();
            }
            updateButtonInteractability();
        }

        
        private void StartAnimGen()
        {
            m_animationManager.OnStartAnimaGeneration(null);
        }

        private void StopAnimGen()
        {
            m_animationManager.OnStopAnimaGeneration(null);
        }

        //!
        //! Callback when a keyframe was manipulated via a 3d module
        //!
        //! @param o The UI manager.
        //! @param sceneObjects The list containing the selected objects. 
        //!
        public void OnKeyframeValueManipulated(object sender, AbstractParameter para){
            UpdateCurrentKeyframeValue(para); 
        }
        
        //!
        //! Function called when a keyframe is created (actually is called via parameter everytime the value changes!)
        //! Will remove all keyframes from timeline UI is new selection is empty.
        //!
        //! @param o The UI manager.
        //! @param sceneObjects The list containing the selected objects. 
        //!
        private void OnKeyAddOrRemoved(object o, EventArgs e)
        {
            //only execute for creation: only if selected and visible
            if(manager.SelectedObjects.Count < 0 || !m_showTimeLine)
                return;

            Debug.Log("<color=yellow>OnKeyAddOrRemoved</color>");
            clearFrames();
            //CreateFrames(m_activeParameter);  //-> did not update the light color correctly!
            CreateFrames((IAnimationParameter) o);
        }

        //!
        //! Updates the displayed keys according to the given scene objecs and their 
        //! containt parameters with it's keys. Will add/remove frames if neccessary.
        //! @param sceneObjects Scene object for which to display the timeline.
        //!
        private void On2DUIReady(object o, UIBehaviour ui)
        {

            //Debug.Log("<color=green>On2DUIReady</color>");
            
            UpdateCallbacks(ui);
            
            if(!m_showTimeLine)
                return;
            
            clearFrames();
            CreateFrames(m_activeParameter);

        }

        //remove and add callbacks
        private void UpdateCallbacks(UIBehaviour ui, int parameterIndex = 0){
            //******************* remove
            RemoveCallbacks(ui != null);

            //******************* add
            AddCallbacks(ui, parameterIndex);
        }

        private void RemoveCallbacks(bool removeSnapSelect){
            if (m_activeParameter != null){
                m_activeParameter.keyHasChanged -= OnKeyAddOrRemoved;
                m_activeParameter = null;
            }
            if(removeSnapSelect && m_snapSelect)
                m_snapSelect.parameterChanged -= OnParameterChanged;
            if(m_manipulator)
                m_manipulator.doneEditing -= OnManipulatorEditEnded;
        }

        private void AddCallbacks(UIBehaviour ui, int parameterIndex){
            if (manager.SelectedObjects.Count > 0){
                m_activeParameter = manager.SelectedObjects[0].parameterList[parameterIndex] as IAnimationParameter;
                m_activeParameter.keyHasChanged += OnKeyAddOrRemoved;
            }
            if(ui){
                m_snapSelect = (SnapSelect) ui;
                m_snapSelect.parameterChanged += OnParameterChanged;
            }
            //TODO: somehow get currentManipulator from UICreator2DModule
            //is this ugly?
            UICreator2DModule ui2DModule = manager.getModule<UICreator2DModule>();
            if(ui2DModule != null && ui2DModule.GetManipulator()){
                m_manipulator = ui2DModule.GetManipulator().GetComponent<Manipulator>();
                if(m_manipulator)
                    m_manipulator.doneEditing += OnManipulatorEditEnded;
            }
        }

        //! 
        //! Function called when the parameter selected by the UI changed.
        //! Updates the timeline widgets based on the parameters data.
        //!
        //! @param o The UI element (SnapSelect) that changes the selected parameter.
        //! @param idx The index of the parameter in the selected objects parameter list.
        //!
        private void OnParameterChanged(object o, int idx)
        {
            //Debug.Log("<color=green>OnParameterChanged for index "+idx+"</color>");
            clearFrames();

            UpdateCallbacks(null, idx);
            
            CreateFrames(m_activeParameter);

            keyframeDeselected();
        }

        //! 
        //! Function called when the parameter selected by the UI changed.
        //! Updates the timeline widgets based on the parameters data.
        //!
        //! @param o The UI element (SnapSelect) that changes the selected parameter.
        //! @param para changed abstract parameter
        //!
        private void OnManipulatorEditEnded(object o, AbstractParameter para)
        {
            Debug.Log("<color=green>OnManipulatorEditEnded</color>");
            UpdateCurrentKeyframeValue(para);
        }

        //! 
        //! Function that change the current selected keyframe value
        //! because we changed values via gizmo or ui
        //!
        //! @param para changed abstract parameter
        //!
        private void UpdateCurrentKeyframeValue(AbstractParameter para){
            if(m_activeKeyframeIndex < 0 || m_activeParameter == null)
                return;

            Debug.Log("<color=green>UpdateCurrentKeyframeValue</color>");
            m_activeParameter.updateKey(m_activeKeyframeIndex);
            if (m_activeParameter is Parameter<Vector3> && para.name == "position")
                m_animationManager.OnRenewSplineContainer(null);
        }

        //!
        //! Function that creates the keyframe widget of the time line, 
        //! based on a given parameter.
        //!
        //! @param parameter The parameter for which the keyframe widgets
        //! shall be created. 
        //!
        public void CreateFrames(IAnimationParameter parameter)
        {
            if(!m_showTimeLine || parameter == null || parameter.getKeys() == null)
                return;

            foreach (AbstractKey key in parameter.getKeys())
            {
                bool exists = false;
                // check if there is already a key // TODO: smarter search
                foreach (KeyFrame kf in m_keyframeList)
                {
                    if (kf.key.time == key.time)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                    addFrame(key);
            }

            UpdateFrames();
        }

        //!
        //! Updates position of the displayed frames. Will show or hide keyframes according to [start,end]. E.g. called after timeline visible range changed.
        //!
        public void UpdateFrames()
        {
            foreach (GameObject kfGo in m_keyframeObjectList)
            {
                float _time = kfGo.GetComponent<KeyFrame>().key.time;
                if (_time < m_startTime || _time > m_endTime)
                    kfGo.SetActive(false);
                else
                    kfGo.SetActive(true);
                kfGo.GetComponent<RectTransform>().localPosition = new Vector3(mapToTimelinePosition(_time), 0, 0);
            }
        }

        //!
        //! Add a frame representing m_image to the timeline
        //! @param      key    key at which to add the keyframe to the timeline
        //!
        private void addFrame(AbstractKey key)
        {
            float time = key.time;
            GameObject keyframeGO = GameObject.Instantiate<GameObject>(m_keyframePrefab, m_timelineRect, false);
            KeyFrame keyframeComponent = keyframeGO.GetComponent<KeyFrame>();
            keyframeGO.transform.SetAsFirstSibling();
            keyframeComponent.key = key;

            keyframeGO.name = m_keyframeObjectList.Count.ToString();
            m_keyframeObjectList.Add(keyframeGO);
            m_keyframeList.Add(keyframeComponent);

            if (m_startTime <= time && time <= m_endTime)
                keyframeGO.SetActive(true);
            else
                keyframeGO.SetActive(false);

            keyframeComponent.KeyframeDragEndEvent = setTimeFromGlobalPositionX;
            keyframeComponent.KeyframeSelectedEvent = keyframeSelected;

            updateButtonInteractability();
        }

        ////////////////////////
        // KEYFRAME CALLBACKS //
        ////////////////////////

        //!
        //! Callback function called from a keyframe widget to update the key's values.
        //!
        //! @param key The key the keyframe wedget belogs to.
        //! @param x The new x position of the keyframe widget on the timelene. 
        //!
        private void setTimeFromGlobalPositionX(AbstractKey key, float x)
        {
            float _x = m_timelineRect.InverseTransformPoint(new Vector3(x, m_timelineRect.position.y, m_timelineRect.position.z)).x;
            float time = mapToCurrentTime(_x);
            Debug.Log("<color=green>setTimeFromGlobalPositionX</color>");
            m_activeParameter.setKeyTime(key, time);
            clearFrames();
            CreateFrames(m_activeParameter);
            setTime(m_currentTime);
        }

        //!
        //! Callback function called from a keyframe widget when it has been klicked.
        //!
        //! @param keyframe The Uinty GameObject behind the keyframe widget. 
        //!
        private void keyframeSelected(GameObject keyframe){
            if (m_activeKeyframeIndex != -1){
                m_keyframeList[m_activeKeyframeIndex].deSelect();
            }

            if (m_activeKeyframeIndex == m_keyframeObjectList.IndexOf(keyframe)){
                m_keyframeList[m_activeKeyframeIndex].deSelect();
                m_activeKeyframeIndex = -1;
            }else{
                m_activeKeyframeIndex = m_keyframeObjectList.IndexOf(keyframe);
                m_keyframeList[m_activeKeyframeIndex].select();
                if(SET_TIME_TO_MARKED_KEYFRAME){
                    setTime(m_keyframeList[m_activeKeyframeIndex].key.time);
                }
            }
            updateButtonInteractability();
        }

        private void keyframeDeselected()
        {
            m_activeKeyframeIndex = -1;
            updateButtonInteractability();
        }
        
        //!
        //! Function used to add a key.
        //!
        private void AddKey()
        {
            if (m_activeParameter != null)
            {
                Debug.Log("<color=yellow>AddKey</color>");
                UpdateKey(false);
            }
        }

        //!
        //! Function used to remove a key.
        //!
        private void RemoveKey()
        {
            if (m_activeParameter != null)
            {
                Debug.Log("<color=yellow>RemoveKey</color>");
                UpdateKey(true);
                keyframeDeselected();
            }
        }

        //!
        //! Function used to remove all keys.
        //!
        private void RemoveAllKeys()
        {
            if (m_activeParameter != null)
            {
                Debug.Log("<color=yellow>RemoveAllKeys</color>");
                UpdateKey(false, true);
                keyframeDeselected();
            }
        }

        //!
        //! Function used to Update a key (add or remove).
        //!
        private void UpdateKey(bool removeKey, bool removeAll = false)
        {
            if (m_activeParameter is Parameter<bool> boolParam)
            {
                ApplyKeyUpdate(boolParam, removeKey, removeAll);
            }
            if (m_activeParameter is Parameter<int> intParam)
            {
                ApplyKeyUpdate(intParam, removeKey, removeAll);
            }
            if (m_activeParameter is Parameter<float> floatParam)
            {
                ApplyKeyUpdate(floatParam, removeKey, removeAll);
            }
            if (m_activeParameter is Parameter<Vector2> vector2Param)
            {
                ApplyKeyUpdate(vector2Param, removeKey, removeAll);
            }
            else if (m_activeParameter is Parameter<Vector3> vector3Param)
            {
                ApplyKeyUpdate(vector3Param, removeKey, removeAll);
                if ((m_activeParameter as AbstractParameter)?.name == "position")
                {
                    m_animationManager.OnRenewSplineContainer(null);
                }
            }
            if (m_activeParameter is Parameter<Vector4> vector4Param)
            {
                ApplyKeyUpdate(vector4Param, removeKey, removeAll);
            }
            if (m_activeParameter is Parameter<Quaternion> quaternionParam)
            {
                ApplyKeyUpdate(quaternionParam, removeKey, removeAll);
            }
            if (m_activeParameter is Parameter<Color> colorParameter)
            {
                ApplyKeyUpdate(colorParameter, removeKey, removeAll);
            }
            
            updateButtonInteractability();
            (m_activeParameter).InvokeKeyHasChanged();
        }
        
        //!
        //! Function used to apply the key update
        //!
        private void ApplyKeyUpdate<T>(Parameter<T> parameter, bool removeKey = false, bool removeAll = false)
        {
            if (removeKey && !removeAll && m_activeKeyframeIndex > -1)
            {
                int idx = m_activeKeyframeIndex;
                if (idx >= 0){
                    parameter.removeKeyAtIndex(idx);
                    m_activeKeyframeIndex = -1;
                    //update selected object to show the correct values
                    //OnKeyframeUpdated(null, null);
                    m_animationManager.timelineUpdated(m_currentTime);
                }
            }
            else if (!removeKey)
            {
                parameter.setKey();
                //this will be transfered via network?
            }

            if (removeAll)
            {
                parameter.clearKeys();
                m_activeKeyframeIndex = -1;
            }
        
        }
        

        //////////////
        // CONTROLS //
        //////////////

        public GameObject FindClosestBiggerValue(float time)
        {
            var closestBiggerObject = m_keyframeObjectList.Where(obj => obj.GetComponent<KeyFrame>() != null).Select(obj => new
                {
                    gameObject = obj,
                    value = obj.GetComponent<KeyFrame>().key.time 
                }).Where(x => x.value > time).OrderBy(x => x.value).FirstOrDefault();

            return closestBiggerObject?.gameObject;
        }
        
        public GameObject FindClosestSmallerValue(float time)
        {
            var closestSmallerObject = m_keyframeObjectList.Where(obj => obj.GetComponent<KeyFrame>() != null).Select(obj => new
                {
                    gameObject = obj,
                    value = obj.GetComponent<KeyFrame>().key.time 
                }).Where(x => x.value < time).OrderByDescending(x => x.value).FirstOrDefault(); 

            return closestSmallerObject?.gameObject;
        }


        //!
        //! Function selects the next key frame and moves the time line view if needed.
        //!
        private void nextFrame()
        {
            KeyFrame nextKeyFrame = null, activeKeyFrame;
            if (m_activeKeyframeIndex == -1){                               //no keyframe selected
                var go = FindClosestBiggerValue(m_currentTime);
                if (go != null){
                    nextKeyFrame = go.GetComponent<KeyFrame>();
                    m_activeKeyframeIndex = m_keyframeObjectList.IndexOf(nextKeyFrame.gameObject);
                }
            }else if (m_activeKeyframeIndex + 1 < m_keyframeList.Count){    //next keyframe available
                activeKeyFrame = m_keyframeList[m_activeKeyframeIndex];
                nextKeyFrame = m_keyframeList[++m_activeKeyframeIndex];
                
                /*float deltaTime = nextKeyFrame.key.time - activeKeyFrame.key.time;
                if (nextKeyFrame.key.time >= m_endTime){                    //keyframe time out of scope, move timeline
                    EndTime += deltaTime;
                    StartTime += deltaTime;
                    UpdateFrames();
                    setTime(m_currentTime + deltaTime);
                }*/

                activeKeyFrame.deSelect();
            }else if (m_keyframeList.Count > 0){                            //use keyframe that's already selected
                nextKeyFrame = m_keyframeList[m_activeKeyframeIndex];
            }else{                                                          //none available
                return;
            }

            if (nextKeyFrame != null){                                      //next keyframe found (or already selected)
                nextKeyFrame.select();
                setTime(nextKeyFrame.key.time, false);
                if(wouldTimelineTimeBeOutOfScope(nextKeyFrame.key.time)){
                    focusOnCurrentTime();
                }
            }
            updateButtonInteractability();
        }

        //!
        //! Function selects the previous key frame and moves the time line view if needed.
        //!
        private void prevFrame(){
            KeyFrame prevKeyFrame = null, activeKeyFrame;

            if (m_activeKeyframeIndex == -1){                               //no keyframe selected
                var go = FindClosestSmallerValue(m_currentTime);
                if (go != null)
                {
                    prevKeyFrame = go.GetComponent<KeyFrame>();
                    m_activeKeyframeIndex = m_keyframeObjectList.IndexOf(prevKeyFrame.gameObject);
                }
            }else if (m_activeKeyframeIndex - 1 > -1){                      //prev keyframe available
                activeKeyFrame = m_keyframeList[m_activeKeyframeIndex];
                prevKeyFrame = m_keyframeList[--m_activeKeyframeIndex];
                
                /*float deltaTime = activeKeyFrame.key.time - prevKeyFrame.key.time;
                if (prevKeyFrame.key.time <= m_startTime){
                    EndTime -= deltaTime;
                    StartTime -= deltaTime;
                    UpdateFrames();
                    setTime(m_currentTime - deltaTime);
                }*/

                activeKeyFrame.deSelect();
            }else if (m_keyframeList.Count > 0){                           //use keyframe that's already selected
                prevKeyFrame = m_keyframeList[m_activeKeyframeIndex];
            }else{                                                          //none available
                return;
            }

            if (prevKeyFrame != null){                                      //prev keyframe found (or already selected)
                prevKeyFrame.select();
                setTime(prevKeyFrame.key.time, false);
                if(wouldTimelineTimeBeOutOfScope(prevKeyFrame.key.time)){
                    focusOnCurrentTime();
                }
            }
            updateButtonInteractability();
        }

        //!
        //! Function to trigger the play/pause mode.
        //!
        private void play(){
            if (m_isPlaying){
                m_isPlaying = false;
                core.StopCoroutine(playCoroutine());
                UnlockAllAnimatedObjects();
            }else{
                GatherAllAnimatedSceneObjects();
                Debug.Log("<color=blue>Animation. We have "+m_allAnimatedObjects.Count+" animated SceneObjects</color>");
                
                /* uncomment if we only want to allow playing the animation if all can be locked
                if(!AreAllAnimatedObjectsUnlocked()){
                    //if we do this, also ensure while an animation is playing anywhere, do not allow to create animations
                    //Debug.Log("<color=red>Cannot play animation, because "+so.gameObject.name+" is locked from elsewhere</color>");
                    return;
                }
                */
                LockAllAnimatedObjects();

                deselectKeyframe();
                focusOnCurrentTime();
                m_isPlaying = true;
                m_playCoroutine = core.StartCoroutine(playCoroutine());
            }

            updateButtonInteractability();
        }

        #region Animation Locking

        //!
        //! Store all animated scene objects locally, so we do not need to call the get function for locking
        //!
        private void GatherAllAnimatedSceneObjects(){
            m_allAnimatedObjects = core.getManager<SceneManager>().getAllAnimatedSceneObjects();
        }

        private bool AreAllAnimatedObjectsUnlocked(){
            foreach(SceneObject so in m_allAnimatedObjects){
                if(so._lock){
                    return false;
                }
            }
            return true;
        }

        private void LockAllAnimatedObjects(){
            m_animatedSceneObjectsLockCalled = true;
            foreach(SceneObject so in m_allAnimatedObjects){
                so.setObjectPlayedByTimeline(true); //should always be set, because it could be unlocked on the client and then will be manipulated?
                if(!so._lock){
                    so.lockObject(true);
                }
            }
        }

        private void UnlockAllAnimatedObjects(){
            m_animatedSceneObjectsLockCalled = false;
            foreach(SceneObject so in m_allAnimatedObjects){
                so.setObjectPlayedByTimeline(false);
                if(!so._lock){
                    //dont unlock a selected object
                    if(!manager.SelectedObjects.Contains(so))
                        so.lockObject(false);
                }
            }
        }
        #endregion


        //!
        //! Coroutine to update the time and trigger all evaluations in play mode.
        //!
        private IEnumerator playCoroutine()
        {
            if(m_keyframeList != null && m_keyframeList.Count > 0)
                lastKeyFrame = m_keyframeList[^1];
                
            while (m_isPlaying){
                yield return new WaitForSecondsRealtime(Mathf.FloorToInt(1000f / core.settings.framerate) / 1000f);
                
                //pause while touching the timeline and manipulating it!
                if(m_isSelected){
                    updateButtonBgColor(m_playButton.GetComponentInChildren<Image>(), Color.yellow); //first child will be BG
                    while(m_isSelected && m_isPlaying){
                        yield return null;
                    }
                    if(!m_isPlaying){
                        yield break;
                    }
                    updateButtonBgColor(m_playButton.GetComponentInChildren<Image>(), Color.green); //first child will be BG
                    continue;
                }
                
                focusOnCurrentTime();
                setTime(m_currentTime + (1f / m_framerate));

                //if bigger than last keyframe, stop playback (if no keyframes are there, play endless)
                if(STOP_ON_LAST_KEYFRAME && lastKeyFrame && m_currentTime > lastKeyFrame.key.time){
                    play();
                    setTime(lastKeyFrame.key.time);
                }else if(LOOP_IN_CURRENT_VIEW && m_currentTime >= EndTime){
                    setTime(StartTime);
                }
            }
        }

        //!
        //! if current time (red line) is out of scope, move timeline so its on the left side. During m_isPlaying, 'fix' it on 40% on timeline
        //!
        private void focusOnCurrentTime(){
            float distance = EndTime - StartTime;
            if(m_isPlaying){
                //if last keyframe is visible on timeline (below 90%) dont move the timeline as it plays
                if(STOP_ON_LAST_KEYFRAME && lastKeyFrame && lastKeyFrame.key.time < (EndTime-distance*0.1f))
                    return;

                if(LOOP_IN_CURRENT_VIEW)
                    return;

                //move the timeline so the red-line stays at the same position (40%) for a more convenient way to watch
                if(m_currentTime > StartTime + distance*0.4f){
                    StartTime = m_currentTime - distance*0.4f;
                    EndTime = StartTime+distance;
                }
                UpdateFrames();
            }else{
                if(m_currentTime < StartTime){
                    StartTime = m_currentTime;
                    EndTime = StartTime+distance;
                    setTime(m_currentTime, false);
                    //update
                    UpdateFrames();
                }else if(m_currentTime > (EndTime-distance*0.05f)){
                    StartTime = m_currentTime;
                    EndTime = StartTime+distance;
                    setTime(m_currentTime, false);
                    //update
                    UpdateFrames();
                }
            }
        }

        //!
        //! deselect a keyframe (e.g. if we move the current time by hand, delesect a possible selected keyframe)
        //!
        private void deselectKeyframe(){
            //if a keyframe is selected, deselect it (and check if we select another one - by being next to it: snap!)
            if(m_activeKeyframeIndex != -1){
                m_keyframeList[m_activeKeyframeIndex].deSelect();
                m_activeKeyframeIndex = -1;
            }
        }


        ///////////
        // INPUT //
        ///////////
        #region Timeline Input

        //!
        //! Function that is called when the input manager registers a pointer down event
        //! check whether we hit the timeline, do nothing more
        //!
        //! @param sender A reference to the input manager.
        //! @param point The point in screen space the pointer down event happened.
        //!
        private void OnPointerDown(object sender, Vector2 point)
        {
            if (GetGameObjectAtUIRaycast(point) != m_timeLine)
                return;

            m_isSelected = true;
            m_posDragBuffer = point;
            m_initalTouchPos = point;
            m_initialTouchTime = Time.time;

            //InputManager.FingerDown is executed after this, so I cannot differentiate between a touch and a click
            //but i want to move the timeline to the *click* (if not key is pressed)
            //and only move the timeline to the *touch* on FingerUp!
            //thats why this additional check is below
            if(m_inputManager.IsInputTouch() || m_inputManager.getKey(53)){
                //ignore
                //the touch is received after this, so we cannot correctly ignore it here...
                //Debug.Log("IGNORE <color=cyan>TIMELINE::OnPointerDown</color>");
            }else{
                //set time instantly on click with no special action
                UpdateTime(m_timelineRect.InverseTransformPoint(point).x);
            }

        }

        //!
        //! Function that is called when the input manager registers a pointer end event
        //!
        //! @param sender A reference to the input manager.
        //! @param point The point in screen space the pointer up event happened.
        //!
        private void OnPointerEnd(object sender, Vector2 point){
            //if we did nothing special (drag, zoom), assume that we want to set the time here
            if(m_isSelected){
                if(!m_didSpecialAction){
                    UpdateTime(m_timelineRect.InverseTransformPoint(point).x);
                    deselectKeyframe();
                    updateButtonInteractability();

                    if(m_animatedSceneObjectsLockCalled){
                        UnlockAllAnimatedObjects();
                    }
                }
            }
            m_isSelected = false;
            m_didSpecialAction = false;
        }

        private void UpdateTime(float xValue){
            float time = mapToCurrentTime(xValue);
            setTime(time);
        }


        //!
        //! Function that is called when the input manager registers on input move event
        //!
        //! @param sender A reference to the input manager.
        //! @param point The point in screen space the on move event happened.
        //!
        private void OnMove(object sender, Vector2 point)
        {
            if (!m_isSelected || m_isMiddleClickDrag)
                return;

            //additional add zoom and drag
            if (m_inputManager.getKey(53))  // 53 -> left ALT key
            {
                if (m_inputManager.getKey(55)) // pinch  55 -> left CTRL key
                {
                    Vector2 delta = point-m_posDragBuffer;
                    ZoomTimeline(m_initalTouchPos, delta.x);
                }
                else // move
                {
                    Vector2 delta = point-m_posDragBuffer;
                    DragTimeline(delta);

                }
            }else{ // move time cursor
                deselectKeyframe();
                //we should/need to lock animated sceneobject if we are scrubbing the timeline manually
                if(!m_animatedSceneObjectsLockCalled && Time.time - m_initialTouchTime > 0.2f){
                    GatherAllAnimatedSceneObjects();
                    LockAllAnimatedObjects();
                    m_animatedSceneObjectsLockCalled = true;
                }
                setTime(mapToCurrentTime(m_timelineRect.InverseTransformPoint(point).x));
            }

            m_posDragBuffer = point;
        }

        //!
        //! Function that is called for a specific event from the input manager we map on dragging the timeline (middle click event)
        //!
        //! @param sender A reference to the input manager.
        //! @param point The point in screen space the on drag event happened.
        //!
        private void OnMiddleClickPress(object sender, Vector2 point){
            if (!m_isSelected){
                if(!m_isMiddleClickDrag && GetGameObjectAtUIRaycast(point) == m_timeLine){
                    m_posDragBuffer = point;
                    m_isMiddleClickDrag = true;
                    m_isSelected = true;
                }else
                    return;
            }
        }

        //!
        //! Function that is called for a specific event from the input manager we map on dragging the timeline (middle click press+hold and move event)
        //!
        //! @param sender A reference to the input manager.
        //! @param point The point in screen space the on drag event happened.
        //!
        private void OnMiddleClickHold(object sender, Vector2 point)
        {
            if (!m_isSelected)
                return;

            Vector2 delta = point-m_posDragBuffer;
            DragTimeline(delta);
            m_posDragBuffer = point;
        }

        //!
        //! Function that is called for a specific event from the input manager we map on quit dragging the timeline (middle click release event)
        //!
        //! @param sender A reference to the input manager.
        //! @param point The point in screen space the on drag event happened.
        //!
        private void OnMiddleClickRelease(object sender, Vector2 point){
            if (m_isSelected)
                m_isSelected = false;

            m_isMiddleClickDrag = false;
            m_didSpecialAction = false; //because not OnPointerEnd is thrown
        }

        //////////////////
        // Touch Inputs //
        //////////////////

        //!
        //! Function that is called when the input manager registers an on pinch event
        //!
        //! @param sender A reference to the input manager.
        //! @param delta The distance between the two pinch input pointers.
        //!
        private void OnPinchDetail(object sender, InputManager.DetailedEventArgs detailedArgs)
        {
            bool simulateRelease = false;
            if (!m_isSelected){
                //if we are not on touch (e.g. scroll wheel) select this if point is here
                if(!m_inputManager.IsInputTouch() && GetGameObjectAtUIRaycast(detailedArgs.point) == m_timeLine){
                    //act like its selected
                    //if we zoom via scroll wheel, we do not have an "end" event and need a dummy click to reset the 'special action'
                    //therefore we'll fake an end after every call right now and right here!
                    #if (!UNITY_IOS && !UNITY_ANDROID) || UNITY_EDITOR
                    simulateRelease = true;
                    #endif
                }else{
                    //Debug.Log("ignore <color=cyan>TIMELINE::OnPinchDetail</color> due to !m_isSelected");
                    return;
                }
            }

            //Debug.Log("OnPinchDetail "+detailedArgs.delta);

            if(Time.time - m_timeLastDragged < 0.125f)
                return;
            

            ZoomTimeline(detailedArgs.point, detailedArgs.delta.x);

            if(simulateRelease){
                OnMiddleClickRelease(null, Vector2.zero);
            }
        }

        //!
        //! Function that is called when the input manager registers a two finger drag event
        //!
        //! @param sender A reference to the input manager.
        //! @param deltaPos The delta of touch0 touch1 positions
        //!
        private void OnTwoFingerDrag(object sender, Vector2 deltaPos)
        {
            if (!m_isSelected)
                return;

            //Debug.Log("OnTwoFingerDrag");

            if(Time.time - m_timeLastZoomed < 0.125f)
                return;

            DragTimeline(deltaPos);
        }


        private void ZoomTimeline(Vector2 point, float delta){
            //Debug.Log(">>ZoomTimeline dpi@"+Screen.dpi);
            m_timeLastZoomed = Time.time;
            m_didSpecialAction = true;

            //correct delta for touch and big screens (touch should be less accurate)
            delta *= (m_inputManager.IsInputTouch() ? 10f/Screen.dpi : 50f/Screen.dpi);
            //Debug.Log(">>> delta@"+delta);

            //multiply delta accordingly to its timeline's time size
            delta *= (EndTime-StartTime)/2f;// /m_framerate;

            //use point to offset the zoom on where we are on the timeline (would be timeOnTimeline if *= m_framerate)
            float valueOnTimeline = mapToCurrentTime(m_timelineRect.InverseTransformPoint(point).x);
            //float timeOnTimeline = valueOnTimeline * m_framerate;
            
            //minimum delta of 1/Framerate
            delta = delta > 0 ? Mathf.Max(delta * Time.deltaTime, 1f/m_framerate) : Mathf.Min(delta * Time.deltaTime, -1f/m_framerate);

            //maximum delta of ... 1? (means +-30 per Frame)
            delta = Mathf.Clamp(delta, -m_framerate, m_framerate);

            //save if we need to reset them
            float startTimeWas = StartTime;
            float endTimeWas = EndTime;
            
            //check where on the timeline our center of touches is and offset the delta accordingly
            //e.g.  on the most left (startTimeDragInit + delta * 0f) and (endTimeDragInit - delta * 1f)
            //          == we dont change the starttime, but the endtime (and vice versa below)
            //      on the center (startTimeDragInit + delta * 0.5f) and (endTimeDragInit - delta * 0.5f)

            //gather start/end offset via zoom-pos
            float startEndOffsetRatio = (float)(valueOnTimeline-StartTime)/(EndTime-StartTime);

            //increase or decrease the startTime accordingly to the point where we are zooming at
            StartTime   += delta * startEndOffsetRatio;
            //never go below the frame -10 at the start
            if(StartTime <= (float)TIMELINE_START_MINIMUM/m_framerate){
                StartTime = (float)TIMELINE_START_MINIMUM/m_framerate;
                EndTime -= delta;
            }else     
                EndTime -= delta * (1f-startEndOffsetRatio);

            //act like clamping, dont change nor update
            if(StartTime >= EndTime){
                StartTime = startTimeWas;
                EndTime = endTimeWas;
                return;
            }

            // update position of time (could be out of timeline scope!)
            updatePositionOnTimeline();
            
            //update
            UpdateFrames();
        }

        private void DragTimeline(Vector2 deltaPos){
            m_timeLastDragged = Time.time;
            m_didSpecialAction = true;

            //save if we need to reset them
            float startTimeWas = StartTime;
            float endTimeWas = EndTime;

            if(m_invertScrollAndDrag){
                StartTime   += deltaPos.x * Time.deltaTime;
                EndTime     += deltaPos.x * Time.deltaTime;
            }else{
                StartTime   -= deltaPos.x * Time.deltaTime;
                EndTime     -= deltaPos.x * Time.deltaTime;
            }


            //act like clamping, dont change nor update
            if(StartTime <= (float)TIMELINE_START_MINIMUM/m_framerate){
                StartTime = startTimeWas;
                EndTime = endTimeWas;
                return;
            }

            // update position of time (could be out of timeline scope!)
            updatePositionOnTimeline();
            
            //update
            UpdateFrames();
        }
        #endregion

        //////////////////////
        // Helper functions //
        //////////////////////

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameObject GetGameObjectAtUIRaycast(Vector2 point) //TODO: exclude into specific static holder for these kind of functions
        {
            //Set up the new Pointer Event
            PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
            //Set the Pointer Event Position to that of the game object
            pointerEventData.position = point;

            //Create a list of Raycast Results
            List<RaycastResult> results = new List<RaycastResult>();

            //Raycast using the Graphics Raycaster and mouse click position
            EventSystem.current.RaycastAll(pointerEventData, results);

            return results.Count > 0 ? results[0].gameObject : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static private float _map(float x, float a1, float b1, float a2, float b2)
        {
            return (x * (b2 - a2) - a1 * b2 + a2 * b1) / (b1 - a1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float mapToTimelinePosition(float x)
        {
            return _map(x, m_startTime, m_endTime, -m_timelineRect.sizeDelta.x * 0.5f, m_timelineRect.sizeDelta.x * 0.5f);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float mapToCurrentTime(float x)
        {
            return _map(x, -m_timelineRect.sizeDelta.x * 0.5f, m_timelineRect.sizeDelta.x * 0.5f, m_startTime, m_endTime);
        }

    }
}
