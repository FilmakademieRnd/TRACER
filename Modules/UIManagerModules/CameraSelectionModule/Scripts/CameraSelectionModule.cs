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
//! @author Thomas Krüger
//! @version 1
//! @date 19.08.2026
//! @revision

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        private UIManager m_uiManager;
        //!
        //! The preloaded prafab of the safe frame overlay game object.
        //!
        private GameObject m_safeFramePrefab;
        //!
        //! The instance of the the safe frame overlay.
        //!
        private GameObject m_safeFrame = null;
        //!
        //! The position of the camera before we clicked the ui button "look through"
        //!
        private Vector3 preLookThroughCamPos;
        //!
        //! The rotation of the camera before we clicked the ui button "look through"
        //!
        private Quaternion preLookThroughCamRot;
        //!
        //! The rect transform of the safe frame's parent.
        //!
        private RectTransform m_sfParentTransform = null;
        //!
        //! The rect transform of the safe frame.
        //!
        private RectTransform m_sfTransform = null;        
        //!
        //! The text of the safe frame.
        //!
        private TextMeshProUGUI m_infoText = null;
        //!
        //! A copy of the last selected camera.
        //!
        private SceneObjectCamera m_oldSOCamera = null;
        //!
        //! Safe frame button
        //!
        //private MenuButton m_safeFrameButton = null;
        //!
        //! Next Camera button
        //!
        private MenuButton m_nextCameraButton;
        //!
        //! The coroutine handling the safe frame update.
        //!
        private Coroutine m_safeFrameUpdateCoroutine;

        //!
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public CameraSelectionModule(string name, Manager manager) : base(name, manager){
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

            m_nextCameraButton = new MenuButton("", showNextCamera, new List<UIManager.Roles>() { UIManager.Roles.DOP });
            m_nextCameraButton.setIcon("Images/button_camera");
            m_nextCameraButton.isToggle = true;

            m_uiManager.addButton(m_nextCameraButton);

            m_sceneManager.sceneReady += initCameraOnce;
            m_uiManager.selectionChanged += selection;

            m_uiManager.cameraControlChanged += cameraControlChanged;
            m_uiManager.cameraControlChanged += updateSelectCamera;
        }

        //! 
        //! Function called before Unity destroys the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        public override void Dispose()
        {
            base.Dispose();

            m_sceneManager.sceneReady -= initCameraOnce;
            m_uiManager.selectionChanged -= selection;
            m_uiManager.cameraControlChanged -= cameraControlChanged;
            m_uiManager.cameraControlChanged -= updateSelectCamera;
        }

        //!
        //! Function that creates the camera selection ui buttons & handles selection changes. Called every time a scene object has been selected.
        //!
        //! @param sender The UI manager.
        //! @param sceneObjects a list of the currently selected objects.
        //!
        private void selection(object sender, List<SceneObject> sceneObjects){
            
            RemoveSafeFrameButton();

            Debug.Log("CameraSelectionModule selection call > 0 "+(sceneObjects.Count > 0));

            if (sceneObjects.Count > 0){
                m_selectedObject = sceneObjects[0];

                CreateCameraSpecificButton();
            }else{

                if (IsCamLocked()){     //UNLOCK AND REVERT
                    RevertLock(false);
                }
                m_selectedObject = null;
            }
        }

        //!
        //! The function that moves the main camera to the selected object (light or cam)
        //!
        private void LockOnLookThrough(){
            SpecificLockArgument(CameraLockageType.lookThrough);
        }

        //!
        //! The function that moves the main camera to the selected object and parants it to the camera.
        //!
        private void LockObjectToCameraView(){
            SpecificLockArgument(CameraLockageType.lockObjectToCam);
        }

        //!
        //! The lock we want to achieve (cam look through + lock OR object lock in cam space)
        //!
        private void SpecificLockArgument(CameraLockageType _lockType) {
            if (m_selectedObject == null)
                return;

            if (IsCamLocked()){
                RevertLock(true);
            }else{

                switch (_lockType) {
                    case CameraLockageType.lookThrough:
                        
                        preLookThroughCamPos = Camera.main.transform.position;
                        preLookThroughCamRot = Camera.main.transform.rotation;

                        Type selectionType = m_selectedObject.GetType();
                        if (selectionType == typeof(SceneObjectCamera)){
                            copyCamera();
                            ShowSafeFrame();
                        }else if (selectionType == typeof(SceneObjectDirectionalLight) || (selectionType == typeof(SceneObjectSpotLight))) {
                            //show specific other safe frame
                        }

                        Camera.main.cullingMask &= ~(1 << 11);
                        
                        if (string.Equals(m_selectedObject.transform.parent.name, "Scene")){
                            Camera.main.transform.position = m_selectedObject.transform.position;
                            Camera.main.transform.rotation = m_selectedObject.transform.rotation;
                        } else {
                            Camera.main.transform.position = m_selectedObject.transform.parent.TransformPoint(m_selectedObject.transform.localPosition);
                            Camera.main.transform.rotation = m_selectedObject.transform.parent.rotation * m_selectedObject.transform.localRotation;
                        }

                        core.updateEvent += updateLookThrough;
                        break;

                    case CameraLockageType.lockObjectToCam:
                        m_localPositionWouldBe = Camera.main.transform.InverseTransformPoint(m_selectedObject.transform.position);
                        //calculate the local rotation by Quaternion.Inverse(target spaces' object rotation) * world rotation of the object
                        //BEWARE matrix multiplication - order matters!
                        m_localRotationWouldBe = Quaternion.Inverse(Camera.main.transform.rotation) * m_selectedObject.transform.rotation;

                        core.updateEvent += updateLockToCamera;
                        break;
                }
                m_lockType = _lockType;
                manager.emitCameraLockObjectChanged(this, IsCamLocked());
                //uiCameraOperation?.Invoke(this, IsCamLocked());
            }
        }

        //!
        //! Unlock the camera and remove the events
        //!
        private void RevertLock(bool viaButton = false) {
            //Specifics
            switch (m_lockType) {
                case CameraLockageType.lookThrough:
                    core.updateEvent -= updateLookThrough;
                        if (m_safeFrameUpdateCoroutine != null)
                            core.StopCoroutine(m_safeFrameUpdateCoroutine);

                    //revert cam to original position (if not canceled by selecting another object)
                    if(viaButton){
                        Camera.main.transform.position = preLookThroughCamPos;  //was -= Camera.main.transform.forward;
                        Camera.main.transform.rotation = preLookThroughCamRot;
                    }
                    break;
                case CameraLockageType.lockObjectToCam:
                    core.updateEvent -= updateLockToCamera;
                    break;
            }
            //General
            RemoveSafeFrame();
            ResetRatio();

            m_lockType = CameraLockageType.none;
            //uiCameraOperation?.Invoke(this, IsCamLocked());
            manager.emitCameraLockObjectChanged(this, IsCamLocked());
        }

        //!
        //! Returns a more readable way whether the camera view is currently locked onto something
        //!
        private bool IsCamLocked(){ return m_lockType != CameraLockageType.none; }

        //!
        //! resets the cams values to standard (and move it one step backwar - most likely because we looked through a camera before)
        //!
        private void ResetRatio(){
            Camera.main.fieldOfView = 60;
            Camera.main.cullingMask = LayerMask.NameToLayer("Everything");
        }

        //!
        //! creates the SafeFrame- or LockToObject button, depending on our selection
        //!
        private void CreateCameraSpecificButton() {
            Type selectionType = m_selectedObject.GetType();
            if (selectionType == typeof(SceneObjectCamera))
            {
                m_cameraIndex = m_sceneManager.sceneCameraList.FindIndex(x => x.Equals((SceneObjectCamera)m_selectedObject));
            }

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

        //!
        //! removes the safe frame button, e.g. if switching to ar or selecting sth else than a camera
        //!
        private void RemoveSafeFrameButton() {
            if (m_cameraSelectButton != null){
                m_uiManager.removeButton(m_cameraSelectButton);
                m_cameraSelectButton = null;
            }
        }

        //!
        //! Shows the safe frame overlay.
        //!
        private void ShowSafeFrame(){
            if (m_safeFrame == null){
                m_safeFrame = GameObject.Instantiate(m_safeFramePrefab, Camera.main.transform);
                CanvasScaler scaler =  m_safeFrame.GetComponent<CanvasScaler>();
                float physicalDeviceScale = Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height) / Screen.dpi / 12f;
                scaler.scaleFactor = Screen.dpi * 0.04f * Mathf.Min(Mathf.Max(manager.settings.uiScale.value, 0.4f), 3f) * physicalDeviceScale;
                m_infoText = m_safeFrame.transform.FindDeepChild("InfoText").GetComponent<TextMeshProUGUI>();
                m_sfParentTransform = m_safeFrame.transform.GetComponent<RectTransform>(); ;
                m_sfTransform = m_safeFrame.transform.Find("scaler").GetComponent<RectTransform>();

                m_safeFrameUpdateCoroutine = core.StartCoroutine(UpdateSafeFrameRoutine());
            }
        }

        //!
        //! Destroys the safe frame overlay and the corresponding coroutine.
        //!
        private void RemoveSafeFrame(){
            if (m_safeFrameUpdateCoroutine != null)
            {
                core.StopCoroutine(m_safeFrameUpdateCoroutine);
                m_safeFrameUpdateCoroutine = null;
            }

            if (m_safeFrame != null)
            {
                GameObject.Destroy(m_safeFrame);
                m_safeFrame = null;
            }

        }

        private IEnumerator UpdateSafeFrameRoutine()
        {
            yield return new WaitForEndOfFrame();             // give the canvas time to init
            while (true)
            {
                updateSafeFrame(m_selectedObject, null);
                yield return new WaitForSeconds(1);
            }
        }

        //!
        //! update safeFrame
        //!
        private void cameraControlChanged(object sender, UIManager.CameraControl c){
            if (c == UIManager.CameraControl.AR){
                RemoveSafeFrame();
                RemoveSafeFrameButton();
            }
        }

        //!
        //! update selectCamera
        //!
        private void updateSelectCamera(object sender, UIManager.CameraControl c)
        {
            if (c == UIManager.CameraControl.AR)
            {
                m_nextCameraButton.showHighlighted(false);
                manager.removeButton(m_nextCameraButton);

            }
            else
            {
                if(!manager.getButtons().Contains(m_nextCameraButton))
                    manager.addButton(m_nextCameraButton);
            }
        }

        //!
        //! Function for updating the aspect ratio of the safe frame based on the currently selected camera.
        //!
        private void updateSafeFrame(object so, AbstractParameter parameter)
        {
            Camera cameraMain = Camera.main;
            SceneObjectCamera soCamera = null;

            if (so != null &&
                so.GetType() == typeof(SceneObjectCamera)) {
                soCamera = (SceneObjectCamera)so;
                cameraMain.fieldOfView = soCamera.fov.value;
                cameraMain.sensorSize = soCamera.sensorSize.value;
            }

            if (m_safeFrame)
            {
                string camInfo = "";
                if (soCamera != null)
                {
                    float focalLength = soCamera.sensorSize.value.y / (2.0f * Mathf.Tan(Mathf.Deg2Rad * soCamera.fov.value * 0.5f));
                    camInfo = String.Format("{0:0.00}mm | f/{1:0.00} | {2:0.00}:{3:0.00}mm | {4:0.00} fps", focalLength, soCamera.aperture.value, cameraMain.sensorSize.x, cameraMain.sensorSize.y, 1.0f / Time.deltaTime);
                }

                float newAspect = cameraMain.sensorSize.x / cameraMain.sensorSize.y;
                float scale = 0.5f * m_sfParentTransform.rect.width * (1.0f - (1f / cameraMain.aspect * (cameraMain.sensorSize.x / cameraMain.sensorSize.y)));
                
                if (newAspect < cameraMain.aspect)
                {
                    //m_scaler.localScale = new Vector3(1f / cameraMain.aspect * (cameraMain.sensorSize.x / cameraMain.sensorSize.y), 1f, 1f);
                    m_sfTransform.offsetMin = new Vector2(scale, m_sfTransform.offsetMin.y);
                    m_sfTransform.offsetMax = new Vector2(-scale, m_sfTransform.offsetMax.y);
                }
                else
                {
                    //m_scaler.localScale = new Vector3(1f, cameraMain.aspect / (cameraMain.sensorSize.x / cameraMain.sensorSize.y), 1f);
                    m_sfTransform.offsetMin = new Vector2(m_sfTransform.offsetMin.x, -scale);
                    m_sfTransform.offsetMax = new Vector2(m_sfTransform.offsetMax.x, scale);
                }

                m_infoText.text = camInfo;
            }
        }

        //!
        //! The function that cycles through the available cameras in scene and set the camera main transform to these camera transform. 
        //!
        private void showNextCamera()
        {
            RemoveSafeFrame();

            m_cameraIndex++;

            if (IsCamLocked()){
                RevertLock(false);

                m_cameraSelectButton.showHighlighted(false);
            }

            if (m_cameraIndex > m_sceneManager.sceneCameraList.Count - 1)
                m_cameraIndex = 0;

            // copy properties to main camera and set it use display 1 (0)
            copyCamera();

            // deselect everything and selct camamera scene object
            manager.clearSelectedObjects();
            manager.addSelectedObject(m_sceneManager.sceneCameraList[m_cameraIndex]);

            // InputManager inputManager = core.getManager<InputManager>();
            // if (inputManager.cameraControl == InputManager.CameraControl.ATTITUDE)
            //     inputManager.setCameraAttitudeOffsets();
        }

        //!
        //! Function that copies the selected cameras attributes to the main camera.
        //!
        private void copyCamera(){
            if (m_sceneManager.sceneCameraList.Count <= 0 || m_cameraIndex >= m_sceneManager.sceneCameraList.Count)
                return;
        
            if (m_oldSOCamera)
                m_oldSOCamera.hasChanged -= updateSafeFrame;
            
            Camera mainCamera = Camera.main;
            int targetDisplay = mainCamera.targetDisplay;
            float aspect = mainCamera.aspect;
            SceneObjectCamera soCamera = m_sceneManager.sceneCameraList[m_cameraIndex];
            Camera newCamera = soCamera.GetComponent<Camera>();
            soCamera.hasChanged += updateSafeFrame;
            m_oldSOCamera = soCamera;
            mainCamera.enabled = false;
            mainCamera.CopyFrom(newCamera);
            mainCamera.targetDisplay = targetDisplay;
            mainCamera.aspect = aspect;
            mainCamera.enabled = true;

            // announce the UI operation to the input manager
//            m_inputManager.updateCameraCommand();
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
        }

        //!
        //! Function that updates the selected object to the camera pos & rot
        //! look through metaphor
        //!
        private void updateLookThrough(object sender, EventArgs e)
        {
            if(!m_selectedObject)
                return;

            Transform camTransform = Camera.main.transform;
            Transform objTransform = m_selectedObject.transform;
            Vector3 newPosition;
            Quaternion newRotation;

            switch (manager.cameraControl){
                case UIManager.CameraControl.ATTITUDE: 
                case UIManager.CameraControl.AR:
                case UIManager.CameraControl.STANDARD:
                   // newPosition = camTransform.position - objTransform.parent.position;
                    //newRotation = camTransform.rotation * Quaternion.Inverse(objTransform.parent.rotation);
                    if (objTransform.parent.name != "Scene"){
                        newPosition = objTransform.parent.InverseTransformPoint(camTransform.position);
                        newRotation = Quaternion.Inverse(objTransform.parent.rotation) * camTransform.rotation;
                    }else{
                        newPosition = camTransform.position;
                        newRotation = camTransform.rotation;
                    }
                    if (m_selectedObject.position.value != newPosition)
                        m_selectedObject.position.setValue(newPosition);
                    if (m_selectedObject.rotation.value != newRotation)
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

            switch (manager.cameraControl)
            {
                case UIManager.CameraControl.ATTITUDE:
                case UIManager.CameraControl.AR:
                case UIManager.CameraControl.STANDARD:
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
