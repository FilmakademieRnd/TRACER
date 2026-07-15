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

//! @file "OnScreenNavigationModule.cs"
//! @brief Enables on-screen sticks for easy cam/scene navigation
//! @author Thomas "Kruegbert" Krüger
//! @version 0
//! @date 15.07.2026


using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace tracer{
        
    //!
    //! We could also expand this module to utilize the sticks from ControllerModule,
    //! so we check if we are in Viewing, Manipulation, etc
    //! > but right now, we only show these navigation twin sticks
    //!

    public class OnScreenNavigationModule : UIManagerModule{

        //!
        //! is the measure ui active or not
        //!
        private bool isActive = false;
        
        private InputManager _inputManager;
        
        //**** UI Configuration (Prozente 0.0 - 1.0)
        //0.2 = 20% der Bildschirmhöhe
        private float stickBasePercent = 0.4f;  
        private float stickKnobPercent = 0.15f; 
        //0.05 = 5% Abstand vom Rand
        private float sideOffsetPercent = 0.2f; 
        private float bottomOffsetPercent = 0.2f;
        
        //**** Visuals
        private Sprite circleSprite;
        private Color idleColor = new Color(1f, 1f, 1f, 0.3f);
        private Color activeColor = new Color(1f, 1f, 1f, 0.8f);

        //**** Behaviour
        private bool dynamicOrigin = true; // If true, the stick base snaps to where you touch. If false, you must touch the base directly.
        private float movementSpeed = 2f;
        private float rotationSpeed = 60f;

        // Internal State
        private Transform _mainCamera;
        private Canvas _navCanvas;
        
        private VirtualStick _leftStick;
        private VirtualStick _rightStick;

        // Camera Rotation Accumulators
        private float m_pitch = 0f;
        private float m_yaw = 0f;

        // input
        //private InputManager.InputTracker _primary   = new InputManager.InputTracker(InputManager.InputLevel.Primary);
        //private InputManager.InputTracker _secondary   = new InputManager.InputTracker(InputManager.InputLevel.Secondary);

        //!
        //! Constructor
        //! @param name Name of this module
        //! @param Manager reference for this module
        //!
        public OnScreenNavigationModule(string _name, Manager _manager) : base(_name, _manager){
            //load = false;
        }

        public bool IsOnScreenNavigationModuleActive(){ return isActive; }

        //!
        //! Function when Unity is loaded, create the top most ui button
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //!
        protected override void Init(object _sender, EventArgs _e){
            Debug.Log("<color=orange>Init OnScreenNavigation Module</color>");
            MenuButton onScreenNavigationMenuButton = new MenuButton("", ToggleOnScreenNavigation, new List<UIManager.Roles>() { UIManager.Roles.SET });
            onScreenNavigationMenuButton.setIcon("Images/button_onScreenNav_off");
            manager.addButton(onScreenNavigationMenuButton);

            _mainCamera = Camera.main.transform;
            _inputManager = core.getManager<InputManager>();

            CreateOnScreenNavUI();
            DisableOnScreenNavUI();
        }


        //! 
        //! Function called before Unity destroys the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        public override void Dispose(){
            base.Dispose();
        }


        private void ToggleOnScreenNavigation(){
            isActive = !isActive;

            if(isActive){
                EnableOnScreenNavUI();
            }else{
                DisableOnScreenNavUI();
            }

            Debug.Log("<color=orange>ToggleMeasureUI: "+isActive+"</color>");
        }

        private void EnableOnScreenNavUI(){
            if (_navCanvas == null)
                return;

            _navCanvas.gameObject.SetActive(true);

            _leftStick?.Reset();
            _rightStick?.Reset();

            core.getManager<InputManager>().Subscribe<InputManager.DragUIEvent>(DragFunction);
        }

        private void DisableOnScreenNavUI(){
            if (_navCanvas == null)
                return;
        
            _navCanvas.gameObject.SetActive(false);
            core.getManager<InputManager>().Unsubscribe<InputManager.DragUIEvent>(DragFunction);

            // Ensure sticks reset if disabled while holding
            _leftStick?.Reset();
            _rightStick?.Reset();
        }

        private void InitializeCameraAngles(){
            Vector3 currentEuler = _mainCamera.eulerAngles;
            m_pitch = currentEuler.x;
            m_yaw = currentEuler.y;
        }

        private void CameraPedestalTruck(){
            if (_leftStick.Input == Vector2.zero) return;

            // Left/Right (Truck) and Forward/Backward (Pedestal on local Z)
            Vector3 moveDelta = new Vector3(_leftStick.Input.x, 0f, _leftStick.Input.y);
            _mainCamera.Translate(moveDelta * movementSpeed * Time.deltaTime, Space.Self);
        }

        private void CameraLookAround(){
            if (_rightStick.Input == Vector2.zero) return;

            // X axis rotates pitch (up/down), Y axis rotates yaw (left/right)
            m_pitch -= _rightStick.Input.y * rotationSpeed * Time.deltaTime;
            m_yaw += _rightStick.Input.x * rotationSpeed * Time.deltaTime;

            // Clamp pitch to avoid doing a backflip
            m_pitch = Mathf.Clamp(m_pitch, -89f, 89f);

            _mainCamera.eulerAngles = new Vector3(m_pitch, m_yaw, 0f);
        }

        //!
        //! Function to connect input managers input event for dragging
        //!
        //! @param evt the InputData
        //!
        private void DragFunction(InputManager.DragUIEvent evt){

            InputManager.InputLevel level = evt.Data.Level;
            if (evt.Data.Level == InputManager.InputLevel.Tertiary) return;

            Vector2 screenPos = evt.Data.Position;

            // check phase
            switch (evt.Data.State){
                case InputManager.InputState.Started:
                    // Determine which stick to bind this touch to (Split screen down the middle)
                    if (screenPos.x < Screen.width / 2f && !_leftStick.IsDragging){
                        TryStartStick(_leftStick, evt.StartPos);
                        if (_leftStick.IsDragging) {
                            _leftStick.BoundLevel = level;
                            _inputManager.SetMultiTouchGestures(false);
                        }
                    }else if(screenPos.x > Screen.width / 2f && !_rightStick.IsDragging){
                        TryStartStick(_rightStick, evt.StartPos);
                        if (_rightStick.IsDragging) {
                            _rightStick.BoundLevel = level; // Lock this finger to this stick
                            _inputManager.SetMultiTouchGestures(false);
                            InitializeCameraAngles();
                        }
                    }
                    break;
                case InputManager.InputState.Ongoing:
                    // Only process if the stick is active AND the incoming finger matches the bound finger
                    if (_leftStick.IsDragging && _leftStick.BoundLevel == level){
                        ProcessStick(_leftStick, screenPos);
                        CameraPedestalTruck();
                        //FireDragUIEvent(_tracker, InputManager.InputState.Ongoing, screenCenter, _leftStick.Input * movementSpeed);
                    }else if (_rightStick.IsDragging && _rightStick.BoundLevel == level){
                        ProcessStick(_rightStick, screenPos);
                        CameraLookAround();
                    }
                    break;
                case InputManager.InputState.Canceled:
                case InputManager.InputState.Ended:
                    // Release the stick and clear the lock if the bound finger is lifted
                    if (_leftStick.IsDragging && _leftStick.BoundLevel == level){
                        _leftStick.Reset();
                        _leftStick.BoundLevel = null;
                        if(!_rightStick.IsDragging)
                            _inputManager.SetMultiTouchGestures(true);
                    }else if (_rightStick.IsDragging && _rightStick.BoundLevel == level){
                        _rightStick.Reset();
                        _rightStick.BoundLevel = null;
                        if(!_leftStick.IsDragging)
                            _inputManager.SetMultiTouchGestures(true);
                    }
                    break;
            }
        }

        private void TryStartStick(VirtualStick stick, Vector2 dragStartPos){
            // null bei Overlay-Canvas
            RectTransformUtility.ScreenPointToLocalPointInRectangle(stick.BaseRect, dragStartPos, null, out Vector2 touchRelativeToBaseCenter);

            if (touchRelativeToBaseCenter.magnitude <= stick.Radius){
                stick.IsDragging = true;
                stick.BaseImage.color = activeColor;
                stick.KnobImage.color = activeColor;

                if (dynamicOrigin){
                    // Da touchRelativeToBaseCenter genau die Abweichung vom Mittelpunkt ist, 
                    // addieren wir das einfach zur aktuellen Position, um den Mittelpunkt zum Finger zu schieben.
                    stick.BaseRect.anchoredPosition += touchRelativeToBaseCenter;
                }
                
                ProcessStick(stick, dragStartPos);
            }
        }

        private void ProcessStick(VirtualStick stick, Vector2 touchPos){
            RectTransformUtility.ScreenPointToLocalPointInRectangle(stick.BaseRect, touchPos, null, out Vector2 localTouchPos);

            Vector2 clampedPos = Vector2.ClampMagnitude(localTouchPos, stick.Radius);
            stick.KnobRect.anchoredPosition = clampedPos;

            stick.Input = clampedPos / stick.Radius;    // (-1.0 bis 1.0)
        }

        // --- UI CREATION ---
        private void CreateOnScreenNavUI(){
            if (_navCanvas != null) return;

            circleSprite = GetOrCreateCircleSprite();

            // 1. Create Canvas
            GameObject canvasGO = new GameObject("OnScreenNavCanvas");
            _navCanvas = canvasGO.AddComponent<Canvas>();
            _navCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _navCanvas.sortingOrder = 10; // Ensure it renders over other UI
            
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGO.AddComponent<GraphicRaycaster>(); // This intercepts 3D raycasts automatically!

            // Reale Pixel-Werte ausrechnen
            float baseRadius = (Screen.height * stickBasePercent) / 2f;
            float knobRadius = (Screen.height * stickKnobPercent) / 2f;
            float sideOffset = Screen.width * sideOffsetPercent;
            float bottomOffset = Screen.height * bottomOffsetPercent;

            // 2. Create Sticks
            _leftStick = CreateStick("LeftStick", canvasGO.transform, new Vector2(sideOffset, bottomOffset), TextAnchor.LowerLeft, baseRadius, knobRadius);
            _rightStick = CreateStick("RightStick", canvasGO.transform, new Vector2(-sideOffset, bottomOffset), TextAnchor.LowerRight, baseRadius, knobRadius);
        }

        private Sprite GetOrCreateCircleSprite(){
            if (circleSprite != null) return circleSprite;

            int resolution = 128; // 128x128 is a good balance for UI crispness vs generation speed
            Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            Color[] colors = new Color[resolution * resolution];
            
            float center = resolution / 2f;
            float radius = resolution / 2f;

            for (int y = 0; y < resolution; y++){
                for (int x = 0; x < resolution; x++){
                    // Calculate distance from center (with 0.5f offset for pixel center)
                    float dx = (x + 0.5f) - center;
                    float dy = (y + 0.5f) - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // Anti-aliasing math: smooth fade over 1 pixel at the edge
                    float alpha = Mathf.Clamp01(radius - distance);
                    
                    colors[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply(); // Upload to GPU

            // Create the sprite and pivot at center
            circleSprite = Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), 100f);
            return circleSprite;
        }

        private VirtualStick CreateStick(string name, Transform parent, Vector2 offset, TextAnchor anchor, float baseRadius, float knobRadius){
            // Base
            GameObject baseGO = new GameObject($"{name}_Base");
            baseGO.transform.SetParent(parent, false);
            Image baseImg = baseGO.AddComponent<Image>();
            baseImg.color = idleColor;
            baseImg.raycastTarget = true;
            if (circleSprite != null) baseImg.sprite = circleSprite; 

            RectTransform baseRect = baseGO.GetComponent<RectTransform>();
            baseRect.sizeDelta = new Vector2(baseRadius * 2, baseRadius * 2);

            // Knob
            GameObject knobGO = new GameObject($"{name}_Knob");
            knobGO.transform.SetParent(baseGO.transform, false);
            Image knobImg = knobGO.AddComponent<Image>();
            knobImg.color = idleColor;
            knobImg.raycastTarget = false; 
            if (circleSprite != null) knobImg.sprite = circleSprite; 

            RectTransform knobRect = knobGO.GetComponent<RectTransform>();
            knobRect.sizeDelta = new Vector2(knobRadius * 2, knobRadius * 2);

            // Anchors
            Vector2 anchorPivot = anchor == TextAnchor.LowerLeft ? new Vector2(0, 0) : new Vector2(1, 0);
            baseRect.anchorMin = anchorPivot;
            baseRect.anchorMax = anchorPivot;
            baseRect.pivot = new Vector2(0.5f, 0.5f);
            baseRect.anchoredPosition = offset;

            return new VirtualStick(baseRect, baseImg, knobRect, knobImg, baseRect.anchoredPosition, baseRadius, idleColor);
        }

        // --- HELPER CLASS ---
        private class VirtualStick{
            public InputManager.InputLevel? BoundLevel = null;
            public RectTransform BaseRect;
            public Image BaseImage;
            public RectTransform KnobRect;
            public Image KnobImage;
            
            public Vector2 DefaultAnchoredPos;
            public float Radius;

            public Vector2 Input;
            public bool IsDragging;
            
            private Color _idleColor;

            public VirtualStick(RectTransform baseRect, Image baseImage, RectTransform knobRect, Image knobImage, Vector2 defaultPos, float radius, Color idleColor){    BaseRect = baseRect;
                BaseRect = baseRect;
                BaseImage = baseImage;
                KnobRect = knobRect;
                KnobImage = knobImage;
                DefaultAnchoredPos = defaultPos;
                Radius = radius;
                _idleColor = idleColor;
            }

            public void Reset(){
                IsDragging = false;
                Input = Vector2.zero;
                BaseRect.anchoredPosition = DefaultAnchoredPos; // local canvas space
                KnobRect.anchoredPosition = Vector2.zero; // Center knob in base
                BaseImage.color = _idleColor;
                KnobImage.color = _idleColor;
            }
        }
    }
}
