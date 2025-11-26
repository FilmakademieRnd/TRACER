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

//! @file "ListSelector.cs"
//! @brief Vertical snap selector for LIST parameters
//! @author Jonas Trottnow
//! @version 0
//! @date 05.11.2025

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace tracer
{
    //!
    //! Vertical snap selector for selecting items from a LIST parameter
    //!
    public class ListSelector : Manipulator, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // Constants
        private const float ITEM_HEIGHT = 14f;
        private const int PREVIEW_ITEMS = 2; // Items visible above/below selected

        // References
        private UIManager _manager;
        private ListParameter _listParam;
        private RectTransform _contentPanel;
        private RectTransform _viewport;
        private Canvas _canvas;

        // State
        private int _selectedIndex = 0;
        private bool _isDragging = false;

        // UI Elements
        private List<GameObject> _itemObjects = new List<GameObject>();
        private GameObject _applyAllButton;

        public override void Init(AbstractParameter para, UIManager m)
        {
            abstractParam = para;
            _manager = m;
            _canvas = GetComponentInParent<Canvas>();

            if (abstractParam.tracerType != AbstractParameter.ParameterType.LIST)
            {
                Helpers.Log("ListSelector can only be used with LIST parameters!", Helpers.logMsgType.WARNING);
                return;
            }

            _listParam = (ListParameter)abstractParam;
            _selectedIndex = _listParam.value;

            BuildUI();
            PositionToSelected();
            UpdateVisuals();

            // Add "Apply to all cameras" button if this is a camera parameter
            if (abstractParam._parent != null && abstractParam._parent is SceneObjectCamera)
            {
                CreateApplyAllButton();
            }
        }

        private void BuildUI()
        {
            int itemCount = _listParam.parameterList.Count;

            // Container
            RectTransform container = GetComponent<RectTransform>();
            if (container == null) container = gameObject.AddComponent<RectTransform>();
            container.sizeDelta = new Vector2(60, 100);

            // Viewport - centered in container
            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.layer = 5;
            viewportObj.transform.SetParent(transform, false);
            _viewport = viewportObj.AddComponent<RectTransform>();
            _viewport.anchorMin = new Vector2(0.5f, 0.5f);
            _viewport.anchorMax = new Vector2(0.5f, 0.5f);
            _viewport.pivot = new Vector2(0.5f, 0.5f);
            _viewport.sizeDelta = new Vector2(50, ITEM_HEIGHT * (PREVIEW_ITEMS * 2 + 1));
            _viewport.anchoredPosition = Vector2.zero; // Centered

            // Subtle background
            viewportObj.AddComponent<CanvasRenderer>();
            Image bgImage = viewportObj.AddComponent<Image>();
            Color bgColor = _manager.uiAppearanceSettings.colors.DefaultBG;
            bgColor.a = 0.1f;
            bgImage.color = bgColor;

            // Mask with softness for fade effect
            RectMask2D mask = viewportObj.AddComponent<RectMask2D>();
            mask.softness = new Vector2Int(0, (int)(ITEM_HEIGHT * PREVIEW_ITEMS));

            // Content panel with VerticalLayoutGroup
            GameObject contentObj = new GameObject("Content");
            contentObj.layer = 5;
            contentObj.transform.SetParent(_viewport, false);
            _contentPanel = contentObj.AddComponent<RectTransform>();
            _contentPanel.anchorMin = new Vector2(0.5f, 1f);
            _contentPanel.anchorMax = new Vector2(0.5f, 1f);
            _contentPanel.pivot = new Vector2(0.5f, 1f);
            _contentPanel.sizeDelta = new Vector2(50, ITEM_HEIGHT * itemCount * 3); // 3x for looping

            VerticalLayoutGroup layout = contentObj.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 0;

            // Create items (3 repetitions for infinite loop)
            for (int rep = 0; rep < 3; rep++)
            {
                for (int i = 0; i < itemCount; i++)
                {
                    CreateItem(i, _listParam.parameterList[i].name);
                }
            }

            // Arrows
            CreateArrows();
        }

        private void CreateItem(int logicalIndex, string text)
        {
            GameObject itemObj = new GameObject($"Item_{logicalIndex}");
            itemObj.layer = 5;
            itemObj.transform.SetParent(_contentPanel, false);

            RectTransform itemRect = itemObj.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0.5f, 1f);
            itemRect.anchorMax = new Vector2(0.5f, 1f);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.sizeDelta = new Vector2(50, ITEM_HEIGHT);

            // Background
            itemObj.AddComponent<CanvasRenderer>();
            Image bg = itemObj.AddComponent<Image>();
            bg.color = Color.clear;

            // Button for clicking
            Button btn = itemObj.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            int capturedIndex = logicalIndex;
            btn.onClick.AddListener(() => OnItemClick(capturedIndex));

            // Text
            GameObject textObj = new GameObject("Text");
            textObj.layer = 5;
            textObj.transform.SetParent(itemObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = new Vector2(-4, 0); // Padding
            textRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI textComp = textObj.AddComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.font = _manager.uiAppearanceSettings.defaultFont;
            textComp.fontSize = 7;
            textComp.fontStyle = FontStyles.Bold;
            textComp.alignment = TextAlignmentOptions.Center;
            textComp.verticalAlignment = VerticalAlignmentOptions.Middle;
            textComp.color = Color.white;
            textComp.overflowMode = TextOverflowModes.Overflow;
            textComp.enableWordWrapping = false;
            textComp.raycastTarget = false;

            _itemObjects.Add(itemObj);
        }

        private void CreateArrows()
        {
            float viewportHeight = ITEM_HEIGHT * (PREVIEW_ITEMS * 2 + 1);

            // Up arrow - above viewport
            GameObject upArrow = new GameObject("UpArrow");
            upArrow.layer = 5;
            upArrow.transform.SetParent(transform, false);

            RectTransform upRect = upArrow.AddComponent<RectTransform>();
            upRect.anchorMin = new Vector2(0.5f, 0.5f);
            upRect.anchorMax = new Vector2(0.5f, 0.5f);
            upRect.pivot = new Vector2(0.5f, 0.5f);
            upRect.sizeDelta = new Vector2(50, 10);
            upRect.anchoredPosition = new Vector2(0, viewportHeight / 2 + 6);

            Button upBtn = upArrow.AddComponent<Button>();
            upBtn.transition = Selectable.Transition.None;
            upBtn.onClick.AddListener(() => SelectPrevious());

            TextMeshProUGUI upText = upArrow.AddComponent<TextMeshProUGUI>();
            upText.text = "▲";
            upText.font = _manager.uiAppearanceSettings.defaultFont;
            upText.fontSize = 8;
            upText.alignment = TextAlignmentOptions.Center;
            upText.color = _manager.uiAppearanceSettings.colors.FontColor;
            upText.raycastTarget = false;

            // Down arrow - below viewport
            GameObject downArrow = new GameObject("DownArrow");
            downArrow.layer = 5;
            downArrow.transform.SetParent(transform, false);

            RectTransform downRect = downArrow.AddComponent<RectTransform>();
            downRect.anchorMin = new Vector2(0.5f, 0.5f);
            downRect.anchorMax = new Vector2(0.5f, 0.5f);
            downRect.pivot = new Vector2(0.5f, 0.5f);
            downRect.sizeDelta = new Vector2(50, 10);
            downRect.anchoredPosition = new Vector2(0, -viewportHeight / 2 - 6);

            Button downBtn = downArrow.AddComponent<Button>();
            downBtn.transition = Selectable.Transition.None;
            downBtn.onClick.AddListener(() => SelectNext());

            TextMeshProUGUI downText = downArrow.AddComponent<TextMeshProUGUI>();
            downText.text = "▼";
            downText.font = _manager.uiAppearanceSettings.defaultFont;
            downText.fontSize = 8;
            downText.alignment = TextAlignmentOptions.Center;
            downText.color = _manager.uiAppearanceSettings.colors.FontColor;
            downText.raycastTarget = false;
        }

        private void CreateApplyAllButton()
        {
            _applyAllButton = new GameObject("ApplyAllButton");
            _applyAllButton.layer = 5;
            _applyAllButton.transform.SetParent(transform, false);

            RectTransform btnRect = _applyAllButton.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0);
            btnRect.anchorMax = new Vector2(0.5f, 0);
            btnRect.pivot = new Vector2(0.5f, 0);
            btnRect.anchoredPosition = new Vector2(0, 5);
            btnRect.sizeDelta = new Vector2(46, 12);

            _applyAllButton.AddComponent<CanvasRenderer>();
            Image bg = _applyAllButton.AddComponent<Image>();
            // Subtle background like other buttons
            Color bgColor = _manager.uiAppearanceSettings.colors.DefaultBG;
            bgColor.a = 0.2f;
            bg.color = bgColor;

            Button btn = _applyAllButton.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(ApplyToAllCameras);

            GameObject textObj = new GameObject("Text");
            textObj.layer = 5;
            textObj.transform.SetParent(_applyAllButton.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "Apply to all";
            text.font = _manager.uiAppearanceSettings.defaultFont;
            text.fontSize = 6;
            text.fontStyle = FontStyles.Normal;
            text.alignment = TextAlignmentOptions.Center;
            text.verticalAlignment = VerticalAlignmentOptions.Middle;
            text.color = _manager.uiAppearanceSettings.colors.FontColor;

            // Adjust container size
            GetComponent<RectTransform>().sizeDelta = new Vector2(60, 125);
        }

        private void PositionToSelected()
        {
            // Position content so selected item (in middle set) is at viewport center
            // Viewport center is at Y=0, viewport top is at +2.5*ITEM_HEIGHT (for PREVIEW_ITEMS=2)
            // Content top anchor is at viewport top
            // Item i center is at -(i+0.5)*ITEM_HEIGHT from content top
            // For item i to be at viewport center: content.y + viewportTop - (i+0.5)*ITEM_HEIGHT = 0
            // Simplifies to: content.y = (i - PREVIEW_ITEMS)*ITEM_HEIGHT
            int itemCount = _listParam.parameterList.Count;
            int middleSetIndex = itemCount + _selectedIndex;
            float targetY = (middleSetIndex - PREVIEW_ITEMS) * ITEM_HEIGHT;
            _contentPanel.anchoredPosition = new Vector2(0, targetY);
        }

        private void OnItemClick(int index)
        {
            if (index == _selectedIndex) return;

            _selectedIndex = index;
            _listParam.select(_selectedIndex);
            PositionToSelected();
            UpdateVisuals();
            InvokeDoneEditing(this, true);
        }

        private void SelectPrevious()
        {
            _selectedIndex = (_selectedIndex - 1 + _listParam.parameterList.Count) % _listParam.parameterList.Count;
            _listParam.select(_selectedIndex);
            PositionToSelected();
            UpdateVisuals();
            InvokeDoneEditing(this, true);
        }

        private void SelectNext()
        {
            _selectedIndex = (_selectedIndex + 1) % _listParam.parameterList.Count;
            _listParam.select(_selectedIndex);
            PositionToSelected();
            UpdateVisuals();
            InvokeDoneEditing(this, true);
        }

        private void UpdateVisuals()
        {
            int itemCount = _listParam.parameterList.Count;
            int middleSetSelected = itemCount + _selectedIndex;

            for (int i = 0; i < _itemObjects.Count; i++)
            {
                Image bg = _itemObjects[i].GetComponent<Image>();
                TextMeshProUGUI text = _itemObjects[i].GetComponentInChildren<TextMeshProUGUI>();

                bool isSelected = (i == middleSetSelected);

                if (isSelected)
                {
                    text.color = _manager.uiAppearanceSettings.colors.ElementSelection_Highlight;
                    Color bgColor = _manager.uiAppearanceSettings.colors.DefaultBG;
                    bgColor.a = 0.3f;
                    bg.color = bgColor;
                }
                else
                {
                    text.color = Color.white;
                    bg.color = Color.clear;
                }
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;

            // Move content
            float scale = 1f / _canvas.scaleFactor;
            Vector2 pos = _contentPanel.anchoredPosition;
            pos.y += eventData.delta.y * scale;
            _contentPanel.anchoredPosition = pos;

            // Infinite loop wrapping - keep content in middle set range
            // From PositionToSelected: content.y = (middleSetIndex - PREVIEW_ITEMS) * ITEM_HEIGHT
            // Middle set spans from itemCount to 2*itemCount-1
            // So content.y ranges from (itemCount-PREVIEW_ITEMS) to (2*itemCount-1-PREVIEW_ITEMS)
            int itemCount = _listParam.parameterList.Count;
            float setHeight = itemCount * ITEM_HEIGHT;
            float upperBound = (2 * itemCount - PREVIEW_ITEMS) * ITEM_HEIGHT;
            float lowerBound = (itemCount - PREVIEW_ITEMS) * ITEM_HEIGHT;

            if (pos.y > upperBound)
            {
                pos.y -= setHeight;
                _contentPanel.anchoredPosition = pos;
            }
            else if (pos.y < lowerBound)
            {
                pos.y += setHeight;
                _contentPanel.anchoredPosition = pos;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;

            // Snap to nearest item
            // From PositionToSelected: content.y = (i - PREVIEW_ITEMS) * ITEM_HEIGHT
            // So: i = (content.y / ITEM_HEIGHT) + PREVIEW_ITEMS
            int itemCount = _listParam.parameterList.Count;
            Vector2 pos = _contentPanel.anchoredPosition;

            int closestItemGlobal = Mathf.RoundToInt(pos.y / ITEM_HEIGHT) + PREVIEW_ITEMS;

            // Wrap to get logical index (0 to itemCount-1)
            int logicalIndex = closestItemGlobal % itemCount;
            if (logicalIndex < 0) logicalIndex += itemCount;

            // Update selection
            _selectedIndex = logicalIndex;
            _listParam.select(_selectedIndex);
            PositionToSelected();
            UpdateVisuals();
            InvokeDoneEditing(this, true);
        }

        private void ApplyToAllCameras()
        {
            SceneManager sceneManager = _manager.core.getManager<SceneManager>();
            if (sceneManager == null) return;

            List<SceneObjectCamera> cameras = sceneManager.sceneCameraList;
            if (cameras == null || cameras.Count == 0) return;

            string paramName = abstractParam.name;
            int appliedCount = 0;

            foreach (SceneObjectCamera camera in cameras)
            {
                foreach (AbstractParameter param in camera.parameterList)
                {
                    if (param.name == paramName && param is ListParameter listParam)
                    {
                        listParam.select(_selectedIndex);
                        appliedCount++;
                        break;
                    }
                }
            }

            Helpers.Log($"Applied {paramName} to {appliedCount} camera(s)");
            InvokeDoneEditing(this, true);
        }

        private void OnDestroy()
        {
            // Cleanup button listeners
            foreach (GameObject item in _itemObjects)
            {
                Button btn = item.GetComponent<Button>();
                if (btn != null) btn.onClick.RemoveAllListeners();
            }
        }
    }
}
