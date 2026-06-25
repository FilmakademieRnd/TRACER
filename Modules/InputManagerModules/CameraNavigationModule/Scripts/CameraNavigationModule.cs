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

//! @file "CameraNavigationModule.cs"
//! @brief implementation of TRACER camera navigation features
//! @author Paulo Scatena
//! @version 0
//! @date 23.03.2022

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace tracer
{
    //!
    //! implementation of TRACER camera navigation
    //!
    public class CameraNavigationModule : InputManagerModule
    {
        //!
        //! A reference to the main camera.
        //!
        private Camera m_cam;

        //!
        //! A reference to the main camera transform.
        //!
        private Transform camTransform;

        //!
        //! Flag to specify if there are objects selected. 
        //!
        private bool m_hasSelection;

        //!
        //! The average position of the selected objects.
        //!
        private Vector3 m_selectionCenter;
        //!
        //! An evaluated position to orbit around if we have no object selected
        //!
        private Vector3 evaluatedNonSelectionCenterForOrbit;

        // TODO: maybe promote these variables to configuration options
        //!
        //! The speed factor for the pan movement.
        //!
        private static readonly float s_panSpeed = .005f;

        //!
        //! The speed factor for the orbit movement.
        //!
        private static readonly float s_orbitSpeed = .15f;

        //!
        //! The speed factor for the dolly movement.
        //!
        private static readonly float s_dollySpeed = .007f;

        //!
        //! The higher the multiplier, the farther the camera will be away from the selected object
        //!
        private static readonly float s_focusDistance = 1.5f;

        //!
        //! do we focus an object? If so and we focus it again, lock to the view and follow it!
        //!
        private SceneObject currentFocusedObject;

        //!
        //! Follow this focused object (not if we do any gizmo transformation)
        //!
        private SceneObject currentFollowObject;

        //!
        //! A parameter defining how close to the edge an object can be and still act as center of interest
        //!
        private float screenTolerance = .05f;
        //!
        //! dont run the coroutine to focus on an object via double click twice
        //!
        private int m_smoothCameraFocusIsRunning = 0;
        //!
        //! storage variables for camera rotation, to not become weird angled, but take into account starting values
        //!
        private float m_pitch, m_yaw, m_roll = 0f;
        //!
        //! if we receive that sensors values via input manager, refrain from allowing other rotation input!
        //! comment out if this bevhaiour is not   intended
        //!
        private bool attitudeValuesIncoming = false;
        private Quaternion cameraMainRotationOffset, attitudeOffset;

        #region Fly Variables
        //!
        //! storage variables for camera fly around (distance from starting point for speed)
        //!
        private Vector2 screenStartPos;
        private float maxSpeed = 5f;
        private float acceleration = 1f;
        private float deceleration = 2f;
        
        //Timers & Thresholds
        private float bootDelay = 2.0f;
        private float lookRecoveryDelay = 0.5f;
        //public float lookDeadzone = 50f; // Pixels distance from startPos

        //UI Resources
        private Sprite circleSprite;

        // State tracking
        private float currentSpeed = 0f;
        private float bootTimer = 0f;
        private float lookRecoveryTimer = 0f;
        private bool isBooted = false;
        private bool isFlying = false;

        // UI Tracking
        private GameObject uiContainer;
        private Image smallCircleFill;
        private RectTransform largeCircleRect;
        private Image largeCircleProgress;
        private float largeCircleAnimTimer = 0f;

        #endregion

        //!
        //! Constructor.
        //!
        //! @param name Name of this module.
        //! @param _core Reference to the TRACER _core.
        //!
        public CameraNavigationModule(string name, Manager manager) : base(name, manager){
        }

        //!
        //! Destructor, cleaning up event registrations. 
        //!
        public override void Dispose(){
            base.Dispose();

            // Unsubscribe
            UIManager uiManager = core.getManager<UIManager>();
            uiManager.selectionChanged -= SelectionUpdate;
//            uiManager.selectionFocus -= FocusOnSelection;
//            manager.updateCameraUICommand -= CameraUpdated;

            manager.Unsubscribe<InputManager.DragOtherEvent>(DragFunction);
            manager.Unsubscribe<InputManager.HoldOtherEvent>(HoldFunction);
            manager.Unsubscribe<InputManager.PinchOtherEvent>(PinchFunction);
            manager.Unsubscribe<InputManager.AttitudeInputEvent>(AttitudeFunction);
            manager.Unsubscribe<InputManager.DoubleClickOtherEvent>(DoubleClickFunction);
            

            if (circleSprite != null){
                if (circleSprite.texture != null) 
                    UnityEngine.GameObject.Destroy(circleSprite.texture);
                UnityEngine.GameObject.Destroy(circleSprite);
            }
        }

        //! 
        //! Init m_callback for the CameraNavigation module.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Init(object sender, EventArgs e)
        {
            m_cam = Camera.main;
            camTransform = m_cam.transform;

            // Subscription to input events

            // Subscribe to selection change
            UIManager uiManager = core.getManager<UIManager>();
            uiManager.selectionChanged += SelectionUpdate;
            // Subscribe to focus event
//            uiManager.selectionFocus += FocusOnSelection;   //change behaviour? subscribe to double click

            // Subscribe to camera change
//            manager.updateCameraUICommand += CameraUpdated;

            manager.Subscribe<InputManager.DragOtherEvent>(DragFunction);
            manager.Subscribe<InputManager.HoldOtherEvent>(HoldFunction);
            manager.Subscribe<InputManager.PinchOtherEvent>(PinchFunction);
            manager.Subscribe<InputManager.AttitudeInputEvent>(AttitudeFunction);
            manager.Subscribe<InputManager.DoubleClickOtherEvent>(DoubleClickFunction);

            // Instantiate once
            _orbitViz = new OrbitImpactPin(core);

            // Initialize control variables
            m_selectionCenter = Vector3.zero;
            m_hasSelection = false;
        }


        //!
        //! Function to connect input managers input event for dragging a sceneObjects gizmo
        //!
        //! @param evt the InputData
        //!
        private void DragFunction(InputManager.DragOtherEvent evt){
            
            if(!manager.IsCamNavigationAllowed())
                return;

            switch (evt.Data.Level) {
                //ROTATE CAMERA
                case InputManager.InputLevel.Primary:
                    if(attitudeValuesIncoming)
                        return;
                    // check phase
                    switch (evt.Data.State){
                        case InputManager.InputState.Started:
                            InitializeCameraAngles();
                            break;
                        case InputManager.InputState.Ongoing:
                        case InputManager.InputState.Canceled:
                            CameraLookAround(evt.Data.Delta);
                            break;
                        case InputManager.InputState.Ended:
                            break;
                    }
                    break;
                //MOVE CAMERA
                case InputManager.InputLevel.Secondary:
                    // check phase
                    switch (evt.Data.State){
                        case InputManager.InputState.Started:
                            break;
                        case InputManager.InputState.Ongoing:
                        case InputManager.InputState.Canceled:
                            CameraPedestalTruck(evt.Data.Delta);
                            break;
                        case InputManager.InputState.Ended:
                            break;
                    }
                    break;
                //Rotate Around center of selected object(s)
                case InputManager.InputLevel.Tertiary:
                    // check phase
                    switch (evt.Data.State){
                        case InputManager.InputState.Started:
                            InitializeCameraAngles();
                            if(!m_hasSelection){
                                EvaluateObjectForOrbit(new Vector2(Screen.width/2f, Screen.height/2f));
                                // Drops the pin and spawns the expanding ground ring
                                _orbitViz.StartPin(evaluatedNonSelectionCenterForOrbit, camTransform.position);
                            }
                            break;
                        case InputManager.InputState.Ongoing:
                        case InputManager.InputState.Canceled:
                            if(m_hasSelection)
                                CameraLookAroundObject(evt.Data.Delta, m_selectionCenter);
                            else{
                                CameraLookAroundObject(evt.Data.Delta, evaluatedNonSelectionCenterForOrbit);
                                // Dynamically fills the ground arc as the camera swings around
                                _orbitViz.UpdateOrbit(camTransform.position);
                            }
                            break;
                        case InputManager.InputState.Ended:
                            // Gracefully shrinks and fades away, even if it was interrupted mid-drop
                            _orbitViz.Dismiss();
                            break;
                    }
                    break;  
            } 
        }

        private void HoldFunction(InputManager.HoldOtherEvent evt){
            
            if(!manager.IsCamNavigationAllowed() || attitudeValuesIncoming)
                return;

            switch (evt.Data.Level) {
                //Fly around?
                //fwd/bck
                case InputManager.InputLevel.Tertiary:
                    // check phase
                    switch (evt.Data.State){
                        case InputManager.InputState.Started:
                            StartFlightInteraction(evt.Data.Position);
                            InitializeCameraAngles();
                            screenStartPos = evt.Data.Position;
                            break;
                        case InputManager.InputState.Ongoing:
                        case InputManager.InputState.Canceled:
                            //float fwdSpeedByDistance = evt.Data.Position.y - screenStartPos.y;
                            //CameraFlying(fwdSpeedByDistance, evt.Data.Delta.x);
                            ProcessContinuousFlight(screenStartPos, evt.Data.Position, evt.Data.Delta);
                            break;
                        case InputManager.InputState.Ended:
                            StopFlightInteraction();
                            break;
                    }
                    break;  
            }
        }

        private void PinchFunction(InputManager.PinchOtherEvent evt){
            
            if(!manager.IsCamNavigationAllowed()) // || attitudeValuesIncoming)
                return;

            switch (evt.Data.Level) {
                //allow all levels
                case InputManager.InputLevel.Primary:
                case InputManager.InputLevel.Secondary:
                case InputManager.InputLevel.Tertiary:
                    // check phase
                    switch (evt.Data.State){
                        case InputManager.InputState.Started:
                        case InputManager.InputState.Ongoing:
                            camTransform.Translate(0f, 0f, evt.PinchDistance * s_dollySpeed);
                            break;
                        case InputManager.InputState.Canceled:
                        case InputManager.InputState.Ended:
                            break;
                    }
                    break;  
            }
        }

        //!
        //! Function to connect input managers input event when attitude sensor switched on
        //!
        //! @param evt the InputData
        //!
        private void AttitudeFunction(InputManager.AttitudeInputEvent evt){
            
            switch (evt.Data.Level) {
                //ROTATE CAMERA
                case InputManager.InputLevel.Primary:
                    switch (evt.Data.State){
                        case InputManager.InputState.Started:
                            attitudeValuesIncoming = true;
                            InitializeAttitudeValues(evt.Rotation);
                            //TODO: dont allow camera view manipulation!
                            break;
                        case InputManager.InputState.Ongoing:
                            // if(!attitudeValuesIncoming){
                            // }else
                                ApplyAttitudeValues(evt.Rotation);
                            break;
                        case InputManager.InputState.Canceled:
                        case InputManager.InputState.Ended:
                            attitudeValuesIncoming = false;
                            break;
                    }
                    break; 
            } 
        }

        //!
        //! Function to connect input managers input event for clicking on the timeline
        //!
        //! @param evt the InputData
        //!
        private void DoubleClickFunction(InputManager.DoubleClickOtherEvent evt){

            switch (evt.Data.Level) {
                case InputManager.InputLevel.Primary:
                    SceneObject hitSO = EvaluationHelper.Instance.EvaluateSceneObject(evt.Data.Position);
                    FocusOnSelection(hitSO);
                    break;  
            }
        }

        private void InitializeCameraAngles() {    
            Vector3 currentAngles = camTransform.eulerAngles;
            
            m_pitch     = currentAngles.x;
            m_yaw       = currentAngles.y;
            m_roll      = currentAngles.z; // Capture the initial tilt!

            // Normalize pitch to -180 to 180 so our Mathf.Clamp works correctly
            if (m_pitch > 180f) { m_pitch -= 360f; }

            //check again for selection center, since we could have moved the object in the meantime
            // [!REVISE] what if animation is playing and the object is moving, re-evalute all the time?
            if (m_hasSelection) {
                SetSelectionCenter(core.getManager<UIManager>().SelectedObjects);
            }
        }

        //!
        //! [NO] ~~when on mobile or via controller, us the cam fwd vectror projected onto the ground plane~~
        //! [NO] ~~via mouse use input start position to use for rotate around check~~
        //! have same behaviour everywhere:
        //! make an evaluation from the camera center and use any non-2d hit as rotate-around-center
        //!     if no hit was made, use ground plane height (Y = 0!)
        //!
        private void EvaluateObjectForOrbit(Vector2 sceenCenterPos) {
            SceneObject hitSO = EvaluationHelper.Instance.EvaluateSceneObject(sceenCenterPos);
            if (hitSO) {
                evaluatedNonSelectionCenterForOrbit = hitSO.transform.position;
                return;
            }
            GameObject hitGO = EvaluationHelper.Instance.EvaluateGameObject(sceenCenterPos);
            if (hitGO) {
                evaluatedNonSelectionCenterForOrbit = hitGO.transform.position;
                return;
            }

            //make the raycast to the imaginary ground plane at Y = 0
            Ray ray = new(Camera.main.transform.position, Camera.main.transform.forward);
            Plane plane = new(Vector3.up, Vector3.zero);

            if (plane.Raycast(ray, out float distance)) {
                evaluatedNonSelectionCenterForOrbit = ray.GetPoint(distance);
            } else {
                evaluatedNonSelectionCenterForOrbit = Camera.main.transform.forward * 30;
            }
        }

        #region Magic Window Metapher

        /****** 
        *
        *   AI DESCRIPTION WHAT THE BELOW DOES
        *   - as I'm no Quaternion Expert, Thomas
        *
        *   Look at Line 1, and then look at the far right of Line 2. You are setting localRotation, 
        *   and then immediately reading rotation in the very next instruction.
        *
        *   In pure C#, that looks redundant. But in Unity's underlying C++ engine, setting localRotation 
        *   sets a dirty flag. The millisecond you call .rotation on Line 2, you force Unity's C++ main 
        *   thread to halt, grab the parent's world matrix, multiply your Line 1 local pose by the parent's 
        *   world space, and hand it back to C#.
        *
        *   By taking the result of Line 1, getting Unity to bake it into World Space, and feeding it back 
        *   into the delta multiplier on Line 2, you created a self-clearing feedback loop that renders the 
        *   camera 100% immune to its parent transform's scale or rotation. Write a comment above those two 
        *   functions warning future developers never to touch them
        *
        */
        private void InitializeAttitudeValues(Quaternion attitudeRotation) {    
            cameraMainRotationOffset = camTransform.rotation;
            attitudeOffset = Quaternion.Inverse(attitudeRotation * Quaternion.Euler(0f, 0f, 180f));
        }

        private void ApplyAttitudeValues(Quaternion attitudeRotation) {
            camTransform.localRotation = attitudeRotation * Quaternion.Euler(0f, 0f, 180f);
            camTransform.rotation = cameraMainRotationOffset * attitudeOffset * camTransform.rotation;
        }
        #endregion

        //! 
        //! rotate the camera from a pov
        //! 
        //! @param e The delta distance from drag input
        //!
        private void CameraLookAround(Vector2 delta){
           // Accumulate the angles
            m_yaw   += s_orbitSpeed * delta.x;
            m_pitch -= s_orbitSpeed * delta.y;

            // Clamp the pitch so the camera can't flip upside down
            // -89 is straight up, 89 is straight down
            m_pitch = Mathf.Clamp(m_pitch, -89f, 89f);

            // Apply the rotation via Euler Angles. 
            // Notice the Z value is forced to 0f. It is mathematically impossible for the camera to tilt sideways now.
            camTransform.eulerAngles = new Vector3(m_pitch, m_yaw, m_roll);
        }

        //! 
        //! Orbit function: rotates the camera around a selected object
        //! @param delta The delta distance from the touch gesture triggering the movement.
        //! @param objsCenter the center of the object or multiple objects if more are selected
        //!
        private void CameraLookAroundObject(Vector2 delta, Vector3 objsCenter){
            // Check if selection center is inside camera view
            Vector3 objInViewportCoords = m_cam.WorldToViewportPoint(objsCenter);
            // If any element is negative, it out of camera
            if (objInViewportCoords.x < screenTolerance || objInViewportCoords.y < screenTolerance || objInViewportCoords.x > 1 - screenTolerance || objInViewportCoords.y > 1 - screenTolerance || objInViewportCoords.z < 0)
                return;
        
            // 1. Calculate desired deltas
            float yawDelta = s_orbitSpeed * delta.x;
            float pitchDelta = -s_orbitSpeed * delta.y;

            // 2. Predict the new pitch to clamp it properly before moving
            float nextPitch = m_pitch + pitchDelta;
            
            if (nextPitch > 89f) {
                // Clamp going too far down
                pitchDelta = 89f - m_pitch;
                m_pitch = 89f;
            }else if (nextPitch < -89f) {
                // Clamp going too far up
                pitchDelta = -89f - m_pitch;
                m_pitch = -89f;
            }else{
                // Normal movement
                m_pitch = nextPitch;
            }

            // Keep yaw tracked for seamless switching with standard LookAround
            m_yaw += yawDelta;

            // 3. Apply the Orbit
            // Rotate around World Up for horizontal movement
            camTransform.RotateAround(objsCenter, Vector3.up, yawDelta);
            // Rotate around the Camera's Local Right for vertical movement
            camTransform.RotateAround(objsCenter, camTransform.right, pitchDelta);

            // 4. Force the Roll to stay locked
            // RotateAround can introduce microscopic floating-point roll drift over time. 
            // This locks it back to your desired m_roll.
            Vector3 currentEuler = camTransform.eulerAngles;
            camTransform.eulerAngles = new Vector3(currentEuler.x, currentEuler.y, m_roll);
        }

        //! 
        //! Pedestal & Truck function: moves the camera vertically or horizontally.
        //! 
        //! @param e The delta distance from the touch gesture triggering the movement.
        //!
        private void CameraPedestalTruck(Vector2 delta){
            // Adjust the input
            Vector2 offset = -s_panSpeed * delta;

            // Move around
            camTransform.Translate(offset.x, offset.y, 0);
        }

        private void StartFlightInteraction(Vector2 startPos){
            isFlying = true;
            isBooted = false;
            bootTimer = 0f;
            currentSpeed = 0f;
            lookRecoveryTimer = 0f;
            largeCircleAnimTimer = 0f;

            CreateDynamicUI(startPos);
        }

        // --- DYNAMIC UI GENERATION ---
        private void CreateDynamicUI(Vector2 screenPos){
            if (uiContainer != null) UnityEngine.GameObject.Destroy(uiContainer);

            circleSprite = GetOrCreateCircleSprite();

            // Create Canvas overlay
            uiContainer = new GameObject("FlightUI");
            Canvas canvas = uiContainer.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Render on top

            // Container placed at touch position
            GameObject posContainer = new GameObject("PosTracker");
            posContainer.transform.SetParent(uiContainer.transform);
            RectTransform posRect = posContainer.AddComponent<RectTransform>();
            posRect.position = screenPos;

            // 1. Small Boot Circle
            GameObject smallGO = new GameObject("SmallBootCircle");
            smallGO.transform.SetParent(posRect);
            smallCircleFill = smallGO.AddComponent<Image>();
            smallCircleFill.sprite = circleSprite;
            smallCircleFill.color = new Color(1f, 1f, 1f, 0.8f);
            smallCircleFill.type = Image.Type.Filled;
            smallCircleFill.fillMethod = Image.FillMethod.Radial360;
            smallCircleFill.fillAmount = 0f;
            smallCircleFill.rectTransform.sizeDelta = new Vector2(40f, 40f);
            smallGO.transform.localPosition = Vector3.zero;

            // 2. Large Background Circle
            GameObject largeBgGO = new GameObject("LargeCircleBg");
            largeBgGO.transform.SetParent(posRect);
            Image largeBg = largeBgGO.AddComponent<Image>();
            largeBg.sprite = circleSprite;
            largeBg.color = new Color(0f, 0f, 0f, 0.3f);
            largeCircleRect = largeBg.rectTransform;
            largeCircleRect.sizeDelta = new Vector2(120f, 120f);
            largeCircleRect.localScale = Vector3.zero; // Start hidden for bounce anim
            largeBgGO.transform.localPosition = Vector3.zero;

            // 3. Large Progress Circle
            GameObject largeProgGO = new GameObject("LargeCircleProgress");
            largeProgGO.transform.SetParent(largeBgGO.transform);
            largeCircleProgress = largeProgGO.AddComponent<Image>();
            largeCircleProgress.sprite = circleSprite;
            largeCircleProgress.color = new Color(0.2f, 0.8f, 1f, 0.9f); // Cyan
            largeCircleProgress.type = Image.Type.Filled;
            largeCircleProgress.fillMethod = Image.FillMethod.Radial360;
            largeCircleProgress.fillAmount = 0f;
            largeCircleProgress.rectTransform.sizeDelta = new Vector2(120f, 120f);
            largeCircleProgress.rectTransform.localScale = Vector3.one;
            largeProgGO.transform.localPosition = Vector3.zero;
        }

        // --- PROCEDURAL SPRITE GENERATION ---
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

        // --- MATH HELPERS ---
        private float EaseOutBack(float x){
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        private void ProcessContinuousFlight(Vector2 startPos, Vector2 currentPos, Vector2 delta){
            if (!isFlying) return;

            bool isLooking = delta.sqrMagnitude > 1f;

            // --- 1. BOOT PHASE ---
            if (!isBooted){
                bootTimer += Time.deltaTime;
                
                // Update small circle UI
                if (smallCircleFill != null)
                    smallCircleFill.fillAmount = bootTimer / bootDelay;

                if (bootTimer >= bootDelay){
                    isBooted = true;
                    if (smallCircleFill != null) UnityEngine.GameObject.Destroy(smallCircleFill.gameObject); // Vanish small circle
                }
                return; // Don't move or look while booting
            }

            // --- 2. ANIMATE LARGE CIRCLE ---
            if (isBooted && largeCircleAnimTimer < 1f){
                largeCircleAnimTimer += Time.deltaTime * 3f; // Animation speed
                float t = Mathf.Clamp01(largeCircleAnimTimer);
                largeCircleRect.localScale = Vector3.one * EaseOutBack(t);
            }

            // --- 3. LOOK & MOVE LOGIC ---
            if (isLooking){
                // Call your existing look function
                CameraLookAround(delta);

                // Natural deceleration: the faster you look (delta magnitude), the heavier the brake
                float brakeFactor = Mathf.Clamp01(delta.magnitude / 10f);
                currentSpeed = Mathf.Lerp(currentSpeed, 0f, deceleration * brakeFactor * Time.deltaTime);
                
                // Reset recovery timer
                lookRecoveryTimer = lookRecoveryDelay;
            }else{
                // If we recently looked, wait for recovery
                if (lookRecoveryTimer > 0){
                    lookRecoveryTimer -= Time.deltaTime;
                    // Minor natural friction while recovering
                    currentSpeed = Mathf.Lerp(currentSpeed, 0f, (deceleration * 0.2f) * Time.deltaTime); 
                }else{
                    // Accelerate
                    currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);
                }
            }

            // Apply movement
            camTransform.position += camTransform.forward * currentSpeed * Time.deltaTime;

            // --- 4. UPDATE UI PROGRESS ---
            if (largeCircleProgress != null){
                largeCircleProgress.fillAmount = currentSpeed / maxSpeed;
            }
        }

        private void StopFlightInteraction(){
            isFlying = false;
            if (uiContainer != null) UnityEngine.GameObject.Destroy(uiContainer);
        }


        //!
        //! Function called when selection has changed.
        //!
        private void SelectionUpdate(object sender, List<SceneObject> sceneObjects){
            currentFollowObject = null;

            if (sceneObjects.Count < 1){
                m_hasSelection = false;
                return;
            }

            SetSelectionCenter(sceneObjects);
            m_hasSelection = true;
        }

        private void SetSelectionCenter(List<SceneObject> sceneObjects) {
            // Calculate the average position
            Vector3 averagePos = Vector3.zero;
            foreach (SceneObject obj in sceneObjects)
                averagePos += obj.transform.position;
            averagePos /= sceneObjects.Count;

            m_selectionCenter = averagePos;
        }

        //!
        //! Focus on the current object (center it, move cam to it)
        //! [TODO] have closer/farther look at object as iteration
        //! [TODO] have "lock to object" via hold? (we may have a radial options menu for all this stuff)
        //!
        private void FocusOnSelection(SceneObject sceneObject){
            if(!sceneObject){
                currentFocusedObject = null;
                currentFollowObject = null;     //we follow this object, but not if we do any gizmo transformation!
                return;
            }
            if(currentFocusedObject == sceneObject){
                currentFollowObject = currentFocusedObject;
                //Start coroutine to follow? Only if locked? another module?
                //return; //focus again!
            }else{
                currentFocusedObject = sceneObject;
            }

            GameObject go = sceneObject.gameObject;
            //calculate bounds
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            switch(sceneObject){
                case SceneObjectCamera:
                case SceneObjectLight:
                    break;
                default:
                    UnityEngine.Object[] rList = go.GetComponentsInChildren(typeof(Renderer));
                    foreach (Renderer r in rList){
                        b.Encapsulate(r.bounds);
                    }
                    break;
            }

            Vector3 max = b.size;
            // Get the radius of a sphere circumscribing the bounds, multiply by s_focusDistance (the higher the multiply, the farther away)
            float radius = Mathf.Max(max.magnitude, 1f) / 2f * s_focusDistance;
            // Get the horizontal FOV, since it may be the limiting of the two FOVs to properly encapsulate the objects
            float horizontalFOV = 2f * Mathf.Atan(Mathf.Tan(m_cam.fieldOfView * Mathf.Deg2Rad / 2f) * m_cam.aspect) * Mathf.Rad2Deg;
            // Use the smaller FOV as it limits what would get cut off by the frustum		
            float fov = Mathf.Min(m_cam.fieldOfView, horizontalFOV);
            float dist = radius /  (Mathf.Sin(fov * Mathf.Deg2Rad / 2f));

            //Debug.Log("Radius = " + radius + " dist = " + dist);

            //never go away if dist is bigger, instead keep the distance?
            dist = Mathf.Min(dist, Vector3.Distance(go.transform.position, camTransform.position));

            //Smooth transition
            sceneObject.StartCoroutine(SmoothCameraFocus(radius, b.center, b.center - camTransform.forward * dist));

            //TODO: add another function to lock view on a locked object!
            //AND update its position smoothly (update function?!)
            //right 
        }

        //!
        //! coroutine to smoothly focus an object
        //!
        private IEnumerator SmoothCameraFocus(float orthSize, Vector3 lookAt, Vector3 pos){
            m_smoothCameraFocusIsRunning++;
            yield return null;
            int coroNr = m_smoothCameraFocusIsRunning;

            float t = 0f;
            float easeProgress;
            float duration = 1f;
            Vector3 currentPos = camTransform.position;
            Vector3 currentLookAt = currentPos + camTransform.forward;
            float currentOrth = m_cam.orthographicSize;
            while(t<1f && coroNr == m_smoothCameraFocusIsRunning){
                t += Time.deltaTime / duration;
                easeProgress = EaseOutCirc(t);
                camTransform.position = Vector3.Lerp(currentPos, pos, easeProgress);
                camTransform.LookAt(Vector3.Lerp(currentLookAt, lookAt, easeProgress));
                if (m_cam.orthographic)
                    m_cam.orthographicSize = Mathf.Lerp(currentOrth, orthSize, easeProgress);
                //invoke to update the gizmo sizes
//              manager.SmoothCameraFocusChange();
                yield return null;
            }
        }

        public static float EaseOutCirc(float progress01){
            return Mathf.Sqrt(1 - Mathf.Pow(progress01 - 1f, 2f));
        }

        #region DEBUG VIZ
        private OrbitImpactPin _orbitViz;

        #endregion
    }

}