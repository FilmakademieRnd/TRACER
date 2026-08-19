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

//! @file "OrbitImpactPin"
//! @brief simple (debug) viz for an orbit helper when we orbit the camera
//! @author Thomas Krüger
//! @ai created with reference for DragVizualizer and DragRotateVizualizer
//! @version 0
//! @date 23.06.2026

using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class OrbitImpactPin {

    private GameObject _rootObj;
    private Transform _pinHolder;
    private RectTransform _canvasRect;
    private CanvasGroup _canvasGroup;

    private Image _baseRing;
    private Image _arcFill;
    private Material _sharedMat;
    private Sprite _circleSprite;

    private MonoBehaviour _coroutineRunner;
    private Coroutine _activeTransition;

    private Vector3 _startCamDirFlat;
    private float _targetRadius = 1.2f;

    public OrbitImpactPin(MonoBehaviour runner) {
        _coroutineRunner = runner;
    }

    /// <summary>
    /// Call this on your CameraLookAround START event.
    /// </summary>
    public void StartPin(Vector3 targetPosition, Vector3 cameraPosition, float ringRadius = 1.2f, Color themeColor = default) {
        if (themeColor == default) themeColor = new Color(0f, 0.75f, 1f, 0.8f); // Soft Cyan default
        _targetRadius = ringRadius;

        // 1. Force kill any ongoing fade-outs from a previous click
        Dismiss(true); 

        // 2. Calculate Ground Plane projection
        Vector3 camToTarget = (cameraPosition - targetPosition);
        _startCamDirFlat = Vector3.ProjectOnPlane(camToTarget, Vector3.up).normalized;
        if (_startCamDirFlat == Vector3.zero) _startCamDirFlat = Vector3.forward;

        // 3. Build Hierarchy
        CreateHierarchy(targetPosition, themeColor);

        // 4. Trigger the "Crush Down & Pop" sequence
        if (_coroutineRunner != null && _coroutineRunner.gameObject.activeInHierarchy) {
            _activeTransition = _coroutineRunner.StartCoroutine(CrushAndPopRoutine());
        }
    }

    /// <summary>
    /// Call this inside your ONGOING / UPDATE orbit event.
    /// </summary>
    public void UpdateOrbit(Vector3 currentCameraPosition) {
        if (_rootObj == null || _arcFill == null || _startCamDirFlat == Vector3.zero) return;

        Vector3 currCamDirFlat = Vector3.ProjectOnPlane(currentCameraPosition - _rootObj.transform.position, Vector3.up).normalized;
        
        float angle = Vector3.SignedAngle(_startCamDirFlat, currCamDirFlat, Vector3.up);

        _arcFill.fillAmount = Mathf.Abs(angle) / 360f;
        _arcFill.fillClockwise = angle < 0;
    }

    /// <summary>
    /// Call this on Ended, Canceled, or Interrupted.
    /// </summary>
    public void Dismiss(bool instant = false) {
        if (_rootObj == null) return;

        if (_activeTransition != null && _coroutineRunner != null) {
            _coroutineRunner.StopCoroutine(_activeTransition);
            _activeTransition = null;
        }

        if (instant || _coroutineRunner == null || !_coroutineRunner.gameObject.activeInHierarchy) {
            Object.Destroy(_rootObj);
            _rootObj = null;
        } else {
            _activeTransition = _coroutineRunner.StartCoroutine(GracefulFadeOut());
        }
    }

    // --- ANIMATION COROUTINES ---

    private IEnumerator CrushAndPopRoutine() {
        float t = 0f;
        float dropDuration = 0.06f; // Extremely snappy fall
        float dropStartHeight = 1.5f;

        // Stage 1: The Sky Crush
        while (t < 1f) {
            t += Time.deltaTime / dropDuration;
            float ease = t * t; // Quadratic Ease-In (Gravity feel)
            _pinHolder.localPosition = new Vector3(0, Mathf.Lerp(dropStartHeight, 0f, ease), 0);
            yield return null;
        }
        _pinHolder.localPosition = Vector3.zero;

        // Stage 2: Ground Ring Pop
        t = 0f;
        float popDuration = 0.16f;
        while (t < 1f) {
            t += Time.deltaTime / popDuration;
            
            // Cubic Ease-Out-Back (creates a soft 10% expanding ripple overshoot)
            float c1 = 1.70158f;
            float ease = 1f + (c1 + 1f) * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);

            _canvasRect.localScale = Vector3.one * ((_targetRadius * 2f / 400f) * Mathf.Max(0, ease));
            yield return null;
        }
    }

    private IEnumerator GracefulFadeOut() {
        float t = 0f;
        float fadeDur = 0.12f;
        
        float startAlpha = _canvasGroup.alpha;
        Vector3 startPinScale = _pinHolder.localScale;
        Vector3 startRingScale = _canvasRect.localScale;

        while (t < 1f) {
            t += Time.deltaTime / fadeDur;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            _pinHolder.localScale = Vector3.Lerp(startPinScale, Vector3.zero, t);
            _canvasRect.localScale = Vector3.Lerp(startRingScale, startRingScale * 0.5f, t);
            yield return null;
        }

        Object.Destroy(_rootObj);
        _rootObj = null;
    }

    // --- PROCEDURAL GENERATION ---

    private void CreateHierarchy(Vector3 pos, Color color) {
        _rootObj = new GameObject("OrbitDebugViz_Root");
        _rootObj.transform.position = pos;

        _sharedMat = new Material(Shader.Find("Standard"));
        _sharedMat.SetFloat("_Mode", 2);
        _sharedMat.SetInt("_ZWrite", 0);
        _sharedMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _sharedMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _sharedMat.renderQueue = 3000;
        _sharedMat.color = color;
        _sharedMat.EnableKeyword("_EMISSION");
        _sharedMat.SetColor("_EmissionColor", color * 0.4f);

        // 1. The Slender Spike Pin
        _pinHolder = new GameObject("PinHolder").transform;
        _pinHolder.SetParent(_rootObj.transform, false);
        
        GameObject pinMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(pinMesh.GetComponent<Collider>());
        pinMesh.transform.SetParent(_pinHolder, false);
        pinMesh.transform.localPosition = new Vector3(0f, 0.2f, 0f); // Pivot at the base tip
        pinMesh.transform.localScale = new Vector3(0.025f, 0.4f, 0.025f); // Very slender needle
        pinMesh.GetComponent<MeshRenderer>().sharedMaterial = _sharedMat;

        // 2. The Ground UI Canvas
        GameObject canvasGo = new GameObject("GroundCanvas");
        canvasGo.transform.SetParent(_rootObj.transform, false);
        canvasGo.transform.localPosition = new Vector3(0, 0.005f, 0); // Lift 5mm to stop ground Z-fighting
        
        // Face the sky, point Canvas "UP" towards the camera's starting look vector
        canvasGo.transform.rotation = Quaternion.LookRotation(Vector3.up, _startCamDirFlat);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = color.a;

        _canvasRect = canvasGo.GetComponent<RectTransform>();
        _canvasRect.sizeDelta = new Vector2(400f, 400f);
        _canvasRect.localScale = Vector3.zero; // Hide for pop animation

        _baseRing = CreateCanvasRing("BaseRing", _canvasRect, new Color(color.r, color.g, color.b, 0.15f));
        _arcFill = CreateCanvasRing("ArcFill", _canvasRect, color);
    }

    private Image CreateCanvasRing(string name, RectTransform parent, Color c) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.rectTransform.sizeDelta = new Vector2(400f, 400f);
        img.sprite = GetOrCreateRingSprite();
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = (int)Image.Origin360.Top;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    private Sprite GetOrCreateRingSprite() {
        if (_circleSprite != null) return _circleSprite;
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float outer = center - 2f;
        float inner = outer - 24f; // Clean 24px ring

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float d = Mathf.Sqrt(Mathf.Pow((x + 0.5f) - center, 2) + Mathf.Pow((y + 0.5f) - center, 2));
                if (d > inner && d < outer) tex.SetPixel(x, y, Color.white);
                else if (d <= inner && d > inner - 2f) tex.SetPixel(x, y, new Color(1,1,1, (d - (inner - 2f)) / 2f));
                else if (d >= outer && d < outer + 2f) tex.SetPixel(x, y, new Color(1,1,1, 1f - ((d - outer) / 2f)));
                else tex.SetPixel(x, y, new Color(0,0,0,0));
            }
        }
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(0.5f, 0.5f));
        return _circleSprite;
    }
}