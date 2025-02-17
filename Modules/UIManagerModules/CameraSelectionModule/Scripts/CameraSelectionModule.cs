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

//! @file "CameraSelectionModule.cs"
//! @brief Implementation of the Camera selection buttons functionality 
//! @author Simon Spielmann
//! @version 0
//! @date 27.04.2022

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace tracer
{
    public class CameraSelectionModule : UIManagerModule
    {
        private enum CameraLockageType{
            none = 0,
            lookThrough = 10,
            lockObjectToCam = 20
        }
        //!
        //! Flag determining if the camera is locked to an object.
        //!
        private CameraLockageType m_lockType = CameraLockageType.none;
        //!
        //! The index of the currently selected camera.
        //!
        private int m_cameraIndex = 0;
        //!
        //! The initial position of the selected object.
        //!
        private Vector3 m_oldPosition = Vector3.zero;
        //!
        //! The initial rotation of the selected object.
        //!
        private Quaternion m_oldRotation = Quaternion.identity;
        //!
        //! The initial rotation of the main camera.
        //!
        private Quaternion m_oldCamRotation = Quaternion.identity;
        //!
        //! The inverse initial posiotion of the main camara.
        //!
        private Quaternion m_inverseOldCamRotation = Quaternion.identity;
        //!
        //! The initial vector between the main camera and the selected object.
        //!
        private Vector3 m_positionOffset = Vector3.zero;
        //!
        //! correct way to "lock" the selected object to its local position to the MainCamera
        //!
        private Vector3 m_localPositionWouldBe;
        //!
        //! correct way to "lock" the selected object to its local rotation to the MainCamera
        //!
        private Quaternion m_localRotationWouldBe;

        //!
        //! The UI button for logging the camera to an object.
        //!
        private MenuButton m_cameraSelectButton;

        public MenuButton cameraSelectButton()
        {
            return m_cameraSelectButton;
        }
        //!
        //! The currently selected object.
        //!
        private SceneObject m_selectedObject = null;
        //!
        //! A reference to the scene manager.
        //!
        private SceneManager m_sceneManager;
        //!
        //! A reference to the input manager.
        //!
        private InputManager m_inputManager;
        //!
        //! Reference to UIManager
        //!
        UIManager m_uiManager;
        //!
        //! The preloaded prafab of the safe frame overlay game object.
        //!
        private GameObject m_safeFramePrefab;
        //!
        //! The instance of the the safe frame overlay.
        //!
        private GameObject m_safeFrame = null;
        //!
        //! The scaler of the safe frame.
        //!
        private Transform m_scaler = null;
        //!
        //! The text of the safe frame.
        //!
        private TextMeshProUGUI m_infoText = null;
        //!
        //! A copy of the last selected camera.
        //!
        private SceneObjectCamera m_oldSOCamera = null;

        //!
        //! safe frame button
        //!
        MenuButton m_safeFrameButton = null;

        private MenuButton nextCameraButton;
        //!
        //! Event emitted when camera operations are in action
        //!
        public event EventHandler<bool> uiCameraOperation;

        //!
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public CameraSelectionModule(string name, Manager manager) : base(name, manager)
        {
        }

        //! 
        //! Function called before Unity destroys the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Start(object sender, EventArgs e)
        {
            base.Start(sender, e);

            m_uiManager = core.getManager<UIManager>();
            m_sceneManager = core.getManager<SceneManager>();
            m_inputManager = core.getManager<InputManager>();

            m_safeFramePrefab = Resources.Load("Prefabs/SafeFrame") as GameObject;
            m_safeFrameButton = new MenuButton("", showSafeFrame, new List<UIManager.Roles>() { UIManager.Roles.DOP });
            m_safeFrameButton.setIcon("Images/button_safeFrames");

            nextCameraButton = new MenuButton("", selectNextCamera, new List<UIManager.Roles>() { UIManager.Roles.DOP });
            nextCameraButton.setIcon("Images/button_camera");
            nextCameraButton.isToggle = true;

            m_uiManager.addButton(m_safeFrameButton);
            m_uiManager.addButton(nextCameraButton);

            m_sceneManager.sceneReady += initCameraOnce;
            m_uiManager.selectionChanged += selection;

            m_inputManager.cameraControlChanged += updateSafeFrame;
            m_inputManager.cameraControlChanged += updateSelectCamera;
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

            m_sceneManager.sceneReady -= initCameraOnce;
            m_uiManager.selectionChanged -= selection;
            m_inputManager.cameraControlChanged -= updateSafeFrame;
            m_inputManager.cameraControlChanged -= updateSelectCamera;
        }

        //!
        //! Function that creates the camera selection ui buttons & handles selection changes. Called every time a scene object has been selected.
        //!
        //! @param sender The UI manager.
        //! @param sceneObjects a list of the currently selected objects.
        //!
        private void selection(object sender, List<SceneObject> sceneObjects)
        {
            //TODO do not reset and redo if the same object has been selected!
            // if(m_selectedObject != null && sceneObjects.Count > 0 && m_selectedObject == sceneObjects[0])
            //     return;

            if (m_cameraSelectButton != null){
                m_uiManager.removeButton(m_cameraSelectButton);
                m_cameraSelectButton = null;
            }

            if (sceneObjects.Count > 0)
            {
                m_cameraIndex = -1;
                m_selectedObject = sceneObjects[0];

                Type selectionType = m_selectedObject.GetType();
                if (selectionType == typeof(SceneObjectCamera))
                   m_cameraIndex = m_sceneManager.sceneCameraList.FindIndex(x => x.Equals((SceneObjectCamera)m_selectedObject));
                
                
                if (selectionType == typeof(SceneObjectCamera) ||
                    selectionType == typeof(SceneObjectDirectionalLight) ||
                    selectionType == typeof(SceneObjectSpotLight))
                {
                    m_cameraSelectButton = new MenuButton("", LockOnLookThrough, null, "CameraSelectionButton");
                    m_cameraSelectButton.setIcon("Images/button_lookTrough");
                }
                else
                {
                    m_cameraSelectButton = new MenuButton("", LockObjectToCameraView);
                    m_cameraSelectButton.setIcon("Images/button_lockToCamera");
                }
                m_uiManager.addButton(m_cameraSelectButton);
            }
            else
            {
                if (m_lockType != CameraLockageType.none){
                    if (m_lockType == CameraLockageType.lookThrough)
                        ResetRatio();
                    UnlockCam();
                    uiCameraOperation?.Invoke(this, true); //always true: since we clicked nowhere, we want to hide the gizmo!
                }
                m_selectedObject = null;

            }
        }

        //!
        //! Returns a more readable way whether the camera view is currently locked onto something
        //!
        private bool IsCamLocked(){ return m_lockType != CameraLockageType.none; }

        //!
        //! The function that moves the main camera to the selected object (light or cam)
        //!
        public void LockOnLookThrough(){
            if (m_selectedObject == null)
                return;
            
            
            if (IsCamLocked()){     //UNLOCK AND REVERT
                UnlockCam();
                ResetRatio();
            }else{                  //LOCK
                //Debug.Log("LOOK THROUGH "+m_selectedObject.name);
                Type selectionType = m_selectedObject.GetType();
                if(selectionType == typeof(SceneObjectCamera)){
                    copyCamera();
                }else if(selectionType == typeof(SceneObjectDirectionalLight) || (selectionType == typeof(SceneObjectSpotLight))){
                    
                }

                Camera.main.cullingMask &= ~(1 << 11);
                Camera.main.transform.position = m_selectedObject.transform.position;
                Camera.main.transform.rotation = m_selectedObject.transform.rotation;
                if (m_selectedObject.transform.parent.name != "Scene")
                {
                    Camera.main.transform.position = m_selectedObject.transform.parent.TransformPoint(m_selectedObject.transform.localPosition);
                    Camera.main.transform.rotation = m_selectedObject.transform.parent.rotation * m_selectedObject.transform.localRotation;
                }
                m_oldPosition = Vector3.zero;
                m_oldRotation = Quaternion.identity;
                m_inverseOldCamRotation = Quaternion.identity;

                m_positionOffset = Vector3.zero;
                m_oldRotation = Quaternion.identity;

                InputManager inputManager = core.getManager<InputManager>();
                if (inputManager.cameraControl == InputManager.CameraControl.ATTITUDE)
                    inputManager.setCameraAttitudeOffsets();

                core.updateEvent += updateLookThrough;
                m_lockType = CameraLockageType.lookThrough;
            }

            uiCameraOperation?.Invoke(this, IsCamLocked());
        }

        //!
        //! Unlock the camera and remove the events
        //!
        private void UnlockCam(){
            switch(m_lockType){
                case CameraLockageType.lookThrough:
                    core.updateEvent -= updateLookThrough;
                    if (m_oldSOCamera)
                        m_oldSOCamera.hasChanged -= updateCamera;
                    break;
                case CameraLockageType.lockObjectToCam:
                    core.updateEvent -= updateLockToCamera;
                    break;
            }            
            m_lockType = CameraLockageType.none;
        }

        //!
        //! resets the cams values to standard (and move it one step backwar - most likely because we looked through a camera before)
        //!
        private void ResetRatio(){
            Camera.main.fieldOfView = 60;
            Camera.main.transform.position -= Camera.main.transform.forward;
            Camera.main.cullingMask = LayerMask.NameToLayer("Everything");
        }

        //!
        //! The function that moves the main camera to the selected object and parants it to the camera.
        //!
        private void LockObjectToCameraView()
        {
            if (!m_selectedObject)
                return;
            
            if (IsCamLocked()){    //UNLOCK
                UnlockCam();
            }else{
                m_localPositionWouldBe = Camera.main.transform.InverseTransformPoint(m_selectedObject.transform.position);
                //Debug.Log("localPositionWouldBe "+m_localPositionWouldBe);
                m_localRotationWouldBe = Quaternion.Inverse(Camera.main.transform.rotation) * m_selectedObject.transform.rotation;
                //calculate the local rotation by Quaternion.Inverse(target spaces' object rotation) * world rotation of the object
                //BEWARE matrix multiplication - order matters!

                core.updateEvent += updateLockToCamera;
                m_lockType = CameraLockageType.lockObjectToCam;
            }

            uiCameraOperation?.Invoke(this, IsCamLocked());
        }

        //!
        //! Toggles the safe frame overlay.
        //!
        private void showSafeFrame()
        {

            if (m_safeFrame == null)
            {
                m_safeFrame = GameObject.Instantiate(m_safeFramePrefab, Camera.main.transform);
                CanvasScaler scaler =  m_safeFrame.GetComponent<CanvasScaler>();
                float physicalDeviceScale = Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height) / Screen.dpi / 12f;
                scaler.scaleFactor = Screen.dpi * 0.04f * Mathf.Min(Mathf.Max(manager.settings.uiScale.value, 0.4f), 3f) * physicalDeviceScale;
                m_infoText = m_safeFrame.transform.FindDeepChild("InfoText").GetComponent<TextMeshProUGUI>();
                m_scaler = m_safeFrame.transform.Find("scaler");
                if (m_oldSOCamera)
                    updateCamera(m_oldSOCamera, null);
            }
            else
            {
                GameObject.DestroyImmediate(m_safeFrame);
                m_safeFrame = null;
            }
        }

        //!
        //! update safeFrame
        //!
        private void updateSafeFrame(object sender, InputManager.CameraControl c)
        {
            if (c == InputManager.CameraControl.AR)
            {
                m_safeFrameButton.showHighlighted(false);
                GameObject.DestroyImmediate(m_safeFrame);
                m_safeFrame = null;
                core.getManager<UIManager>().removeButton(m_safeFrameButton);

            }
            else
            {
                if(!core.getManager<UIManager>().getButtons().Contains(m_safeFrameButton))
                    core.getManager<UIManager>().addButton(m_safeFrameButton);
            }
        }
        
        //!
        //! update selectCamera
        //!
        private void updateSelectCamera(object sender, InputManager.CameraControl c)
        {
            if (c == InputManager.CameraControl.AR)
            {
                nextCameraButton.showHighlighted(false);
                core.getManager<UIManager>().removeButton(nextCameraButton);

            }
            else
            {
                if(!core.getManager<UIManager>().getButtons().Contains(nextCameraButton))
                    core.getManager<UIManager>().addButton(nextCameraButton);
            }
        }

        //!
        //! Function for updating the aspect ratio of the safe frame based on the currently selected camera.
        //!
        private void updateCamera(object sender, AbstractParameter parameter)
        {
            Camera cameraMain = Camera.main;
            SceneObjectCamera soCamera = (SceneObjectCamera) sender;

            cameraMain.fieldOfView = soCamera.fov.value;
            cameraMain.sensorSize = soCamera.sensorSize.value;

            if (m_safeFrame)
            {
                string camInfo = String.Format("{0}mm | f/{1} | {2}:{3}mm | {4:0.00} fps", cameraMain.focalLength, soCamera.aperture.value, cameraMain.sensorSize.x, cameraMain.sensorSize.y, 1.0f / Time.deltaTime);
                float newAspect = cameraMain.sensorSize.x / cameraMain.sensorSize.y;

                if (newAspect < cameraMain.aspect)
                    m_scaler.localScale = new Vector3(1f / cameraMain.aspect * (cameraMain.sensorSize.x / cameraMain.sensorSize.y), 1f, 1f);
                else
                    m_scaler.localScale = new Vector3(1f, cameraMain.aspect / (cameraMain.sensorSize.x / cameraMain.sensorSize.y), 1f);

                m_infoText.text = camInfo;
            }
        }

        //!
        //! The function that cycles through the available cameras in scene and set the camera main transform to these camera transform. 
        //!
        private void selectNextCamera()
        {
            m_uiManager.clearSelectedObject();
            m_cameraIndex++;

            if (IsCamLocked())
            {
                UnlockCam();

                uiCameraOperation?.Invoke(this, IsCamLocked());
                m_cameraSelectButton.showHighlighted(false);
            }

            if (m_cameraIndex > m_sceneManager.sceneCameraList.Count - 1)
                m_cameraIndex = 0;

            // copy properties to main camera and set it use display 1 (0)
            copyCamera();

            InputManager inputManager = core.getManager<InputManager>();
            if (inputManager.cameraControl == InputManager.CameraControl.ATTITUDE)
                inputManager.setCameraAttitudeOffsets();
        }

        //!
        //! Function that copies the selected cameras attributes to the main camera.
        //!
        private void copyCamera(){
            if (m_sceneManager.sceneCameraList.Count <= 0 || m_cameraIndex >= m_sceneManager.sceneCameraList.Count)
                return;
        
            if (m_oldSOCamera)
                m_oldSOCamera.hasChanged -= updateCamera;
            
            Camera mainCamera = Camera.main;
            int targetDisplay = mainCamera.targetDisplay;
            float aspect = mainCamera.aspect;
            SceneObjectCamera soCamera = m_sceneManager.sceneCameraList[m_cameraIndex];
            Camera newCamera = soCamera.GetComponent<Camera>();
            soCamera.hasChanged += updateCamera;
            Debug.Log(soCamera.name + " Camera linked.");
            m_oldSOCamera = soCamera;
            mainCamera.enabled = false;
            mainCamera.CopyFrom(newCamera);
            mainCamera.targetDisplay = targetDisplay;
            mainCamera.aspect = aspect;
            mainCamera.enabled = true;

            updateCamera(soCamera, null);

            // announce the UI operation to the input manager
            m_inputManager.updateCameraCommand();
        }

        //!
        //! Function that copies the first camera's attributes to the main camera once
        //!
        private void initCameraOnce(object sender, EventArgs e)
        {
            if (m_sceneManager.sceneCameraList.Count <= 0)
                return;

            Camera mainCamera = Camera.main;
            float aspect = mainCamera.aspect;
            SceneObjectCamera soCamera = m_sceneManager.sceneCameraList[0];
            mainCamera.enabled = false;
            mainCamera.CopyFrom(soCamera.GetComponent<Camera>());
            mainCamera.aspect = aspect;
            mainCamera.enabled = true;

            updateCamera(soCamera, null);
        }

        //!
        //! Function that updates based on the main cameras transformation the selectet objects transformation by using a look through metaphor.
        //!
        private void updateLookThrough(object sender, EventArgs e)
        {
            if(!m_selectedObject)
                return;

            Transform camTransform = Camera.main.transform;
            Transform objTransform = m_selectedObject.transform;
            Vector3 newPosition;
            Quaternion newRotation;

            switch (m_inputManager.cameraControl)
            {
                case InputManager.CameraControl.ATTITUDE: 
                case InputManager.CameraControl.AR:
                case InputManager.CameraControl.TOUCH:
                   // newPosition = camTransform.position - objTransform.parent.position;
                    //newRotation = camTransform.rotation * Quaternion.Inverse(objTransform.parent.rotation);
                    if (objTransform.parent.name != "Scene")
                    {
                        newPosition = objTransform.parent.InverseTransformPoint(camTransform.position);
                        newRotation = Quaternion.Inverse(objTransform.parent.rotation) * camTransform.rotation;
                    }
                    else
                    {
                        newPosition = camTransform.position;
                        newRotation = camTransform.rotation;
                    }
                    m_selectedObject.position.setValue(newPosition);
                    m_selectedObject.rotation.setValue(newRotation);
                    break;
                case InputManager.CameraControl.NONE:
                    //do the same here right now, because the behaviour seems to be set to None from time to time (specifically AR mode did not work well anymore)
                    //newPosition = camTransform.position - objTransform.parent.position;
                    //newRotation = camTransform.rotation * Quaternion.Inverse(objTransform.parent.rotation);
                    if (objTransform.parent.name != "Scene")
                    {
                        newPosition = objTransform.parent.InverseTransformPoint(camTransform.position);
                        newRotation = Quaternion.Inverse(objTransform.parent.rotation) * camTransform.rotation;
                    }
                    else
                    {
                        newPosition = camTransform.position;
                        newRotation = camTransform.rotation;
                    }
                    m_selectedObject.position.setValue(newPosition);
                    m_selectedObject.rotation.setValue(newRotation);
                    break;
                default:
                    break;
            }
        }

        //!
        //! Function that updates based on the main cameras transformation the selectet objects transformation by using a grab and move metaphor.
        //!
        private void updateLockToCamera(object sender, EventArgs e)
        {
            if(!m_selectedObject)
                return;

            switch (m_inputManager.cameraControl)
            {
                case InputManager.CameraControl.ATTITUDE:
                case InputManager.CameraControl.AR:
                case InputManager.CameraControl.TOUCH:
                case InputManager.CameraControl.NONE:
                    Vector3 localToWorldPos = Camera.main.transform.TransformPoint(m_localPositionWouldBe);
                    
                    Quaternion localToWorldRot = Camera.main.transform.rotation * m_localRotationWouldBe;
                    //apply the stored local rotation from the camera into world space 
                    //BEWARE matrix multiplication - order matters!
                    
                    //BEWARE: these will set a localPosition AND localRotation - therefore, transform it once again
                    if(m_selectedObject.transform.parent){
                        Vector3 worldToLocalParentPos = m_selectedObject.transform.parent.InverseTransformPoint(localToWorldPos);
                        Quaternion worldToLocalParentRot = Quaternion.Inverse(m_selectedObject.transform.parent.rotation) * localToWorldRot;
                        m_selectedObject.position.setValue(worldToLocalParentPos);
                        m_selectedObject.rotation.setValue(worldToLocalParentRot);
                    }else{
                        m_selectedObject.position.setValue(localToWorldPos);
                        m_selectedObject.rotation.setValue(localToWorldRot);
                    }
                    
                    break;
                default:
                    break;
            }
        }
    }
}
