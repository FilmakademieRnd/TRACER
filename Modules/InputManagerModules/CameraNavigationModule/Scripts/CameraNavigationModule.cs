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
        private Transform m_camXform;

        //!
        //! Flag to specify if there are objects selected. 
        //!
        private bool m_hasSelection;

        //!
        //! The average position of the selected objects.
        //!
        private Vector3 m_selectionCenter;

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
        //! The camera center of interest point
        //!
        private Vector3 centerOfInterest;

        //!
        //! A buffer vector storing the position offset between camera and center of interest
        //!
        private Vector3 coiOffset;

        //!
        //! A control variable for orbiting operation
        //!
        private bool stickToOrbit = false;

        //!
        //! A parameter defining how close to the edge an object can be and still act as center of interest
        //!
        private float screenTolerance = .05f;
        //!
        //! dont run the coroutine to focus on an object via double click twice
        //!
        private bool m_smoothCameraFocusIsRunning = false;

        //!
        //! Constructor.
        //!
        //! @param name Name of this module.
        //! @param _core Reference to the TRACER _core.
        //!
        public CameraNavigationModule(string name, Manager manager) : base(name, manager)
        {

        }

        //!
        //! Destructor, cleaning up event registrations. 
        //!
        public override void Dispose()
        {
            base.Dispose();

            // Unsubscribe
            manager.pinchEvent -= CameraDolly;
            manager.twoDragEvent -= CameraOrbit;
            manager.threeDragEvent -= CameraPedestalTruck;
            UIManager uiManager = core.getManager<UIManager>();
            uiManager.selectionChanged -= SelectionUpdate;
            uiManager.selectionFocus -= FocusOnSelection;
            manager.updateCameraUICommand -= CameraUpdated;
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
            m_camXform = m_cam.transform;

            // Subscription to input events
            manager.pinchEvent += CameraDolly;
            manager.twoDragEvent += CameraOrbit;
            manager.threeDragEvent += CameraPedestalTruck;

            // Subscribe to selection change
            UIManager uiManager = core.getManager<UIManager>();
            uiManager.selectionChanged += SelectionUpdate;
            // Subscribe to focus event
            uiManager.selectionFocus += FocusOnSelection;

            // Subscribe to camera change
            manager.updateCameraUICommand += CameraUpdated;

            // Initialize control variables
            m_selectionCenter = Vector3.zero;
            m_hasSelection = false;
        }

        //! 
        //! Function that updates the camera center of interest for new camera selection.
        //! 
        //! @param sender The input manager.
        //! @param e Not used.
        //!
        private void CameraUpdated(object sender, bool e)
        {
            // Assign arbitrary center of interest
            centerOfInterest = m_camXform.TransformPoint(Vector3.forward * 6f);

            // Store positional offset
            coiOffset = m_camXform.position - centerOfInterest;
        }


        //! 
        //! Dolly function: moves the camera forward.
        //! 
        //! @param sender The input manager.
        //! @param e The distance between the touch gesture triggering the movement.
        //!
        private void CameraDolly(object sender, float distance)
        {
            if(!manager.IsScreenCamNavigationUsed())
                return;

            // Dolly cam
            m_camXform.Translate(0f, 0f, distance * s_dollySpeed);

            // Check if center of interest is in front of camera
            Vector3 camCoord = m_cam.WorldToViewportPoint(centerOfInterest);
            if(camCoord.z < 0)
                // Else snap to camera
                centerOfInterest = m_camXform.position;

            // Store positional offset
            coiOffset = m_camXform.position - centerOfInterest;

        }

        //! 
        //! Orbit function: rotates the camera around a pivot point.
        //! Currently the orbit point is set to a specific distance from the camera.
        //! 
        //! @param sender The input manager.
        //! @param e The delta distance from the touch gesture triggering the movement.
        //!
        private void CameraOrbit(object sender, Vector2 delta)
        {
            if(!manager.IsScreenCamNavigationUsed())
                return;

            // Prepare the pivot point
            Vector3 pivotPoint;

            // If an object is selected
            if (m_hasSelection)
            {
                pivotPoint = m_selectionCenter;
                // Check if selection center is inside camera view
                Vector3 camCoord = m_cam.WorldToViewportPoint(m_selectionCenter);
                // If any element is negative, it out of camera
                if (camCoord.x < screenTolerance || camCoord.y < screenTolerance || camCoord.x > 1 - screenTolerance || camCoord.y > 1 - screenTolerance || camCoord.z < 0 || stickToOrbit)
                {
                    // If center of interest coincides with selection center
                    if (centerOfInterest == m_selectionCenter)
                    {
                        // It means the center of orbit was already set to an object, and needs to be reset to the center
                        centerOfInterest = m_camXform.TransformPoint(Vector3.forward * 6f);
                    }
                    pivotPoint = centerOfInterest;
                    // And it should not change until selection is changed (else orbiting pivot will jump to object as soon as it 
                    stickToOrbit = true;
                }
            }
            else
            {
                pivotPoint = centerOfInterest;
            }

            // Arc
            m_camXform.RotateAround(pivotPoint, Vector3.up, s_orbitSpeed * delta.x);
            // Tilt
            m_camXform.RotateAround(pivotPoint, m_camXform.right, -s_orbitSpeed * delta.y);

            // Update value
            centerOfInterest = pivotPoint;
            // Store positional offset
            coiOffset = m_camXform.position - centerOfInterest;

        }

        //! 
        //! Pedestal & Truck function: moves the camera vertically or horizontally.
        //! 
        //! @param sender The input manager.
        //! @param e The delta distance from the touch gesture triggering the movement.
        //!
        private void CameraPedestalTruck(object sender, Vector2 delta)
        {
            if(!manager.IsScreenCamNavigationUsed())
                return;

            // Adjust the input
            Vector2 offset = -s_panSpeed * delta;

            // Move around
            m_camXform.Translate(offset.x, offset.y, 0);

            // If it was not orbited
            if (centerOfInterest != m_selectionCenter)
            {
                // Drag the center of interest with it
                centerOfInterest = m_camXform.position - coiOffset;
            }
        }

        //!
        //! Function called when selection has changed.
        //!
        private void SelectionUpdate(object sender, List<SceneObject> sceneObjects)
        {
            m_hasSelection = false;
            if (sceneObjects.Count < 1)
            {
                // In case of deselection, set the center of interest back to the center of the view
                // If it has been orbited (meaning center of interest coincides to selection), preserve distance to camera
                if (centerOfInterest == m_selectionCenter)
                {
                    Vector3 bufferpos = m_cam.WorldToViewportPoint(m_selectionCenter);
                    bufferpos.x = .5f;
                    bufferpos.y = .5f;
                    centerOfInterest = m_cam.ViewportToWorldPoint(bufferpos);

                    // Store positional offset
                    coiOffset = m_camXform.position - centerOfInterest;
                }
                return;
            }

            // Calculate the average position
            Vector3 averagePos = Vector3.zero;
            foreach (SceneObject obj in sceneObjects)
                averagePos += obj.transform.position;
            averagePos /= sceneObjects.Count;

            m_selectionCenter = averagePos;
            m_hasSelection = true;

            // Reset control variable
            stickToOrbit = false;
        }

        //!
        //! Focus on the current object (center it, move cam to it)
        //!
        private void FocusOnSelection(object sender, SceneObject sceneObject){
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

            //Smooth transition
            sceneObject.StartCoroutine(SmoothCameraFocus(radius, b.center, b.center - m_camXform.forward * dist));
            
            // if (m_cam.orthographic)
            //     m_cam.orthographicSize = radius;
            
            // // Frame the object hierarchy
            // m_camXform.LookAt(b.center);
            // m_camXform.position = b.center - m_camXform.forward * dist;
        }

        //!
        //! coroutine to smoothly focus an object
        //!
        private IEnumerator SmoothCameraFocus(float orthSize, Vector3 lookAt, Vector3 pos){
            while(m_smoothCameraFocusIsRunning){
                yield return null;
            }
            m_smoothCameraFocusIsRunning = true;

            float t = 0f;
            float easeProgress;
            float duration = 1f;
            Vector3 currentPos = m_camXform.position;
            Vector3 currentLookAt = currentPos + m_camXform.forward;
            float currentOrth = m_cam.orthographicSize;
            while(t<1f){
                t += Time.deltaTime / duration;
                easeProgress = EaseOutCirc(t);
                m_camXform.position = Vector3.Lerp(currentPos, pos, easeProgress);
                m_camXform.LookAt(Vector3.Lerp(currentLookAt, lookAt, easeProgress));
                if (m_cam.orthographic)
                    m_cam.orthographicSize = Mathf.Lerp(currentOrth, orthSize, easeProgress);
                //invoke to update the gizmo sizes
                manager.SmoothCameraFocusChange();
                yield return null;
            }


            m_smoothCameraFocusIsRunning = false;
        }

        public static float EaseOutCirc(float progress01){
            return Mathf.Sqrt(1 - Mathf.Pow(progress01 - 1f, 2f));
        }

    }
}