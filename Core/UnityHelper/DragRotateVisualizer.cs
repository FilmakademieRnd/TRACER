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

//! @file "DragRotateVisualizer"
//! @brief easy going debug viz for our rotation handling of objects
//! could be extended for a final viz
//! @author Thomas Krüger
//! @ai adjusted multiple times with Gemini Pro
//! @version 0
//! @date 22.06.2026

using UnityEngine;
using UnityEngine.UI;

public class DragRotateVisualizer {

    private const bool EnableOnlyInDebugMode = true; 
    private const bool ConstantScreenSize = true;
    // The distance at which the visualizer appears at its normal 1:1 _radius size. 
    // Tweak this if the initial spawn size feels too big/small!
    private const float ReferenceCameraDistance = 5f;
    
    private float _radius = 1.0f;
    private float _canvasResolution = 400f; 
    private float _lineThickness = 6f;

    private GameObject _rootObj;
    private float _accumulatedAngle = 0f;
    private Vector3 _lastCurrentVec;
    
    private Sprite _circleSprite;
    private Sprite _whiteSprite; // forces the lines to render

    //no occlusion for lines, always visible!
    private RectTransform _startLinePivot, _currentLinePivot;

    private Image _activeFront, _activeBack, _baseFront, _baseBack;
    private Image _occActiveFront, _occActiveBack, _occBaseFront, _occBaseBack;
    private Image _startGraphic, _currentGraphic;

    private Material _matVis, _matOcc;
    private Color currentAxisColor;


    public void UpdateVisuals(Vector3 origin, Vector3 planeNormal, Vector3 startVec, Vector3 currentVec, bool isFreeRotation, Transform trRefAxisColor) {
        
        if(isFreeRotation)
            return;

        if (EnableOnlyInDebugMode && !Application.isEditor && !Debug.isDebugBuild) {
            Cleanup();
            return;
        }

        if (_rootObj == null) {
            InitializeMaterials();
            CreateUIHierarchy(origin);
            
            _rootObj.transform.position = origin;
            _accumulatedAngle = 0f;
            _lastCurrentVec = startVec;

            currentAxisColor = GetLocalAxisColor(planeNormal, trRefAxisColor.transform);
        }

        _rootObj.transform.position = origin;

        float frameDelta = -Vector3.SignedAngle(_lastCurrentVec, currentVec, planeNormal);
        if (Mathf.Abs(frameDelta) < 180f) {
            _accumulatedAngle += frameDelta;
        }
        _lastCurrentVec = currentVec;

        if (planeNormal != Vector3.zero && startVec != Vector3.zero) {
            _rootObj.transform.rotation = Quaternion.LookRotation(planeNormal, startVec.normalized);
        }

        float absAngle = Mathf.Abs(_accumulatedAngle);
        int activeTurnIndex = Mathf.FloorToInt(absAngle / 360f);
        float activeFill = (absAngle % 360f) / 360f;
        
        if (absAngle > 0f && activeFill == 0f) activeFill = 1f; 

        int baseTurnIndex = Mathf.Max(0, activeTurnIndex - 1);
        float baseFill = activeTurnIndex > 0 ? 1f : 0f;

        SetFills(baseFill, activeFill, _accumulatedAngle > 0);

        // --- Snail Shell "Switch" Math ---
        //Color axisColor = GetAxisColor(planeNormal, isFreeRotation);
        
        Color baseColor = CalculateTurnColor(currentAxisColor, baseTurnIndex);
        Color activeColor = CalculateTurnColor(currentAxisColor, activeTurnIndex);

        // Apply to Visible layer
        _baseFront.color = baseColor; _baseBack.color = baseColor;
        _activeFront.color = activeColor; _activeBack.color = activeColor;

        // Apply faint Ghosting to Occluded layer
        Color occBaseColor = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * 0.25f);
        Color occActiveColor = new Color(activeColor.r, activeColor.g, activeColor.b, activeColor.a * 0.25f);
        
        _occBaseFront.color = occBaseColor; _occBaseBack.color = occBaseColor;
        _occActiveFront.color = occActiveColor; _occActiveBack.color = occActiveColor;

        // Lines get solid active color
        // Color lineColor = new Color(activeColor.r, activeColor.g, activeColor.b, 1f);
        // _startGraphic.color = Color.Lerp(axisColor, Color.white, 0.6f);
        // _currentGraphic.color = lineColor;
        _startGraphic.color = Color.gray;
        _currentGraphic.color = Color.white;
        //Color lineColor = new Color(activeColor.r, activeColor.g, activeColor.b, 1f);
        //_occStartGraphic.color = new Color(_startGraphic.color.r, _startGraphic.color.g, _startGraphic.color.b, 0.25f);
        //_occCurrentGraphic.color = new Color(lineColor.r, lineColor.g, lineColor.b, 0.25f);

        // Orient Indicator Lines
        _startLinePivot.localRotation = Quaternion.identity;
        
        Quaternion currentRot = Quaternion.Euler(0, 0, -_accumulatedAngle);
        _currentLinePivot.localRotation = currentRot;
    }

    public void Cleanup() {
        if (_rootObj != null) {
            Object.Destroy(_rootObj);
            _rootObj = null; 
        }
    }

    private void CreateUIHierarchy(Vector3 originForDistance) {
        _rootObj = new GameObject("DragRotateVisualizer_UI");

        Canvas canvas = _rootObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 3000;
        
        CanvasGroup group = _rootObj.AddComponent<CanvasGroup>();
        group.alpha = 1f; 
        group.interactable = false; group.blocksRaycasts = false;

        RectTransform rootRect = _rootObj.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(_canvasResolution, _canvasResolution);
        
        // float scaleMultiplier = (_radius * 2f) / _canvasResolution;
        // rootRect.localScale = new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier);

        // Calculate constant screen size scaling
        float effectiveRadius = _radius;
        if (ConstantScreenSize && Camera.main != null) {
            float dist = Vector3.Distance(Camera.main.transform.position, originForDistance);
            // Scales the radius up/down based on distance so it always looks exactly as it would at 'referenceCameraDistance'
            effectiveRadius = _radius * (dist / ReferenceCameraDistance);
        }

        float scaleMultiplier = (effectiveRadius * 2f) / _canvasResolution;
        rootRect.localScale = new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier);

        // LAYER 1: OCCLUDED (Renders behind walls, drawn first)
        _occBaseFront = CreateRingImage("Occ_BaseFront", rootRect, _matOcc, false);
        _occBaseBack = CreateRingImage("Occ_BaseBack", rootRect, _matOcc, true);
        _occActiveFront = CreateRingImage("Occ_ActiveFront", rootRect, _matOcc, false);
        _occActiveBack = CreateRingImage("Occ_ActiveBack", rootRect, _matOcc, true);
        //position them a small amount "away" so we dont have a fightin
        _occBaseFront.rectTransform.localPosition += Vector3.forward;
        _occBaseBack.rectTransform.localPosition += Vector3.forward;
        _occActiveFront.rectTransform.localPosition += Vector3.forward;
        _occActiveBack.rectTransform.localPosition += Vector3.forward;
        
        // LAYER 2: VISIBLE (Renders normally, drawn over top)
        _baseFront = CreateRingImage("Vis_BaseFront", rootRect, _matVis, false);
        _baseBack = CreateRingImage("Vis_BaseBack", rootRect, _matVis, true);
        _activeFront = CreateRingImage("Vis_ActiveFront", rootRect, _matVis, false);
        _activeBack = CreateRingImage("Vis_ActiveBack", rootRect, _matVis, true);

        CreateLineHierarchy("Vis_StartLine", rootRect, _matVis, out _startLinePivot, out _startGraphic);
        CreateLineHierarchy("Vis_CurrentLine", rootRect, _matVis, out _currentLinePivot, out _currentGraphic);
    }

    private void SetFills(float baseFill, float activeFill, bool isClockwise) {
        _baseFront.fillAmount = baseFill; _baseBack.fillAmount = baseFill;
        _baseFront.fillClockwise = isClockwise; _baseBack.fillClockwise = !isClockwise; 

        _occBaseFront.fillAmount = baseFill; _occBaseBack.fillAmount = baseFill;
        _occBaseFront.fillClockwise = isClockwise; _occBaseBack.fillClockwise = !isClockwise;

        _activeFront.fillAmount = activeFill; _activeBack.fillAmount = activeFill;
        _activeFront.fillClockwise = isClockwise; _activeBack.fillClockwise = !isClockwise;

        _occActiveFront.fillAmount = activeFill; _occActiveBack.fillAmount = activeFill;
        _occActiveFront.fillClockwise = isClockwise; _occActiveBack.fillClockwise = !isClockwise;
    }

    private Image CreateRingImage(string name, RectTransform parent, Material mat, bool flip180) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        if (flip180) go.transform.localRotation = Quaternion.Euler(0, 180f, 0);

        Image img = go.AddComponent<Image>();
        img.rectTransform.sizeDelta = new Vector2(_canvasResolution, _canvasResolution);
        img.sprite = GetOrCreateCircleSprite();
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = (int)Image.Origin360.Top;
        img.raycastTarget = false;
        img.material = mat; 
        return img;
    }

    private void CreateLineHierarchy(string name, RectTransform parent, Material mat, out RectTransform pivot, out Image graphic) {
        GameObject pivotGo = new GameObject(name + "_Pivot");
        pivotGo.transform.SetParent(parent, false);
        pivot = pivotGo.AddComponent<RectTransform>();
        pivot.sizeDelta = Vector2.zero; pivot.anchoredPosition = Vector2.zero;

        GameObject graphicGo = new GameObject("Graphic");
        graphicGo.transform.SetParent(pivot, false);
        graphic = graphicGo.AddComponent<Image>();
        
        float lineLength = 50f;     //35 was too small
        float lineYOffset = (_canvasResolution / 2f) - (lineLength / 2f);
        
        graphic.rectTransform.sizeDelta = new Vector2(_lineThickness, lineLength);
        graphic.rectTransform.anchoredPosition = new Vector2(0, lineYOffset);
        graphic.sprite = GetOrCreateWhiteSprite(); // <--- Guaranteed to render now
        graphic.raycastTarget = false;
        graphic.material = mat;
    }

    private void InitializeMaterials() {
        Shader unlit = Shader.Find("Tracer/GizmoUnlit");
        if (unlit == null) {
            Debug.LogError("Shader 'Tracer/GizmoUnlit' missing! Please create the shader file.");
            return;
        }

        _matVis = new Material(unlit);
        _matVis.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);

        _matOcc = new Material(unlit);
        _matOcc.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Greater);
    }

    private Color CalculateTurnColor(Color baseColor, int turns) {
        // Darkens by 20% each 360 turn: 100% -> 80% -> 64% -> 51%
        float darkenFactor = Mathf.Pow(0.8f, turns); 
        // Starts at 40% opaque, steps up toward solid
        float alphaFactor = Mathf.Clamp01(0.4f + (turns * 0.2f)); 
        
        return new Color(baseColor.r * darkenFactor, baseColor.g * darkenFactor, baseColor.b * darkenFactor, alphaFactor);
    }

    private Color GetAxisColor(Vector3 worldAxisDir, bool isFreeRotation) {
        if (isFreeRotation) return new Color(0.85f, 0.85f, 0.85f, 1f); 

        Vector3 a = new Vector3(Mathf.Abs(worldAxisDir.x), Mathf.Abs(worldAxisDir.y), Mathf.Abs(worldAxisDir.z));
        a.Normalize();

        Debug.Log("Plane Normal Color Grabbing Vector is "+worldAxisDir);

        if (a.x > 0.8f) return Color.red;
        if (a.y > 0.8f) return Color.green;
        if (a.z > 0.8f) return Color.blue;

        return Color.cyan; 
    }

    private Color GetLocalAxisColor(Vector3 planeNormal, Transform objectTr) {
        float dotProduct = Mathf.Abs(Vector3.Dot(planeNormal, objectTr.right));
        if(dotProduct > 0.5f) return Color.red;
        dotProduct = Mathf.Abs(Vector3.Dot(planeNormal, objectTr.up));
        if(dotProduct > 0.5f) return Color.green;
        else
            return Color.blue;
    }

    private Sprite GetOrCreateWhiteSprite() {
        if (_whiteSprite != null) return _whiteSprite;
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        for (int i = 0; i < 16; i++) tex.SetPixel(i % 4, i / 4, Color.white);
        tex.Apply();
        _whiteSprite = Sprite.Create(tex, new Rect(0,0,4,4), Vector2.zero);
        return _whiteSprite;
    }

    private Sprite GetOrCreateCircleSprite() {
        if (_circleSprite != null) return _circleSprite;

        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0,0,0,0);
        
        float center = size / 2f;
        float outerRadius = (size / 2f) - 2f;
        float innerRadius = outerRadius - 35f; 

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dist = Mathf.Sqrt(Mathf.Pow((x + 0.5f) - center, 2) + Mathf.Pow((y + 0.5f) - center, 2));
                
                if (dist > innerRadius && dist < outerRadius) tex.SetPixel(x, y, Color.white);
                else if (dist <= innerRadius && dist > innerRadius - 2f) { 
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, (dist - (innerRadius - 2f)) / 2f));
                } else if (dist >= outerRadius && dist < outerRadius + 2f) { 
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f - ((dist - outerRadius) / 2f)));
                } else tex.SetPixel(x, y, clear);
            }
        }
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(0.5f, 0.5f));
        return _circleSprite;
    }
}