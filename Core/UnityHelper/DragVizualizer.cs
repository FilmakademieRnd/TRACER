using UnityEngine;
using System.Collections;

public class DragVisualizer {

    private GameObject _rootObj;
    private GameObject _planeObj;
    private GameObject _distanceLineObj;
    private GameObject _axisLineObj;

    private Material _planeMat;
    private Material _lineMat;

    private float _lineThickness = 0.02f;
    private float _planeSize = 10f;

    public void UpdateVisuals(Vector3 origin, Vector3 planeNormal, Vector3 newPosition, bool isMovingAlongOneAxis, Vector3 localAxisDirection = default, Vector3 worldAxisDirection = default) {
        if (_rootObj == null) {
            InitializeMaterials();
            CreatePrimitives(isMovingAlongOneAxis);

            _rootObj.transform.position = origin;
            _planeObj.transform.localScale = new Vector3(_planeSize, _planeSize, 1f);

            if (isMovingAlongOneAxis && _axisLineObj != null) {
                // Rotiert die Ebene so, dass die Textur (Up-Vector) exakt an der Bewegungsachse anliegt
                _planeObj.transform.rotation = localAxisDirection != Vector3.zero ? Quaternion.LookRotation(planeNormal, localAxisDirection) : Quaternion.LookRotation(planeNormal);
                
                _axisLineObj.transform.rotation = localAxisDirection != Vector3.zero ? Quaternion.LookRotation(localAxisDirection) : Quaternion.identity;
                _axisLineObj.transform.localScale = new Vector3(_lineThickness, _lineThickness, _planeSize);
                SetPrimitiveColor(_axisLineObj, GetAxisColor(worldAxisDirection) * 0.3f); 
            } else {
                // Standard-Rotation, wenn frei auf der Ebene bewegt wird
                _planeObj.transform.rotation = Quaternion.LookRotation(planeNormal);
            }

            _rootObj.GetComponent<VisualizerAnimator>().AnimateIn();
        }

        Vector3 direction = newPosition - origin;
        float distance = direction.magnitude;

        if (distance > 0.001f) {
            _distanceLineObj.SetActive(true);
            _distanceLineObj.transform.position = origin + (direction / 2f);
            _distanceLineObj.transform.rotation = Quaternion.LookRotation(direction);
            _distanceLineObj.transform.localScale = new Vector3(_lineThickness, _lineThickness, distance);

            Color axisColor = GetAxisColor(worldAxisDirection);
            
            if (isMovingAlongOneAxis) {
                float dotProduct = Vector3.Dot(direction.normalized, localAxisDirection.normalized);
                SetPrimitiveColor(_distanceLineObj, dotProduct >= 0 ? axisColor : Color.Lerp(axisColor, Color.white, 0.5f));
            } else {
                SetPrimitiveColor(_distanceLineObj, axisColor);
            }
        } else {
            _distanceLineObj.SetActive(false);
        }
    }

    public void Cleanup() {
        if (_rootObj != null) {
            _rootObj.GetComponent<VisualizerAnimator>().FadeOutAndDestroy();
            _rootObj = null; 
        }
    }

    private void CreatePrimitives(bool needsAxisLine) {
        _rootObj = new GameObject("DragVisualizer_Root");
        _rootObj.AddComponent<VisualizerAnimator>();

        _planeObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        SetupRenderer(_planeObj, new Material(_planeMat));
        _planeObj.transform.SetParent(_rootObj.transform);

        _distanceLineObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        SetupRenderer(_distanceLineObj, new Material(_lineMat));
        _distanceLineObj.transform.SetParent(_rootObj.transform);
        _distanceLineObj.SetActive(false);

        if (needsAxisLine) {
            _axisLineObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SetupRenderer(_axisLineObj, new Material(_lineMat));
            _axisLineObj.transform.SetParent(_rootObj.transform);
        }
    }

    private void SetupRenderer(GameObject obj, Material mat) {
        Object.Destroy(obj.GetComponent<Collider>());
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        // Kein Licht, keine Schatten!
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = mat;
    }

    private void InitializeMaterials() {
        Shader standardShader = Shader.Find("Standard");

        if (_planeMat == null) {
            _planeMat = new Material(standardShader);
            SetupStandardMaterial(_planeMat, new Color(1f, 1f, 1f, 1.0f));
            _planeMat.mainTexture = GenerateDebugDotTexture();
            //bigger scale means tighter dots
            _planeMat.mainTextureScale = new Vector2(_planeSize * 8f, _planeSize * 8f);
        }
        if (_lineMat == null) {
            _lineMat = new Material(standardShader);
            SetupStandardMaterial(_lineMat, Color.white);
        }
    }

    private void SetupStandardMaterial(Material mat, Color color) {
        mat.SetFloat("_Mode", 2); // Fade Mode
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.color = color;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 0.5f); // Basis-Leuchten für den Unlit-Look
    }

    private Texture2D GenerateDebugDotTexture() {
        int size = 8;   //smaller size, means bigger dots
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point; 

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color dot = new Color(1f, 1f, 1f, 0.2f); 

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                bool isCenter = (x == size / 2 || x == size / 2 - 1) && (y == size / 2 || y == size / 2 - 1);
                tex.SetPixel(x, y, isCenter ? dot : clear);
            }
        }
        tex.Apply();
        return tex;
    }

    private Color GetAxisColor(Vector3 worldAxisDir) {
        Vector3 a = new Vector3(Mathf.Abs(worldAxisDir.x), Mathf.Abs(worldAxisDir.y), Mathf.Abs(worldAxisDir.z));
        a.Normalize();

        bool x = a.x > 0.5f;
        bool y = a.y > 0.5f;
        bool z = a.z > 0.5f;

        // Ebenen-Bewegungen (Mischfarben)
        if (x && y && !z) return Color.yellow;   // XY Ebene
        if (x && !y && z) return Color.magenta;  // XZ Ebene
        if (!x && y && z) return Color.cyan;     // YZ Ebene
        
        // Single Achsen Bewegungen
        if (x && !y && !z) return Color.red;     // X Achse
        if (!x && y && !z) return Color.green;   // Y Achse
        if (!x && !y && z) return Color.blue;    // Z Achse

        return Color.white; // Fallback für omnidirektional (z.B. ViewSpace)
    }

    private void SetPrimitiveColor(GameObject prim, Color color) {
        if (prim != null && prim.TryGetComponent<MeshRenderer>(out var renderer)) {
            renderer.material.color = color;
            renderer.material.SetColor("_EmissionColor", color * 0.6f); // Emission synchronisieren
        }
    }
}

public class VisualizerAnimator : MonoBehaviour {
    
    private Coroutine _animCoroutine;
    private MeshRenderer[] _renderers;

    public void AnimateIn() {
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(ScaleInRoutine());
    }

    public void FadeOutAndDestroy() {
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _renderers = GetComponentsInChildren<MeshRenderer>(true);
        _animCoroutine = StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator ScaleInRoutine() {
        float t = 0f;
        float duration = 0.35f;
        Vector3 targetScale = Vector3.one;
        transform.localScale = Vector3.zero;

        while (t < 1f) {
            t += Time.deltaTime / duration;
            float x = Mathf.Clamp01(t);
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            float easedT = 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
            
            transform.localScale = targetScale * easedT;
            yield return null;
        }
        transform.localScale = targetScale;
    }

    private IEnumerator FadeOutRoutine() {
        float t = 0f;
        float duration = 0.25f;

        Color[] startColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) {
            startColors[i] = _renderers[i].material.color;
        }

        while (t < 1f) {
            t += Time.deltaTime / duration;
            float x = Mathf.Clamp01(t);
            float easedT = 1f - Mathf.Pow(1f - x, 4f);

            for (int i = 0; i < _renderers.Length; i++) {
                if (_renderers[i] == null) continue;
                Color c = startColors[i];
                c.a = Mathf.Lerp(startColors[i].a, 0f, easedT);
                
                // Alpha und Emission synchron faden, sonst leuchtet es schwarz weiter!
                _renderers[i].material.color = c;
                _renderers[i].material.SetColor("_EmissionColor", new Color(c.r, c.g, c.b) * c.a * 0.6f);
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}