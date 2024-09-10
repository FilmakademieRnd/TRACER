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
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace tracer
{
    public class TimelineModule : UIManagerModule
    {
        public int m_framerate = 30;
        public bool m_isPlaying = false;

        
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
        //! The list in which all GUI keyframes are registered.
        //!
        private List<GameObject> m_keyframeList;
        //!
        //! The index of the last active keyframe;
        //!
        private int m_activeKeyframeIndex = 0;
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
        //! A reference the current active parameter the key are displayed by the timeline.
        //!
        private IAnimationParameter m_activeParameter = null;
        //!
        //! The visible start time of the timeline.
        //!
        private float m_startTime = 0;
        
        //!
        //! The UI button to add a key.
        //!
        private Button _addKeyButton;
        //!
        //! The UI button to remove a key.
        //!
        private Button _removeKeyButton;
        //!
        //! The UI button to remove all keys.
        //!
        private Button _removeAnimationButton;
        //!
        //! Controll panel for addin and removing keys.
        //!
        private GameObject _addRemoveKeyPanel;
        //!
        //! Transform of the KeyCanvas.
        //!
        private Transform _keyCanvasTrans;
        
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
        //! Initial start time at drag begin.
        //!
        private float startTimeDragInit = 0;
        //!
        //! Initial end time at drag begin.
        //!
        private float endTimeDragInit = 1;
        //!
        //! Initial active time at drag begin.
        //!
        private float timeDragStart = 0;
        //!
        //! Initial pinc distance. 
        //!
        private float pinchInitDistance = 0;

        //!
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public TimelineModule(string name, Manager manager) : base(name, manager)
        {
            //load = false;
        }

        //!
        //! Init Function
        //!
        protected override void Init(object sender, EventArgs e)
        {
            m_keyframeList = new List<GameObject>();
            m_uiElements = new List<GameObject>();

            m_canvas = Resources.Load("Prefabs/TimelineCanvas") as GameObject;
            m_keyframePrefab = Resources.Load("Prefabs/KeyFrameTemplate") as GameObject;
            _addRemoveKeyPanel = Resources.Load<GameObject>("Prefabs/AddRemoveKeyPanel");

            MenuButton hideTimelineButton = new MenuButton("Timeline", toggleTimeLine, new List<UIManager.Roles>() { UIManager.Roles.SET });
            //hideTimelineButton.setIcon("Images/button_timeline");
            manager.addButton(hideTimelineButton);

            m_inputManager = core.getManager<InputManager>();
            m_animationManager = core.getManager<AnimationManager>();
            manager.UI2DCreated += On2DUIReady;
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
                StopAnimGen();
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

            m_startFrameDisplay.text = Mathf.RoundToInt(m_startTime * m_framerate).ToString();
            m_endFrameDisplay.text = Mathf.RoundToInt(m_endTime * m_framerate).ToString();

            m_playButton.onClick.AddListener(play);
            m_prevButton.onClick.AddListener(prevFrame);
            m_nextButton.onClick.AddListener(nextFrame);

            m_inputManager.inputPressStartedUI += OnBeginDrag;
            m_inputManager.inputPressEnd += OnPointerEnd;
            m_inputManager.inputPressPerformedUI += OnPointerDown;
            m_inputManager.inputMove += OnDrag;
            m_inputManager.twoDragEvent += OnTwoFingerDrag;
            m_inputManager.pinchEvent += OnPinch;
            manager.selectionChanged += OnSelectionChanged;

            if (manager.SelectedObjects.Count > 0)
            {
                if (m_activeParameter == null)
                {
                    m_activeParameter = manager.SelectedObjects[0].parameterList[0] as IAnimationParameter;
                    m_activeParameter.keyHasChanged += OnKeyframeUpdated;
                }
                CreateFrames(m_activeParameter);
            }

            setTime(m_animationManager.time);
        }

        //!
        //! Function to destroy all created UI elements of a timeline.
        //!
        private void destroyTimeline()
        {
            clearFrames();
            clearUI();
            StopAnimGen();

            m_inputManager.inputPressStartedUI -= OnBeginDrag;
            m_inputManager.inputPressEnd -= OnPointerEnd;
            m_inputManager.inputPressPerformedUI -= OnPointerDown;
            m_inputManager.inputMove -= OnDrag;
            m_inputManager.twoDragEvent -= OnTwoFingerDrag;
            m_inputManager.pinchEvent -= OnPinch;
            manager.selectionChanged -= OnSelectionChanged;
        }

        //!
        //! Destroys all keyframe game objects within timeline.
        //!
        private void clearFrames()
        {
            foreach (GameObject g in m_keyframeList)
                GameObject.DestroyImmediate(g);
            m_keyframeList.Clear();
        }

        //!
        //! Destroys all timeline UI game objects exept keyframes.
        //!
        private void clearUI()
        {
            foreach (GameObject uiElement in m_uiElements)
                UnityEngine.Object.DestroyImmediate(uiElement);
            m_uiElements.Clear();
        }

        //!
        //! set the current time (of the animation) in the timeline
        //! @param      time        current time at the red line of the timeline
        //!
        public void setTime(float time)
        {
            if (time > m_endTime)
                m_currentTime = m_endTime;
            else if
                (time < m_startTime)
                m_currentTime = m_startTime;
            else
                m_currentTime = time;

            m_redLine.localPosition = new Vector3(mapToTimelinePosition(m_currentTime), 0, 0);
            m_currentFrameDisplay.text = Mathf.RoundToInt(m_currentTime * m_framerate).ToString();
            m_animationManager.timelineUpdated(time);
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
            if (sceneObjects.Count < 1)
            {
                clearFrames();
                if (m_activeParameter != null)
                {
                    m_activeParameter.keyHasChanged -= OnKeyframeUpdated;
                    m_activeParameter = null;
                }

                if (m_snapSelect)
                    m_snapSelect.parameterChanged -= OnParameterChanged;

                if (_keyCanvasTrans != null)
                {
                    StopAnimGen();
                }
            }

            if (sceneObjects.Count > 0)
            {
                StartAnimGen();
            }
        }

        private void StopAnimGen()
        {
            if (_keyCanvasTrans != null)
            {
                GameObject.DestroyImmediate(_keyCanvasTrans.gameObject);
                m_animationManager.OnStopAnimaGeneration(null);
                _addKeyButton.onClick.RemoveAllListeners();
                _removeKeyButton.onClick.RemoveAllListeners();
                _removeAnimationButton.onClick.RemoveAllListeners();

            }
        }

        private void StartAnimGen()
        {
            if (!_keyCanvasTrans)
            {
                Transform ui2D = manager.getModule<UICreator2DModule>().UI2DCanvas;
                _keyCanvasTrans = SceneObject.Instantiate(_addRemoveKeyPanel.transform, ui2D);

                m_animationManager.OnStartAnimaGeneration(null);
                _addKeyButton = _keyCanvasTrans.GetChild(1).GetComponent<Button>();
                _addKeyButton.onClick.AddListener(CallAddKeyEvent);
                _removeKeyButton = _keyCanvasTrans.GetChild(2).GetComponent<Button>();
                _removeKeyButton.onClick.AddListener(CallRemoveKeyEvent);
                _removeAnimationButton = _keyCanvasTrans.GetChild(3).GetComponent<Button>();
                _removeAnimationButton.onClick.AddListener(CallRemoveAnimationEvent);
            }
        }

        private void CallAddKeyEvent()
        {
            m_animationManager.OnAddKey(null);
        }

        private void CallRemoveKeyEvent()
        {
            m_animationManager.OnRemoveKey(null);
        }

        private void CallRemoveAnimationEvent()
        {
            m_animationManager.OnRemoveAnimation(null);
        }


        //!
        //! Function called a keyframe 
        //! Will remove all keyframes from timeline UI is new selection is empty.
        //!
        //! @param o The UI manager.
        //! @param sceneObjects The list containing the selected objects. 
        //!
        private void OnKeyframeUpdated(object o, EventArgs e)
        {
            clearFrames();
            CreateFrames((IAnimationParameter) o);
        }

        //!
        //! Updates the displayed keys according to the given scene objecs and their 
        //! containt parameters with it's keys. Will add/remove frames if neccessary.
        //! @param sceneObjects Scene object for which to display the timeline.
        //!
        private void On2DUIReady(object o, UIBehaviour ui)
        {

            m_snapSelect = (SnapSelect) ui;
            m_snapSelect.parameterChanged -= OnParameterChanged;
            m_snapSelect.parameterChanged += OnParameterChanged;

            clearFrames();
            
            if (manager.SelectedObjects.Count > 0)
            {
                if (m_activeParameter != null)
                    m_activeParameter.keyHasChanged -= OnKeyframeUpdated;
                m_activeParameter = manager.SelectedObjects[0].parameterList[0] as IAnimationParameter;
                m_activeParameter.keyHasChanged += OnKeyframeUpdated;
                if (m_showTimeLine)
                    CreateFrames(m_activeParameter);
            }

            UpdateFrames();
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
            clearFrames();
            if (m_activeParameter != null)
                m_activeParameter.keyHasChanged -= OnKeyframeUpdated;
            m_activeParameter = manager.SelectedObjects[0].parameterList[idx] as IAnimationParameter;
            m_activeParameter.keyHasChanged += OnKeyframeUpdated;
            CreateFrames(m_activeParameter);
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
            foreach (AbstractKey key in parameter.getKeys())
            {
                bool exists = false;
                // check if there is already a key // TODO: smarter search
                foreach (GameObject img in m_keyframeList)
                {
                    if (img.GetComponent<KeyFrame>().key.time == key.time)
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
            foreach (GameObject img in m_keyframeList)
            {
                float _time = img.GetComponent<KeyFrame>().key.time;
                if (_time < m_startTime || _time > m_endTime)
                    img.SetActive(false);
                else
                    img.SetActive(true);
                img.GetComponent<RectTransform>().localPosition = new Vector3(mapToTimelinePosition(_time), 0, 0);
            }
        }

        //!
        //! Add a frame representing m_image to the timeline
        //! @param      key    key at which to add the keyframe to the timeline
        //!
        private void addFrame(AbstractKey key)
        {
            float time = key.time;
            GameObject keyFrame = GameObject.Instantiate<GameObject>(m_keyframePrefab, m_timelineRect, false);
            KeyFrame keyframeComponent = keyFrame.GetComponent<KeyFrame>();
            keyFrame.transform.SetAsFirstSibling();
            keyframeComponent.key = key;

            keyFrame.name = m_keyframeList.Count.ToString();
            m_keyframeList.Add(keyFrame);

            if (m_startTime <= time && time <= m_endTime)
                keyFrame.SetActive(true);
            else
                keyFrame.SetActive(false);

            keyframeComponent.Callback = setTimeFromGlobalPositionX;
            keyframeComponent.Callback1 = keyframeSelected;
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
        private void keyframeSelected(GameObject keyframe)
        {
            m_keyframeList[m_activeKeyframeIndex].GetComponent<KeyFrame>().deSelect();
            m_activeKeyframeIndex = m_keyframeList.IndexOf(keyframe);
            m_keyframeList[m_activeKeyframeIndex].GetComponent<KeyFrame>().select();
        }

        //////////////
        // CONTROLS //
        //////////////

        //!
        //! Function selects the next key frame and moves the time line view if needed.
        //!
        private void nextFrame()
        {
            KeyFrame nextKeyFrame, activeKeyFrame;

            if (m_activeKeyframeIndex + 1 < m_keyframeList.Count)
            {
                activeKeyFrame = m_keyframeList[m_activeKeyframeIndex].GetComponent<KeyFrame>();
                nextKeyFrame = m_keyframeList[++m_activeKeyframeIndex].GetComponent<KeyFrame>();
                float deltaTime = nextKeyFrame.key.time - activeKeyFrame.key.time;

                if (nextKeyFrame.key.time >= m_endTime)
                {
                    EndTime += deltaTime;
                    StartTime += deltaTime;
                    UpdateFrames();
                    setTime(m_currentTime + deltaTime);
                }
                
                activeKeyFrame.deSelect();
            }
            else if (m_keyframeList.Count > 0)
                nextKeyFrame = m_keyframeList[m_activeKeyframeIndex].GetComponent<KeyFrame>();
            else
                return;

            nextKeyFrame.select();
            setTime(nextKeyFrame.key.time);
        }

        //!
        //! Function selects the previous key frame and moves the time line view if needed.
        //!
        private void prevFrame()
        {
            KeyFrame prevKeyFrame, activeKeyFrame;

            if (m_activeKeyframeIndex - 1 > -1)
            {
                activeKeyFrame = m_keyframeList[m_activeKeyframeIndex].GetComponent<KeyFrame>();
                prevKeyFrame = m_keyframeList[--m_activeKeyframeIndex].GetComponent<KeyFrame>();
                float deltaTime = activeKeyFrame.key.time - prevKeyFrame.key.time;

                if (prevKeyFrame.key.time <= m_startTime)
                {
                    EndTime -= deltaTime;
                    StartTime -= deltaTime;
                    UpdateFrames();
                    setTime(m_currentTime - deltaTime);
                }

                activeKeyFrame.deSelect();
            }
            else if (m_keyframeList.Count > 0)
                prevKeyFrame = m_keyframeList[m_activeKeyframeIndex].GetComponent<KeyFrame>();
            else
                return;

            prevKeyFrame.select();
            setTime(prevKeyFrame.key.time);
        }

        //!
        //! Function to trigger the play/pause mode.
        //!
        private void play()
        {
            if (m_isPlaying)
            {
                m_isPlaying = false;
                core.StopCoroutine(playCoroutine());
            }
            else
            {
                m_isPlaying = true;
                m_playCoroutine = core.StartCoroutine(playCoroutine());
            }
        }


        //!
        //! Coroutine to update the time and trigger all evaluations in play mode.
        //!
        private IEnumerator playCoroutine()
        {
            while (m_isPlaying)
            {
                yield return new WaitForSecondsRealtime(Mathf.FloorToInt(1000f / core.settings.framerate) / 1000f);
                setTime(m_currentTime + (1f / m_framerate));
            }
        }



        ///////////
        // INPUT //
        ///////////

        //!
        //! Function that is called when the input manager registers a pointer down event
        //!
        //! @param sender A reference to the input manager.
        //! @param point The point in screen space the pointer down event happened.
        //!
        private void OnPointerDown(object sender, Vector2 point)
        {
            if (Raycast(point) != m_timeLine)
                return;

            m_isSelected = true;
            float time = mapToCurrentTime(m_timelineRect.InverseTransformPoint(point).x);
            setTime(time);
        }

        //!
        //! Function that is called when the input manager registers a pointer end event
        //!
        //! @param sender A reference to the input manager.
        //! @param point The point in screen space the pointer up event happened.
        //!
        private void OnPointerEnd(object sender, Vector2 point)
        {
            m_isSelected = false;
        }

        //!
        //! Function that is called when the input manager registers a begin drag event
        //!
        //! @param sender A reference to the input manager.
        //! @param point The point in screen space the bigin drag event happened.
        //!
        private void OnBeginDrag(object sender, Vector2 point)
        {
            startTimeDragInit = m_startTime;
            endTimeDragInit = m_endTime;
            timeDragStart = mapToCurrentTime(m_timelineRect.InverseTransformPoint(point).x);

            pinchInitDistance = point.x;
        }

        //!
        //! Function that is called when the input manager registers an on drag event
        //!
        //! @param sender A reference to the input manager.
        //! @param point The point in screen space the on drag event happened.
        //!
        private void OnDrag(object sender, Vector2 point)
        {
            if (!m_isSelected)
                return;

            if (m_inputManager.getKey(53))  // 53 -> left ALT key
            {
                if (m_inputManager.getKey(55)) // pinch  55 -> left CTRL key
                {
                    // normalized distance
                    float pinchFactor = 1f + (point.x - pinchInitDistance) / Screen.width * 2f;
                    float widthPrev = endTimeDragInit - startTimeDragInit;
                    float widthDeltaHalf = (widthPrev * pinchFactor - widthPrev) * 0.5f;
                    StartTime = startTimeDragInit + widthDeltaHalf;
                    EndTime = endTimeDragInit - widthDeltaHalf;
                    setTime(timeDragStart);
                }
                else // move
                {
                    float timeOffset = timeDragStart - _map(m_timelineRect.InverseTransformPoint(point).x, -m_timelineRect.sizeDelta.x * 0.5f, m_timelineRect.sizeDelta.x * 0.5f, startTimeDragInit, endTimeDragInit);
                    StartTime = startTimeDragInit + timeOffset;
                    EndTime = endTimeDragInit + timeOffset;
                    setTime(timeDragStart + timeOffset);
                }
                UpdateFrames();
            }
            else // move time cursor
                setTime(mapToCurrentTime(m_timelineRect.InverseTransformPoint(point).x));
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
        private void OnPinch(object sender, float delta)
        {
            if (!m_isSelected)
                return;

            // normalized distance
            float pinchFactor = 1f + (delta) / Screen.width * 2f;
            float widthPrev = endTimeDragInit - startTimeDragInit;
            float widthDeltaHalf = (widthPrev * pinchFactor - widthPrev) * 0.5f;
            StartTime = startTimeDragInit + widthDeltaHalf;
            EndTime = endTimeDragInit - widthDeltaHalf;
            setTime(timeDragStart);

            UpdateFrames();
        }

        //!
        //! Function that is called when the input manager registers a two finger drag event
        //!
        //! @param sender A reference to the input manager.
        //! @param The point in screen space the two finger drag event happened.
        //!
        private void OnTwoFingerDrag(object sender, Vector2 point)
        {
            if (!m_isSelected)
                return;

            // normalized distance
            float pinchFactor = 1f + (point.x) / Screen.width * 2f;
            float widthPrev = endTimeDragInit - startTimeDragInit;
            float widthDeltaHalf = (widthPrev * pinchFactor - widthPrev) * 0.5f;
            StartTime = startTimeDragInit + widthDeltaHalf;
            EndTime = endTimeDragInit - widthDeltaHalf;
            setTime(timeDragStart);

            UpdateFrames();
        }

        //////////////////////
        // Helper functions //
        //////////////////////

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private GameObject Raycast(Vector2 point)
        {
            //Set up the new Pointer Event
            PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
            //Set the Pointer Event Position to that of the game object
            pointerEventData.position = point;

            //Create a list of Raycast Results
            List<RaycastResult> results = new List<RaycastResult>();

            //Raycast using the Graphics Raycaster and mouse click position
            EventSystem.current.RaycastAll(pointerEventData, results);

            return results[0].gameObject;
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
