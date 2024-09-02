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
            }
            else
            {
                m_showTimeLine = true;
                createTimeline();  
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
            m_animationManager.keyframeUpdate += OnKeyframeUpdated;
            manager.selectionChanged += OnSelectionChanged;

            if (manager.SelectedObjects.Count > 0)
            {
                if (m_activeParameter == null)
                    m_activeParameter = manager.SelectedObjects[0].parameterList[0] as IAnimationParameter;
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

            m_inputManager.inputPressStartedUI -= OnBeginDrag;
            m_inputManager.inputPressEnd -= OnPointerEnd;
            m_inputManager.inputPressPerformedUI -= OnPointerDown;
            m_inputManager.inputMove -= OnDrag;
            m_inputManager.twoDragEvent -= OnTwoFingerDrag;
            m_inputManager.pinchEvent -= OnPinch;
            m_animationManager.keyframeUpdate -= OnKeyframeUpdated;
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

        private void OnSelectionChanged(object o, List<SceneObject> sceneObjects)
        {
            if (sceneObjects.Count < 1)
                clearFrames();
        }

        private void OnKeyframeUpdated(object o, IAnimationParameter parameter)
        {
            if (parameter == m_activeParameter)
            {
                clearFrames();
                CreateFrames(parameter);
            }
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
                m_activeParameter = manager.SelectedObjects[0].parameterList[0] as IAnimationParameter;
                if (m_showTimeLine)
                    CreateFrames(m_activeParameter);
            }

            UpdateFrames();
        }

        private void OnParameterChanged(object o, int idx)
        {
            clearFrames();

            m_activeParameter = manager.SelectedObjects[0].parameterList[idx] as IAnimationParameter;
            CreateFrames(m_activeParameter);
        }

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
        //! Add a frame representing image to the timeline
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
        }

        //////////////
        // CONTROLS //
        //////////////
        private void nextFrame()
        {
            if (m_activeKeyframeIndex + 1 < m_keyframeList.Count)
            {
                m_keyframeList[m_activeKeyframeIndex].GetComponent<KeyFrame>().deSelect();
                KeyFrame keyFrame = m_keyframeList[++m_activeKeyframeIndex].GetComponent<KeyFrame>();
                keyFrame.select();
                setTime(keyFrame.key.time);
            }
        }

        private void prevFrame()
        {
            if (m_activeKeyframeIndex - 1 > -1)
            {
                m_keyframeList[m_activeKeyframeIndex].GetComponent<KeyFrame>().deSelect();
                KeyFrame keyFrame = m_keyframeList[--m_activeKeyframeIndex].GetComponent<KeyFrame>();
                keyFrame.select();
                setTime(keyFrame.key.time);
            }
        }

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

        private void OnPointerDown(object sender, Vector2 point)
        {
            if (Raycast(point) != m_timeLine)
                return;

            m_isSelected = true;
            float time = mapToCurrentTime(m_timelineRect.InverseTransformPoint(point).x);
            setTime(time);
        }

        private void OnPointerEnd(object sender, Vector2 point)
        {
            m_isSelected = false;
        }

        private void OnBeginDrag(object sender, Vector2 point)
        {
            startTimeDragInit = m_startTime;
            endTimeDragInit = m_endTime;
            timeDragStart = mapToCurrentTime(m_timelineRect.InverseTransformPoint(point).x);

            pinchInitDistance = point.x;
        }

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

        // Touch Inputs //
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void setTimeFromGlobalPositionX(AbstractKey key, float x)
        {
            float _x = m_timelineRect.InverseTransformPoint(new Vector3(x, m_timelineRect.position.y, m_timelineRect.position.z)).x;
            float time = mapToCurrentTime(_x);
            m_activeParameter.setKeyTime(key, time);
            setTime(m_currentTime);
        }

    }
}
