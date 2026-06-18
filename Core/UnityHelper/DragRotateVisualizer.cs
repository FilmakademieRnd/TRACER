using UnityEngine;
using UnityEngine.UI;

public class DragRotateVisualizer {

    private GameObject _rootObj;
    private RectTransform _startLinePivot;
    private RectTransform _currentLinePivot;

    private Image _fillImageFront;
    private Image _fillImageBack;
    private Image _startLineGraphic;
    private Image _currentLineGraphic;

    private Sprite _circleSprite;

    private float _radius = 1.2f;
    private float _canvasResolution = 400f; // Internal pixel resolution for crisp UI

    private float _accumulatedAngle = 0f;
    private Vector3 _lastCurrentVec;

    public void UpdateVisuals(Vector3 origin, Vector3 planeNormal, Vector3 startVec, Vector3 currentVec, bool isFreeRotation) {
        if (_rootObj == null) {
            CreateUIHierarchy();
            _rootObj.transform.position = origin;
            
            // If your VisualizerAnimator only checks for MeshRenderers, it might break UI elements!
            // I recommend animating the scale directly here, or ensuring your Animator supports CanvasGroup.alpha
            _rootObj.transform.localScale = Vector3.one; 

            _accumulatedAngle = 0f;
            _lastCurrentVec = startVec;
        }

        _rootObj.transform.position = origin;

        float frameDelta = Vector3.SignedAngle(_lastCurrentVec, currentVec, planeNormal);
        if (Mathf.Abs(frameDelta) < 180f) {
            _accumulatedAngle += frameDelta;
        }
        _lastCurrentVec = currentVec;

        // 1. Orient the Canvas to face the plane normal, and align its "Up" to the start vector
        if (planeNormal != Vector3.zero && startVec != Vector3.zero) {
            _rootObj.transform.rotation = Quaternion.LookRotation(planeNormal, startVec.normalized);
        }

        // 2. Multi-Turn Math & Color Shifting
        float absAngle = Mathf.Abs(_accumulatedAngle);
        int turns = Mathf.FloorToInt(absAngle / 360f);
        float fillAmount = (absAngle % 360f) / 360f;
        
        if (absAngle > 0f && fillAmount == 0f) fillAmount = 1f; 

        Color axisColor = GetAxisColor(planeNormal, isFreeRotation);
        
        float darkenFactor = Mathf.Clamp01(1f - (turns * 0.15f));
        float alphaFactor = Mathf.Clamp01(0.3f + (turns * 0.15f));
        Color turnColor = new Color(axisColor.r * darkenFactor, axisColor.g * darkenFactor, axisColor.b * darkenFactor, alphaFactor);

        _fillImageFront.color = turnColor;
        _fillImageBack.color = turnColor;

        _fillImageFront.fillAmount = fillAmount;
        _fillImageBack.fillAmount = fillAmount;

        bool isClockwise = _accumulatedAngle > 0;
        _fillImageFront.fillClockwise = isClockwise;
        _fillImageBack.fillClockwise = !isClockwise; 

        // 3. Orient the Reference Lines (UI local space)
        // Because the Canvas 'Up' is already aligned to startVec, the start line needs 0 rotation!
        _startLinePivot.localRotation = Quaternion.identity;
        
        // The current line simply rotates locally inside the canvas around the Z axis
        // (Unity UI Z-rotation is counter-clockwise, so we negate the accumulated angle)
        _currentLinePivot.localRotation = Quaternion.Euler(0, 0, -_accumulatedAngle);

        _startLineGraphic.color = Color.Lerp(axisColor, Color.white, 0.5f);
        _currentLineGraphic.color = axisColor;
    }

    public void Cleanup() {
        if (_rootObj != null) {
            Object.Destroy(_rootObj);
            _rootObj = null; 
        }
    }

    private void CreateUIHierarchy() {
        _rootObj = new GameObject("DragRotateVisualizer_UI");

        Canvas canvas = _rootObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 3000; // Render above other things
        
        CanvasGroup group = _rootObj.AddComponent<CanvasGroup>();
        group.alpha = 1f; // Use this to fade the UI in your Animator!
        group.interactable = false;
        group.blocksRaycasts = false;

        RectTransform rootRect = _rootObj.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(_canvasResolution, _canvasResolution);
        
        // Scale the massive UI Canvas down to fit the 3D world radius exactly
        float scaleMultiplier = (_radius * 2f) / _canvasResolution;
        rootRect.localScale = new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier);

        // Fill Images (Background Rings)
        _fillImageFront = CreateRingImage("FrontFill", rootRect);
        _fillImageBack = CreateRingImage("BackFill", rootRect);
        _fillImageBack.rectTransform.localRotation = Quaternion.Euler(0, 180f, 0);

        // Indicator Line Pivots (Centered, size 0)
        _startLinePivot = CreatePivot("StartLinePivot", rootRect);
        _currentLinePivot = CreatePivot("CurrentLinePivot", rootRect);

        // Indicator Graphics (Offset to the exact edge of the ring)
        float lineWidth = 8f; // Pixels in canvas space
        float lineLength = 40f; 
        float lineYOffset = (_canvasResolution / 2f) - (lineLength / 2f); // Places it exactly on the perimeter

        _startLineGraphic = CreateLineGraphic("StartLineGraphic", _startLinePivot, lineWidth, lineLength, lineYOffset);
        _currentLineGraphic = CreateLineGraphic("CurrentLineGraphic", _currentLinePivot, lineWidth, lineLength, lineYOffset);

        canvas.enabled = true;
        group.enabled = true;
        _fillImageFront.enabled = true;
        _fillImageBack.enabled = true;
        _currentLineGraphic.enabled = true;
        _startLineGraphic.enabled = true;
    }

    private Image CreateRingImage(string name, RectTransform parent) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.rectTransform.sizeDelta = new Vector2(_canvasResolution, _canvasResolution);
        img.rectTransform.anchoredPosition = Vector2.zero;
        img.sprite = GetOrCreateCircleSprite();
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = (int)Image.Origin360.Top;
        img.raycastTarget = false;
        return img;
    }

    private RectTransform CreatePivot(string name, RectTransform parent) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    private Image CreateLineGraphic(string name, RectTransform parent, float width, float length, float yPos) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.rectTransform.sizeDelta = new Vector2(width, length);
        img.rectTransform.anchoredPosition = new Vector2(0, yPos);
        img.raycastTarget = false;
        // Without a sprite, Unity draws a perfect solid white rectangle, which is exactly what we want for the line.
        return img;
    }

    private Sprite GetOrCreateCircleSprite() {
        if (_circleSprite != null) return _circleSprite;

        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color solid = Color.white;
        
        float center = size / 2f;
        float outerRadius = (size / 2f) - 2f;
        float innerRadius = outerRadius - 35f; 

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dist = Mathf.Sqrt(Mathf.Pow((x + 0.5f) - center, 2) + Mathf.Pow((y + 0.5f) - center, 2));
                
                if (dist > innerRadius && dist < outerRadius) {
                    tex.SetPixel(x, y, solid);
                } else if (dist <= innerRadius && dist > innerRadius - 2f) { 
                    float alpha = (dist - (innerRadius - 2f)) / 2f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                } else if (dist >= outerRadius && dist < outerRadius + 2f) { 
                    float alpha = 1f - ((dist - outerRadius) / 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                } else {
                    tex.SetPixel(x, y, clear);
                }
            }
        }
        tex.Apply();
        
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _circleSprite;
    }

    private Color GetAxisColor(Vector3 worldAxisDir, bool isFreeRotation) {
        if (isFreeRotation) return new Color(0.8f, 0.8f, 0.8f, 1f); 

        Vector3 a = new Vector3(Mathf.Abs(worldAxisDir.x), Mathf.Abs(worldAxisDir.y), Mathf.Abs(worldAxisDir.z));
        a.Normalize();

        if (a.x > 0.8f) return Color.red;
        if (a.y > 0.8f) return Color.green;
        if (a.z > 0.8f) return Color.blue;

        return Color.cyan; 
    }
}