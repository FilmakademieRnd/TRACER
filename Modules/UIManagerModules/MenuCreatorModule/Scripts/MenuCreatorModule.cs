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
//! @brief Implementation of the MenuCreatorModule, creating UI menus based on a menuTree object.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 19.09.2022

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace tracer
{
    public class MenuCreatorModule : UIManagerModule
    {
        //!
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public MenuCreatorModule(string name, Manager manager) : base(name, manager)
        {
        }

        //!
        //! A reference to the previous created menu, null at the beginning.
        //!
        private MenuTree m_oldMenu;
        //!
        //! Prefab for the Unity canvas object.
        //!
        private GameObject m_canvas;
        //!
        //! Prefab for the Unity panel object, used as pacer and background.
        //!
        private GameObject m_panel;
        //!
        //! Prefab for the Unity button object, used as UI element for an action parameter.
        //!
        private GameObject m_button;
        //!
        //! Prefab for the Unity toggle object, used as UI element for an bool parameter.
        //!
        private GameObject m_toggle;
        //!
        //! Prefab for the Unity text object, used as UI element for an string (read only) parameter.
        //!
        private GameObject m_text;
        //!
        //! Prefab for the Unity input field object, used as UI element for an string parameter.
        //!
        private GameObject m_inputField;
        //!
        //! Prefab for the Unity input field object, used as UI element for an string parameter.
        //!
        private GameObject m_numberInputField;
        //!
        //! Prefab for the Unity dropdown object, used as UI element for a list parameter.
        //!
        private GameObject m_dropdown;
        //!
        //! The list containing all UI elemets of the current menu.
        //!
        private List<GameObject> m_uiElements;

        private List<ParameterObject> m_parameterObjects;

        private Dictionary<AbstractParameter, List<GameObject>> m_parameterMapping;

        //!
        //! Init Function
        //!
        protected override void Init(object sender, EventArgs e)
        {
            m_uiElements = new List<GameObject>();
            m_parameterObjects = new List<ParameterObject>();
            m_parameterMapping = new Dictionary<AbstractParameter, List<GameObject>>();

            m_canvas = Resources.Load("Prefabs/MenuCanvas") as GameObject;
            m_panel = Resources.Load("Prefabs/MenuPanel") as GameObject;
            m_button = Resources.Load("Prefabs/MenuButton") as GameObject;
            m_toggle = Resources.Load("Prefabs/MenuToggle") as GameObject;
            m_text = Resources.Load("Prefabs/MenuText") as GameObject;
            m_inputField = Resources.Load("Prefabs/MenuInputField") as GameObject;
            m_numberInputField = Resources.Load("Prefabs/MenuNumberInputField") as GameObject;
            m_dropdown = Resources.Load("Prefabs/MenuDropdown") as GameObject;

            manager.menuSelected += createMenu;
            manager.menuDeselected += hideMenu;
        }

        //!
        //! function called before Unity destroys the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        //!
        public override void Dispose()
        {
            base.Dispose();
            manager.menuSelected -= createMenu;
            manager.menuDeselected -= hideMenu;
        }

        //!
        //! Function creating a menu UI element based on a MenuTree object.
        //!
        //! @param sender A reference to the UI manager.
        //! @param menu A reference to the MenuTree used to create the UI elements of a menu.
        //!
        void createMenu(object sender, MenuTree menu)
        {
            destroyMenu();

            if (menu == null)
            {
                m_oldMenu.visible = false;
                return;
            }

            if (menu.visible && m_oldMenu == menu)
                menu.visible = false;
            else
            {
                GameObject menuCanvas = GameObject.Instantiate(m_canvas);
                menuCanvas.GetComponent<Canvas>().sortingOrder = 15;
                m_uiElements.Add(menuCanvas);
                GameObject rootPanel = menuCanvas.transform.Find("Panel").gameObject;
                //Image imageComponent = menuCanvas.GetComponentInChildren<Image>();
                Image imageComponent = menuCanvas.transform.Find("Panel_Menu").GetComponent<Image>();
                imageComponent.color = manager.uiAppearanceSettings.colors.MenuTitleBG;
                m_uiElements.Add(rootPanel);
                TextMeshProUGUI menuTitle = menuCanvas.transform.FindDeepChild("Text").GetComponent<TextMeshProUGUI>();
                menuTitle.font = manager.uiAppearanceSettings.defaultFont;
                menuTitle.fontSize = manager.uiAppearanceSettings.defaultFontSize + 1;
                menuTitle.color = manager.uiAppearanceSettings.colors.FontColor;
                menuTitle.text = menu.caption;
                GameObject button = menuCanvas.transform.FindDeepChild("Button").gameObject;
                button.GetComponent<Button>().onClick.AddListener(() => manager.hideMenu());

                ScrollRect rect = rootPanel.GetComponent<ScrollRect>();
                foreach (MenuItem p in menu.Items)
                {
                    GameObject gameObject = createMenufromTree(p, rootPanel)[0];
                    m_uiElements.Add(gameObject);
                    if (menu.scrollable)
                        rect.content = gameObject.GetComponent<RectTransform>();
                    else
                        GameObject.Destroy(rect.verticalScrollbar.transform.gameObject);
                }
                rect.verticalScrollbar.transform.SetAsLastSibling();

                menu.visible = true;

            }
            m_oldMenu = menu;
        }

        //!
        //! Function that builds UI menu objecrts by recursively traversing a menuTree, starting at the given menuItem.
        //!
        //! @param item The start item for the tree traversal.
        //! @param parentObject The items _parent Unity GameObject.
        //!
        private List<GameObject> createMenufromTree(MenuItem item, GameObject parentObject)
        {
            List<GameObject> newObjects = new List<GameObject>(4);

            switch (item.Type)
            {
                case MenuItem.IType.HSPLIT:
                    newObjects.Add(GameObject.Instantiate(m_panel, parentObject.transform));
                    HorizontalLayoutGroup horizontalLayout = newObjects[0].AddComponent<HorizontalLayoutGroup>();
                    horizontalLayout.childAlignment = TextAnchor.MiddleCenter;
                    horizontalLayout.childForceExpandHeight = false;
                    horizontalLayout.childForceExpandWidth = false;
                    horizontalLayout.childControlHeight = false;
                    horizontalLayout.childControlWidth = false;
                    horizontalLayout.spacing = 2;
                    break;
                case MenuItem.IType.VSPLIT:
                    newObjects.Add(GameObject.Instantiate(m_panel, parentObject.transform));
                    VerticalLayoutGroup verticalLayout = newObjects[0].AddComponent<VerticalLayoutGroup>();
                    verticalLayout.childAlignment = TextAnchor.MiddleCenter;
                    verticalLayout.childForceExpandHeight = false;
                    verticalLayout.childForceExpandWidth = false;
                    verticalLayout.childControlHeight = true;
                    verticalLayout.childControlWidth = true;
                    verticalLayout.spacing = 2;
                    verticalLayout.padding.top = 3;
                    verticalLayout.padding.bottom = 3;
                    break;
                case MenuItem.IType.SPACE:
                    {
                        newObjects.Add(GameObject.Instantiate(m_panel, parentObject.transform));
                    }
                    break;
                case MenuItem.IType.TEXT:
                    {
                        // prevent wrong scaling if two or more textfields are present 
                        HorizontalLayoutGroup hlGroup = parentObject.transform.GetComponent<HorizontalLayoutGroup>();
                        if (parentObject.transform.childCount > 0)
                            if (hlGroup && parentObject.transform.GetChild(0).GetComponent<TextMeshProUGUI>() != null)
                                hlGroup.childControlWidth = true;
                        
                        newObjects.Add(GameObject.Instantiate(m_text, parentObject.transform));
                        TextMeshProUGUI textComponent = newObjects[0].GetComponent<TextMeshProUGUI>();
                        textComponent.text = ((Parameter<string>)item.Parameter).value;
                        textComponent.color = manager.uiAppearanceSettings.colors.FontColor;
                        textComponent.font = manager.uiAppearanceSettings.defaultFont;
                        textComponent.fontSize = manager.uiAppearanceSettings.defaultFontSize;
                        textComponent.fontStyle = FontStyles.Normal;
                    }
                    break;
                case MenuItem.IType.TEXTSECTION:
                    {
                        newObjects.Add(GameObject.Instantiate(m_text, parentObject.transform));
                        TextMeshProUGUI textComponent = newObjects[0].GetComponent<TextMeshProUGUI>();
                        textComponent.text = ((Parameter<string>)item.Parameter).value;
                        textComponent.color = manager.uiAppearanceSettings.colors.ElementSelection_Highlight;
                        textComponent.font = manager.uiAppearanceSettings.defaultFont;
                        textComponent.fontSize = manager.uiAppearanceSettings.defaultFontSize;
                        textComponent.fontStyle = FontStyles.Bold;
                        textComponent.alignment = TextAlignmentOptions.Midline;
                        textComponent.enableWordWrapping = true;
                    }
                    break;
                case MenuItem.IType.PARAMETER:
                    switch (item.Parameter.tracerType)
                    {
                        case AbstractParameter.ParameterType.ACTION:
                            {
                                newObjects.Add(GameObject.Instantiate(m_button, parentObject.transform));
                                Button button = newObjects[0].GetComponent<Button>();
                                ColorBlock buttonColors = button.colors;
                                buttonColors.pressedColor = manager.uiAppearanceSettings.colors.ElementSelection_Highlight;
                                button.colors = buttonColors;
                                Action parameterAction = ((Parameter<Action>)item.Parameter).value;
                                button.onClick.AddListener(() => parameterAction());
                                button.onClick.AddListener(delegate { ((Parameter<Action>)item.Parameter).InvokeHasChanged(); });
                                TextMeshProUGUI textComponent = newObjects[0].GetComponentInChildren<TextMeshProUGUI>();
                                textComponent.text = item.Parameter.name;
                                textComponent.color = manager.uiAppearanceSettings.colors.FontColor;
                                textComponent.font = manager.uiAppearanceSettings.defaultFont;
                                textComponent.fontSize = manager.uiAppearanceSettings.defaultFontSize;
                                Image imgButton = button.GetComponent<Image>();
                                imgButton.color = manager.uiAppearanceSettings.colors.ButtonBG;
                            }
                            break;
                        case AbstractParameter.ParameterType.BOOL:
                            {
                                newObjects.Add(GameObject.Instantiate(m_toggle, parentObject.transform));
                                Toggle toggle = newObjects[0].GetComponent<Toggle>();
                                toggle.isOn = ((Parameter<bool>)item.Parameter).value;
                                toggle.onValueChanged.AddListener(delegate { ((Parameter<bool>)item.Parameter).setValue(toggle.isOn); });
                                /*Text textComponent = newObject.GetComponentInChildren<Text>();
                                textComponent.text = item.Parameter.name;
                                textComponent.color = manager.uiAppearanceSettings.colors.FontColor;
                                textComponent.fontSize = manager.uiAppearanceSettings.defaultFontSize;*/
                                ColorBlock toggleColors = toggle.colors;
                                toggleColors.normalColor = manager.uiAppearanceSettings.colors.DropDown_TextfieldBG;
                                toggleColors.highlightedColor = manager.uiAppearanceSettings.colors.DropDown_TextfieldBG;
                                toggleColors.pressedColor = manager.uiAppearanceSettings.colors.DropDown_TextfieldBG;
                                toggleColors.selectedColor = manager.uiAppearanceSettings.colors.DropDown_TextfieldBG;
                                toggleColors.disabledColor = manager.uiAppearanceSettings.colors.DropDown_TextfieldBG;
                                toggle.colors = toggleColors;
                            }
                            break;
                        case AbstractParameter.ParameterType.INT:
                            {
                                newObjects.Add(GameObject.Instantiate(m_numberInputField, parentObject.transform));
                                TMP_InputField numberInputField = newObjects[0].GetComponent<TMP_InputField>();
                                numberInputField.text = ((Parameter<int>)item.Parameter).value.ToString();
                                numberInputField.onEndEdit.AddListener(delegate { ((Parameter<int>)item.Parameter).setValue(Mathf.RoundToInt(float.Parse(numberInputField.text))); numberInputField.text = ((Parameter<int>)item.Parameter).value.ToString(); });
                                Image imgButton = numberInputField.GetComponent<Image>();
                                imgButton.color = manager.uiAppearanceSettings.colors.DropDown_TextfieldBG;
                                numberInputField.textComponent.color = manager.uiAppearanceSettings.colors.FontColor;
                                numberInputField.textComponent.font = manager.uiAppearanceSettings.defaultFont;
                                numberInputField.textComponent.fontSize = manager.uiAppearanceSettings.defaultFontSize;
                                numberInputField.caretPosition = numberInputField.text.Length;
                            }
                            break;
                        case AbstractParameter.ParameterType.FLOAT:
                            {
                                newObjects.Add(GameObject.Instantiate(m_numberInputField, parentObject.transform));
                                TMP_InputField numberInputField = newObjects[0].GetComponent<TMP_InputField>();
                                numberInputField.text = ((Parameter<float>)item.Parameter).value.ToString();
                                numberInputField.onEndEdit.AddListener(delegate { ((Parameter<float>)item.Parameter).setValue(float.Parse(numberInputField.text)); });
                                Image imgButton = numberInputField.GetComponent<Image>();
                                imgButton.color = manager.uiAppearanceSettings.colors.DropDown_TextfieldBG;
                                numberInputField.textComponent.color = manager.uiAppearanceSettings.colors.FontColor;
                                numberInputField.textComponent.font = manager.uiAppearanceSettings.defaultFont;
                                numberInputField.textComponent.fontSize = manager.uiAppearanceSettings.defaultFontSize;
                                numberInputField.caretPosition = numberInputField.text.Length;
                            }
                            break;
                        case AbstractParameter.ParameterType.VECTOR2:
                            {
                                TMP_InputField[] numberInputFields = new TMP_InputField[2];

                                for (int i = 0; i < 2; i++)
                                {
                                    GameObject go = GameObject.Instantiate(m_numberInputField, parentObject.transform);
                                    numberInputFields[i] = go.GetComponent<TMP_InputField>();
                                    newObjects.Add(go);
                                }

                                Vector2 vectorValue = ((Parameter<Vector2>)item.Parameter).value;

                                for (int i = 0; i < 2; i++)
                                {
                                    numberInputFields[i].text = vectorValue[i].ToString();
                                    int index = i; // Capture current index for delegate

                                    numberInputFields[i].onEndEdit.AddListener(delegate
                                    {
                                        vectorValue[index] = float.Parse(numberInputFields[index].text);
                                        ((Parameter<Vector2>)item.Parameter).setValue(vectorValue);
                                    });

                                    // Set appearance
                                    Image imgButton = numberInputFields[i].GetComponent<Image>();
                                    imgButton.color = manager.uiAppearanceSettings.colors.DropDown_TextfieldBG;
                                    numberInputFields[i].textComponent.color = manager.uiAppearanceSettings.colors.FontColor;
                                    numberInputFields[i].textComponent.font = manager.uiAppearanceSettings.defaultFont;
                                    numberInputFields[i].textComponent.fontSize = manager.uiAppearanceSettings.defaultFontSize;
                                    numberInputFields[i].caretPosition = numberInputFields[i].text.Length;
                                }
                            }
                            break;
                        case AbstractParameter.ParameterType.VECTOR3:
                            {
                                TMP_InputField[] numberInputFields = new TMP_InputField[3];

                                for (int i = 0; i < 3; i++)
                                {
                                    GameObject go = GameObject.Instantiate(m_numberInputField, parentObject.transform);
                                    numberInputFields[i] = go.GetComponent<TMP_InputField>();
                                    newObjects.Add(go);
                                }

                                Vector3 vectorValue = ((Parameter<Vector3>)item.Parameter).value;

                                for (int i = 0; i < 3; i++)
                                {
                                    numberInputFields[i].text = vectorValue[i].ToString();
                                    int index = i; // Capture current index for delegate

                                    numberInputFields[i].onEndEdit.AddListener(delegate
                                    {
                                        vectorValue[index] = float.Parse(numberInputFields[index].text);
                                        ((Parameter<Vector3>)item.Parameter).setValue(vectorValue);
                                    });

                                    // Set appearance
                                    Image imgButton = numberInputFields[i].GetComponent<Image>();
                                    imgButton.color = manager.uiAppearanceSettings.colors.DropDown_TextfieldBG;
                                    numberInputFields[i].textComponent.color = manager.uiAppearanceSettings.colors.FontColor;
                                    numberInputFields[i].textComponent.font = manager.uiAppearanceSettings.defaultFont;
                                    numberInputFields[i].textComponent.fontSize = manager.uiAppearanceSettings.defaultFontSize;
                                    numberInputFields[i].caretPosition = numberInputFields[i].text.Length;
                                }
                            }
                            break;
                        case AbstractParameter.ParameterType.VECTOR4:
                            {
                                TMP_InputField[] numberInputFields = new TMP_InputField[4];

                                for (int i = 0; i < 4; i++)
                                {
                                    GameObject go = GameObject.Instantiate(m_numberInputField, parentObject.transform);
                                    numberInputFields[i] = go.GetComponent<TMP_InputField>();
                                    newObjects.Add(go);
                                }

                                Vector4 vectorValue = ((Parameter<Vector4>)item.Parameter).value;

                                for (int i = 0; i < 4; i++)
                                {
                                    numberInputFields[i].text = vectorValue[i].ToString();
                                    int index = i; // Capture current index for delegate

                                    numberInputFields[i].onEndEdit.AddListener(delegate
                                    {
                                        vectorValue[index] = float.Parse(numberInputFields[index].text);
                                        ((Parameter<Vector4>)item.Parameter).setValue(vectorValue);
                                    });

                                    // Set appearance
                                    Image imgButton = numberInputFields[i].GetComponent<Image>();
                                    imgButton.color = manager.uiAppearanceSettings.colors.DropDown_TextfieldBG;
                                    numberInputFields[i].textComponent.color = manager.uiAppearanceSettings.colors.FontColor;
                                    numberInputFields[i].textComponent.font = manager.uiAppearanceSettings.defaultFont;
                                    numberInputFields[i].textComponent.fontSize = manager.uiAppearanceSettings.defaultFontSize;
                                    numberInputFields[i].caretPosition = numberInputFields[i].text.Length;
                                }
                            }
                            break;
                        case AbstractParameter.ParameterType.QUATERNION:
                            {
                                TMP_InputField[] numberInputFields = new TMP_InputField[4];

                                for (int i = 0; i < 4; i++)
                                {
                                    GameObject go = GameObject.Instantiate(m_numberInputField, parentObject.transform);
                                    numberInputFields[i] = go.GetComponent<TMP_InputField>();
                                    newObjects.Add(go);
                                }

                                Quaternion quaternionValue = ((Parameter<Quaternion>)item.Parameter).value;

                                for (int i = 0; i < 4; i++)
                                {
                                    numberInputFields[i].text = quaternionValue[i].ToString();
                                    int index = i; // Capture current index for delegate

                                    numberInputFields[i].onEndEdit.AddListener(delegate
                                    {
                                        quaternionValue[index] = float.Parse(numberInputFields[index].text);
                                        ((Parameter<Quaternion>)item.Parameter).setValue(quaternionValue);
                                    });

                                    // Set appearance
                                    Image imgButton = numberInputFields[i].GetComponent<Image>();
                                    imgButton.color = manager.uiAppearanceSettings.colors.DropDown_TextfieldBG;
                                    numberInputFields[i].textComponent.color = manager.uiAppearanceSettings.colors.FontColor;
                                    numberInputFields[i].textComponent.font = manager.uiAppearanceSettings.defaultFont;
                                    numberInputFields[i].textComponent.fontSize = manager.uiAppearanceSettings.defaultFontSize;
                                    numberInputFields[i].caretPosition = numberInputFields[i].text.Length;
                                }
                            }
                            break;
                        case AbstractParameter.ParameterType.COLOR:
                            {
                                TMP_InputField[] numberInputFields = new TMP_InputField[4];

                                for (int i = 0; i < 4; i++)
                                {
                                    GameObject go = GameObject.Instantiate(m_numberInputField, parentObject.transform);
                                    numberInputFields[i] = go.GetComponent<TMP_InputField>();
                                    newObjects.Add(go);
                                }

                                Color colorValue = ((Parameter<Color>)item.Parameter).value;

                                for (int i = 0; i < 4; i++)
                                {
                                    numberInputFields[i].text = colorValue[i].ToString();
                                    int index = i; // Capture current index for delegate

                                    numberInputFields[i].onEndEdit.AddListener(delegate
                                    {
                                        float parsedValue = float.Parse(numberInputFields[index].text);
                                        float clampedValue = Mathf.Clamp(parsedValue, 0, 255);
                                        colorValue[index] = clampedValue;
                                        ((Parameter<Color>)item.Parameter).setValue(colorValue);
                                        numberInputFields[index].text = colorValue[index].ToString();
                                    });

                                    // Set appearance
                                    Image imgButton = numberInputFields[i].GetComponent<Image>();
                                    imgButton.color = manager.uiAppearanceSettings.colors.DropDown_TextfieldBG;
                                    numberInputFields[i].textComponent.color = manager.uiAppearanceSettings.colors.FontColor;
                                    numberInputFields[i].textComponent.font = manager.uiAppearanceSettings.defaultFont;
                                    numberInputFields[i].textComponent.fontSize = manager.uiAppearanceSettings.defaultFontSize;
                                    numberInputFields[i].caretPosition = numberInputFields[i].text.Length;
                                }
                            }
                            break;
                        case AbstractParameter.ParameterType.STRING:
                            {
                                newObjects.Add(GameObject.Instantiate(m_inputField, parentObject.transform));
                                TMP_InputField inputField = newObjects[0].GetComponent<TMP_InputField>();
                                inputField.text = ((Parameter<string>)item.Parameter).value;
                                inputField.onEndEdit.AddListener(delegate { ((Parameter<string>)item.Parameter).setValue(inputField.text); });
                                Image imgButton = inputField.GetComponent<Image>();
                                imgButton.color = manager.uiAppearanceSettings.colors.DropDown_TextfieldBG;
                                inputField.textComponent.color = manager.uiAppearanceSettings.colors.FontColor;
                                inputField.textComponent.font = manager.uiAppearanceSettings.defaultFont;
                                inputField.textComponent.fontSize = manager.uiAppearanceSettings.defaultFontSize;
                                inputField.selectionColor = manager.uiAppearanceSettings.colors.ElementSelection_Highlight;
                            }
                            break;
                        case AbstractParameter.ParameterType.LIST:
                            {
                                newObjects.Add(GameObject.Instantiate(m_dropdown, parentObject.transform));
                                TMP_Dropdown dropDown = newObjects[0].GetComponent<TMP_Dropdown>();
                                List<string> names = new List<string>();

                                foreach (AbstractParameter parameter in ((ListParameter)item.Parameter).parameterList)
                                    names.Add(parameter.name);

                                dropDown.AddOptions(names);
                                dropDown.value = ((ListParameter)item.Parameter).value;
                                dropDown.onValueChanged.AddListener(delegate { ((ListParameter)item.Parameter).select(dropDown.value); });

                                dropDown.image.color = manager.uiAppearanceSettings.colors.DropDown_TextfieldBG;
                                dropDown.captionText.color = manager.uiAppearanceSettings.colors.FontColor;
                                dropDown.captionText.font = manager.uiAppearanceSettings.defaultFont;
                                dropDown.captionText.fontSize = manager.uiAppearanceSettings.defaultFontSize;
                                dropDown.itemText.color = manager.uiAppearanceSettings.colors.FontColor;
                                dropDown.itemText.font = manager.uiAppearanceSettings.defaultFont;
                                dropDown.itemText.fontSize = manager.uiAppearanceSettings.defaultFontSize;
                                ColorBlock ddColors = dropDown.colors;
                                ddColors.pressedColor = manager.uiAppearanceSettings.colors.ElementSelection_Highlight;
                                dropDown.colors = ddColors;
                                foreach (UnityEngine.UI.Image DD_image in dropDown.GetComponentsInChildren<Image>(true))
                                    DD_image.color = manager.uiAppearanceSettings.colors.DropDown_TextfieldBG;
                            }
                            break;
                    }
                    break;
            }


            if (item.Parameter != null)
            {
                ParameterObject parameterObject = item.Parameter._parent;
                if (parameterObject != null)
                {
                    if (!m_parameterObjects.Contains(parameterObject))
                    {
                        parameterObject.hasChanged += updateItem;
                        m_parameterObjects.Add(parameterObject);
                        m_parameterMapping.Add(item.Parameter, newObjects);
                    }
                }
            }

            foreach (MenuItem p in item.Children)
                m_uiElements.AddRange(createMenufromTree(p, newObjects[0]));

            return newObjects;
        }

        private void updateItem(object sender, AbstractParameter parameter)
        {
            List<GameObject> gameObjects = m_parameterMapping[parameter];

            if (gameObjects != null)
            {
                switch (parameter.tracerType)
                {
                    case AbstractParameter.ParameterType.ACTION:
                        {
                            //Button button = newObject.GetComponent<Button>();
                            //Action parameterAction = ((Parameter<Action>)item.Parameter).value;
                            //button.onClick.AddListener(() => parameterAction());
                            //TextMeshProUGUI textComponent = newObject.GetComponentInChildren<TextMeshProUGUI>();
                            //textComponent.text = item.Parameter.name;
                            //textComponent.color = manager.uiAppearanceSettings.colors.FontColor;
                            //textComponent.font = manager.uiAppearanceSettings.defaultFont;
                            //textComponent.fontSize = manager.uiAppearanceSettings.defaultFontSize;
                            //Image imgButton = button.GetComponent<Image>();
                            //imgButton.color = manager.uiAppearanceSettings.colors.ButtonBG;
                        }
                        break;
                    case AbstractParameter.ParameterType.BOOL:
                        {
                            Toggle toggle = gameObjects[0].GetComponent<Toggle>();
                            toggle.isOn = ((Parameter<bool>)parameter).value;
                        }
                        break;
                    case AbstractParameter.ParameterType.INT:
                        {
                            TMP_InputField numberInputField = gameObjects[0].GetComponent<TMP_InputField>();
                            numberInputField.text = ((Parameter<int>)parameter).value.ToString();
                        }
                        break;
                    case AbstractParameter.ParameterType.FLOAT:
                        {
                            TMP_InputField numberInputField = gameObjects[0].GetComponent<TMP_InputField>();
                            numberInputField.text = ((Parameter<float>)parameter).value.ToString();
                        }
                        break;
                    case AbstractParameter.ParameterType.VECTOR2:
                        {
                            Vector2 vectorValue = ((Parameter<Vector2>)parameter).value;

                            for (int i = 0; i < 2; i++)
                            {
                                TMP_InputField numberInputField = gameObjects[i].GetComponent<TMP_InputField>();
                                numberInputField.text = vectorValue[i].ToString();
                            }
                        }
                        break;
                    case AbstractParameter.ParameterType.VECTOR3:
                        {
                            Vector3 vectorValue = ((Parameter<Vector3>)parameter).value;

                            for (int i = 0; i < 3; i++)
                            {
                                TMP_InputField numberInputField = gameObjects[i].GetComponent<TMP_InputField>();
                                numberInputField.text = vectorValue[i].ToString();
                            }
                        }
                        break;
                    case AbstractParameter.ParameterType.VECTOR4:
                        {
                            Vector4 vectorValue = ((Parameter<Vector4>)parameter).value;

                            for (int i = 0; i < 4; i++)
                            {
                                TMP_InputField numberInputField = gameObjects[i].GetComponent<TMP_InputField>();
                                numberInputField.text = vectorValue[i].ToString();
                            }
                        }
                        break;
                    case AbstractParameter.ParameterType.QUATERNION:
                        {
                            Quaternion vectorValue = ((Parameter<Quaternion>)parameter).value;

                            for (int i = 0; i < 4; i++)
                            {
                                TMP_InputField numberInputField = gameObjects[i].GetComponent<TMP_InputField>();
                                numberInputField.text = vectorValue[i].ToString();
                            }
                        }
                        break;
                    case AbstractParameter.ParameterType.COLOR:
                        {
                            Color vectorValue = ((Parameter<Color>)parameter).value;

                            for (int i = 0; i < 4; i++)
                            {
                                TMP_InputField numberInputField = gameObjects[i].GetComponent<TMP_InputField>();
                                numberInputField.text = vectorValue[i].ToString();
                            }
                        }
                        break;
                    case AbstractParameter.ParameterType.STRING:
                        {
                            TMP_InputField inputField = gameObjects[0].GetComponent<TMP_InputField>();
                            inputField.text = ((Parameter<string>)parameter).value;
                        }
                        break;
                    case AbstractParameter.ParameterType.LIST:
                        {
                            TMP_Dropdown dropDown = gameObjects[0].GetComponent<TMP_Dropdown>();
                            List<string> names = new List<string>();
                            dropDown.value = ((ListParameter)parameter).value;
                        }
                        break;
                }
            }
        }

        //!
        //! Function to destroy all created UI elements of a menu.
        //!
        private void hideMenu(object sender, EventArgs e)
        {
            m_oldMenu.visible = false;
            destroyMenu();
        }

        //!
        //! Function to destroy all created UI elements of a menu.
        //!
        private void destroyMenu()
        {
            foreach (ParameterObject parameterObject in m_parameterObjects)
                parameterObject.hasChanged -= updateItem;

            m_parameterObjects.Clear();

            m_parameterMapping.Clear();

            foreach (GameObject uiElement in m_uiElements)
                UnityEngine.Object.DestroyImmediate(uiElement);
            
            m_uiElements.Clear();
        }
    }
}
