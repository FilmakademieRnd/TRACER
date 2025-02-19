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

//! @file "UICreator2DModule.cs"
//! @brief implementation of 2D manipulator UI module
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @author Justus Henne
//! @author Paulo Scatena
//! @version 0
//! @date 14.02.2022

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace tracer
{
    //!
    //! implementation of TRACER 2D UI scene creator module
    //!
    public class UICreator2DModule : UIManagerModule
    {
        //Currently displayed manipulator (can be null if none is displayed)
        GameObject currentManipulator;

        public GameObject GetManipulator()
        {
            return currentManipulator;
        }

        //Currently displayed AddSelector (can be null if none is displayed)
        GameObject currentAddSelector;
        
        public GameObject currentAddSelectorGetter
        {
            get => currentAddSelector;
        }

        //Button for additional parameters, hidden if currentAddSelector is active
        GameObject currentAddButton;

        //List of selection Buttons for Manipulators
        private List<GameObject> instancedManipulatorSelectors = new List<GameObject>();

        //!
        //! Event emitted when parameter has changed
        //!
        public event EventHandler<int> parameterChanged;

        //public event EventHandler<ColorSelect> colorSelectActive; 

        private Transform UI2D;

        public Transform UI2DCanvas
        {
            get => UI2D;
        }
        
        private Transform manipulatorPanel;
        private Transform undoRedoPanel;
        private Button undoButton;
        private Button redoButton;
        private Button resetButton;

        public bool blocksRaycasts = true;

        //!
        //! currently selected SceneObject
        //!
        private List<SceneObject> selectedSceneObjects;
        private SceneObject mainSelection;

        //!
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public UICreator2DModule(string name, Manager manager) : base(name, manager)
        {
            GameObject canvas = Resources.Load<GameObject>("Prefabs/PRE_Canvas_2DUI");
            Transform canvasTrans = SceneObject.Instantiate(canvas).transform;
            canvasTrans.name = "Canvas_2DUI";

            UI2D = canvasTrans.GetChild(0).GetChild(0).transform;
            manipulatorPanel = UI2D.GetChild(0);
            undoRedoPanel = UI2D.GetChild(1);
            undoButton = UI2D.GetChild(1).GetChild(1).GetComponent<Button>();
            redoButton = UI2D.GetChild(1).GetChild(2).GetComponent<Button>();
            resetButton = UI2D.GetChild(1).GetChild(3).GetComponent<Button>();
            undoButton.onClick.AddListener(() => core.getManager<SceneManager>().getModule<UndoRedoModule>().undoStep());
            redoButton.onClick.AddListener(() => core.getManager<SceneManager>().getModule<UndoRedoModule>().redoStep());
            resetButton.onClick.AddListener(() => resetCurrentSceneObjects());

            HideMenu();
        }

        //!
        //! Cleanup
        //!
        protected override void Cleanup(object sender, EventArgs e)
        {
            base.Cleanup(sender, e);
            undoButton.onClick.RemoveAllListeners();
            redoButton.onClick.RemoveAllListeners();
            resetButton.onClick.RemoveAllListeners();
        }

        //!
        //! Init m_callback for the UICreator2D module.
        //! Called after constructor.
        //! @param sender m_callback sender
        //! @param e event reference
        //!
        protected override void Init(object sender, EventArgs e)
        {
            manager.selectionChanged += createUI;
            resetButton.GetComponent<LongPressButton>().longPress += manager.core.getManager<SceneManager>().getModule<UndoRedoModule>().resetScene;

            ColorBlock buttonColors = undoButton.colors;
            Color newColor = manager.uiAppearanceSettings.colors.ElementSelection_Default;
            buttonColors.highlightedColor = newColor;
            buttonColors.normalColor = newColor;
            buttonColors.selectedColor = newColor;

            undoButton.colors = buttonColors;
            redoButton.colors = buttonColors;
            resetButton.colors = buttonColors;

            undoButton.GetComponentInChildren<TextMeshProUGUI>().color = newColor;
            redoButton.GetComponentInChildren<TextMeshProUGUI>().color = newColor;
            resetButton.GetComponentInChildren<TextMeshProUGUI>().color = newColor;
        }

        //!
        //! Function that recreates the UI Layout.
        //! Being called when selection has changed.
        //! @param sender m_callback sender
        //! @param sceneObjects event payload containg all sceneObjects the UI shall be created for
        //!
        private void createUI(object sender, List<SceneObject> sceneObjects)
        {

            clearUI();

            if (sceneObjects.Count < 1)
            {
                return;
            }

            ShowMenu();

            //TODO Account for more than the first sceneObject being selected
            selectedSceneObjects = sceneObjects;
            mainSelection = selectedSceneObjects[0];

            GameObject spinnerPrefab = Resources.Load<GameObject>("Prefabs/PRE_UI_AddSelector");
            currentAddSelector = SceneObject.Instantiate(spinnerPrefab, UI2D);
            SnapSelect snapSelect = currentAddSelector.GetComponent<SnapSelect>();
            snapSelect.uiSettings = manager.uiAppearanceSettings;
            snapSelect.manager = manager;

            int lengthToShow = mainSelection.parameterList.Count;
            //if SceneObjectCharacter - DONT SHOW BONES
            if(mainSelection.GetType() == typeof(SceneCharacterObject))
                lengthToShow = 5; //TRS (012) + PathPos (3) + PathRot (4)

            for (int i = 0; i < lengthToShow; i++)
            {
                switch (mainSelection.parameterList[i].name)
                {
                    //translation
                    case "position":
                        snapSelect.addElement(Resources.Load<Sprite>("Images/button_translate"));
                        break;
                    //rotation
                    case "rotation":
                        snapSelect.addElement(Resources.Load<Sprite>("Images/button_rotate"));
                        break;
                    //scale
                    case "scale":
                        snapSelect.addElement(Resources.Load<Sprite>("Images/button_scale"));
                        break;
                    case "intensity":
                        snapSelect.addElement(Resources.Load<Sprite>("Images/button_intensity"));
                        break;
                    case "color":
                        snapSelect.addElement(Resources.Load<Sprite>("Images/button_color"));
                        break;
                    case "range":
                        snapSelect.addElement(Resources.Load<Sprite>("Images/button_range"));
                        break;
                    case "aperture":
                        snapSelect.addElement(Resources.Load<Sprite>("Images/button_aperture"));
                        break;
                    case "aspectRatio":
                        snapSelect.addElement(Resources.Load<Sprite>("Images/button_aspect"));
                        break;
                    case "radius":
                        snapSelect.addElement(Resources.Load<Sprite>("Images/button_radius"));
                        break;
                    case "fov":
                        snapSelect.addElement(Resources.Load<Sprite>("Images/button_fov"));
                        break;
                    case "farClipPlane":
                        snapSelect.addElement(Resources.Load<Sprite>("Images/button_farClipPlane_text"));
                        break;
                    case "nearClipPlane":
                        snapSelect.addElement(Resources.Load<Sprite>("Images/button_nearClipPlane_text"));
                        break;
                    case "focalDistance":
                        snapSelect.addElement(Resources.Load<Sprite>("Images/button_focalDistance"));
                        break;
                    case "sensorSize":
                        snapSelect.addElement(Resources.Load<Sprite>("Images/button_sensorSize_text"));
                        break;
                    case "pathPositions":   //was a button like TRS before:
                        snapSelect.addElement(Resources.Load<Sprite>("Images/button_pathPos"));
                        break;
                    case "pathRotations":   //was a button like TRS before: 
                        snapSelect.addElement(Resources.Load<Sprite>("Images/button_pathRot"));
                        break;
                    case "animHostGen":     //RPC we use to trigger the character animation for a given path (both above) in AnimHost
                        //not visualized at all (beware that they still increase to index of these snap elements)
                        break;
                    default:                        
                        snapSelect.addElement(mainSelection.parameterList[i].name);
                        break;
                }
            }
            snapSelect.parameterChanged += createAdditionalManipulator;
            currentAddSelector.SetActive(true);

            createManipulator(0);

            manager.emitUI2DCreated(snapSelect);
        }

        //!
        //! function to delete all 2D UI elements
        //!
        private void clearUI()
        {
            GameObject.DestroyImmediate(currentManipulator);
            GameObject.DestroyImmediate(currentAddSelector);
            if(manipulatorPanel)
                manipulatorPanel.gameObject.SetActive(true);
            if (undoRedoPanel)
                undoRedoPanel.gameObject.SetActive(false);

            foreach (var manipSelec in instancedManipulatorSelectors)
            {
                GameObject.DestroyImmediate(manipSelec);
            }
            instancedManipulatorSelectors.Clear();
        }

        private void createAdditionalManipulator(object sender, int i)
        {
            createManipulator(i);
        }

        //!
        //! function called when manipulator shall be changed
        //! @param index index of the Parameter a Manipulator shall be drawn for
        //!
        public void createManipulator(int index)
        {
            if (parameterChanged != null)
                parameterChanged.Invoke(this, index);
            if (currentManipulator)
                GameObject.Destroy(currentManipulator);

            if (index < 0 && currentAddSelector)
            {
                currentAddButton.SetActive(false);
                currentAddSelector.SetActive(true);
                createManipulator(currentAddSelector.GetComponent<SnapSelect>().currentAxis+3);
                return;
            }
            else if (index < 3 && currentAddSelector)
            {
                //currentAddButton.SetActive(true);
                //currentAddSelector.SetActive(false);
            }

            AbstractParameter abstractParam = mainSelection.parameterList[index];
            AbstractParameter.ParameterType type = abstractParam.tracerType;

            switch (type)
            {
                case AbstractParameter.ParameterType.FLOAT:
                case AbstractParameter.ParameterType.VECTOR2:
                case AbstractParameter.ParameterType.VECTOR3:
                case AbstractParameter.ParameterType.QUATERNION:
                    GameObject spinnerPrefab = Resources.Load<GameObject>("Prefabs/PRE_UI_Spinner");
                    currentManipulator = SceneObject.Instantiate(spinnerPrefab, manipulatorPanel);
                    Manipulator manipSpinner = currentManipulator.GetComponent<Manipulator>();
                    if (manipSpinner){
                        manipSpinner.Init(abstractParam, manager);
                        //SceneObjectMeasurement is only locally, so we dont add these listener
                        if(mainSelection.GetType() != typeof(SceneObjectMeasurement)){
                            manipSpinner.doneEditing += manager.core.getManager<SceneManager>().getModule<UndoRedoModule>().addHistoryStep;
                            manipSpinner.doneEditing += core.getManager<NetworkManager>().getModule<UpdateSenderModule>().queueUndoRedoMessage;
                        }
                    }
                    break;
                case AbstractParameter.ParameterType.COLOR:
                    GameObject resourcePrefab = Resources.Load<GameObject>("Prefabs/PRE_UI_ColorPicker");
                    currentManipulator = SceneObject.Instantiate(resourcePrefab, manipulatorPanel);
                    Manipulator manipColor = currentManipulator.GetComponent<Manipulator>();
                    if (manipColor){
                        manipColor.Init(abstractParam, manager);
                        manipColor.doneEditing += manager.core.getManager<SceneManager>().getModule<UndoRedoModule>().addHistoryStep;
                        manipColor.doneEditing += core.getManager<NetworkManager>().getModule<UpdateSenderModule>().queueUndoRedoMessage;
                    }
                    manager.ColorGameObjectActive(currentManipulator);
                    break;
                case AbstractParameter.ParameterType.BOOL:
                    //TODO: implement and add button (right now used for path creation)
                    GameObject buttonManipulatorPrefab = Resources.Load<GameObject>("Prefabs/PRE_UI_ButtonManipulator");
                    currentManipulator = SceneObject.Instantiate(buttonManipulatorPrefab, manipulatorPanel);
                    ButtonManipulator manipButton = currentManipulator.GetComponent<ButtonManipulator>();
                    if (manipButton){
                        manipButton.Init(abstractParam, manager);
                    }
                    break;
                case AbstractParameter.ParameterType.ACTION:
                case AbstractParameter.ParameterType.INT:
                case AbstractParameter.ParameterType.STRING:
                case AbstractParameter.ParameterType.VECTOR4:
                default:
                    Helpers.Log("No UI for parameter type implemented...");
                    break;

            }

            foreach (GameObject g in instancedManipulatorSelectors)
                g.GetComponent<ManipulatorSelector>().visualizeIdle();

            //if(index < 3)
            //    instancedManipulatorSelectors[index].GetComponent<ManipulatorSelector>().visualizeActive();
        }

        //!
        //! function to enable or disapble 2D UI interactablitity
        //! @param value interactable true/false
        //!
        private void SetInteractable(bool value)
        {
            UI2D.GetComponent<CanvasGroup>().interactable = value;
            UI2D.GetComponent<CanvasGroup>().blocksRaycasts = value ? blocksRaycasts : false;
        }

        //!
        //! resets the currently selected scene objects to it's initial values
        //!
        private void resetCurrentSceneObjects()
        {
            foreach (SceneObject s in selectedSceneObjects)
            {
                foreach (AbstractParameter p in s.parameterList)
                    p.reset();
                core.getManager<SceneManager>().getModule<UndoRedoModule>().vanishHistory(s);
                core.getManager<NetworkManager>().getModule<UpdateSenderModule>().queueResetMessage(s);
            }
        }

        //!
        //! function to set 2D UI alpha
        //! @param value alpha value
        //!
        private void SetAlpha(float value)
        {
            UI2D.GetComponent<CanvasGroup>().alpha = value;
        }

        //!
        //! Show 2D manipulator menu
        //! @param setInteractable sets if menu shall be initially interactable
        //!
        public virtual void ShowMenu(bool setInteractable = true)
        {

            SetAlpha(1f);
            undoRedoPanel.gameObject.SetActive(true);
            if (setInteractable)
            {
                SetInteractable(true);
            }
        }

        //!
        //! Hide 2D manipulator menu
        //! @param setInteractable sets if menu shall be initially interactable
        //!
        public void HideMenu(bool setInteractable = true)
        {
            SetAlpha(0f);
            undoRedoPanel.gameObject.SetActive(false);

            if (setInteractable)
                SetInteractable(false);
        }
    }
}