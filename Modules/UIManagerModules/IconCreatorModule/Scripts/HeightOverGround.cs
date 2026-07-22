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

//! @file "HeightOverGround.cs"
//! @brief Implementation of the HeightOverGround snippet, acts like IconUpdate, show height over ground
//! @author Thomas Krüger
//! @version 0
//! @date 16.07.2026

using System.Collections;
using tracer;
using UnityEngine;

public class HeightOverGround : MonoBehaviour{

    private const bool SHOW_ONLY_SELECTED = true;
    private const bool SHOW_ONLY_AT_TRANSLATE_GIZMO = true;

    private static Texture2D generatedMarkerTexture;

    private UIManager manager;
    private AbstractParameter selectedAbstractParam;
    private SnapSelect selectorSnapSelect;
    private int selectorCurrentSelectedSnapSelectElement = 0;

    private float groundY = 0f;
    
    // --- TO TWEAK ---
    private bool updateOngoing = true;
    private float textOffsetRight = 0.02f;
    private float dashesPerMeter = 16f;

    // --- State Variables ---
    private Transform target;
    private Camera cam;
    private float lastTargetY;
    private float lastGroundY;
    private bool isAnimating, isCreated, isShown = false;
    private float lastShownTime = 0;    //because we often deselect all SOs before selection, the viz would flip...

    // --- Generated Graphics ---
    private GameObject rootObj;
    private LineRenderer line;
    private TextMesh textMesh;
    private GameObject marker;
    private Material lineMat;
    private Material markerMat;
    private Material textMat;
    private Color materialColor = new Color(0.2f, 0.6f, 1f, 0.8f) ;

    /// <summary>
    /// Call this to initialize the visualization on a specific transform.
    /// </summary>
    public void Initialize(Transform targetTransform, UIManager _manager){
        manager = _manager;
        target = targetTransform;
        cam = Camera.main;
        lastTargetY = target.position.y;

        if (manager.GetStaticMashQuadTree() != null){
            groundY = manager.GetStaticMashQuadTree().GetHeightOverGround(RayMeshUtility.Accuracy.NearestVertex, target.position, 0f);
        }
        lastGroundY = groundY;

        //variable to show only "on selection" or "show all"
        if(!SHOW_ONLY_SELECTED)
            ShowViz();
    }

    /*********** LÖSUNG FOR 3DGS ****************

    Die Lösung für Splats (und komplexe Geometrie allgemein): Der Depth-Buffer Ansatz!

    Wenn ihr wisst, dass ihr später Gaussian Splats, extrem dichte Punktwolken oder hochkomplexe prozedurale Geometrie 
    (die für mathematisches Raycasting zu teuer ist) unterstützen wollt, solltet ihr das Paradigma wechseln:
        - Anstatt mathematische Linien durch den RAM zu ziehen, nutzt ihr die GPU!
        - Erstelle eine winzige, unsichtbare Orthographic-Camera (1x1 Pixel), die vom Objekt strikt nach unten schaut.
        - Lass diese Kamera in eine RenderTexture (Format: Depth) rendern.
        - Lies den einzigen Pixel dieser Depth-Texture aus.
        - Wandle den Depth-Wert (0.0 bis 1.0) über die Near/Far-Clip-Plane der Kamera wieder in Weltkoordinaten um.

    Der massive Vorteil: 
    Die Kamera rendert genau das, was visuell da ist. 
    Wenn ein Gaussian Splat in der Szene sichtbar ist, schreibt er automatisch in den Z-Buffer (Depth-Buffer) der GPU. 
    Die GPU rechnet dir die absolute, pixelperfekte Kollision mit der Splat-Wolke aus – 
    und das in Bruchteilen einer Millisekunde, völlig unabhängig davon, ob es ein Mesh, ein Splat oder ein Terrain ist.

    *********************************************/

    public void ShowViz(bool isSelected = false) {
        if(SHOW_ONLY_SELECTED && !isSelected)
            return;

        Debug.Log("SHOW VIZ");

        if(!isCreated)
            CreateGraphics(isSelected);

        if (isSelected) {
            if (manager.GetStaticMashQuadTree() != null){
                groundY = manager.GetStaticMashQuadTree().GetHeightOverGround(RayMeshUtility.Accuracy.NearestVertex, target.position, 0f);
            }
        }

        
        rootObj.SetActive(true);
        if(lastShownTime == 0 || Time.time - lastShownTime < 5)
            StartCoroutine(IntroRoutine());

        UpdateDepthViz(isSelected);
        
        //line mat would not be transparent
        //lineMat.SetFloat("_ZTest", zTestValue);

        isShown = true;
    }

    private void UpdateDepthViz(bool isSelected) {
        Debug.Log("UPDATE DEPTH VIZ");
        // In Unity's CompareFunction Enum:
        // 8 = Always (Renders through walls and clutter)
        // 4 = LessEqual (Normal 3D behavior, hidden by walls/objects in front of it)
        float zTestValue = isSelected ? 8f : 4f;
        markerMat.SetFloat("_ZTest", zTestValue);
        textMat.SetFloat("_ZTest", zTestValue);
    }

    public void HideViz() {
        if(isShown)
            lastShownTime = Time.time;

        if(isCreated)
            rootObj.SetActive(false);

        isShown = false;
    }

    private void Update(){
        if (isAnimating || !isCreated || !isShown || target == null) return;

        // Update the text alignment and facing every frame so it tracks the camera smoothly
        UpdateAlignments(groundY);

        if (updateOngoing){
            //for now, just assume we ware selected
            if (SHOW_ONLY_SELECTED) {
                if (manager.GetStaticMashQuadTree() != null){
                    groundY = manager.GetStaticMashQuadTree().GetHeightOverGround(RayMeshUtility.Accuracy.NearestVertex, target.position, 0f);
                }   
            }
            // Only update the line math and text value if the heights physically changed
            if (Mathf.Abs(target.position.y - lastTargetY) > 0.001f || Mathf.Abs(groundY - lastGroundY) > 0.001f){
                lastTargetY = target.position.y;
                lastGroundY = groundY;

                Vector3 worldTop = target.position;
                Vector3 worldBottom = new Vector3(worldTop.x, groundY, worldTop.z);

                line.SetPosition(0, Vector3.zero);
                line.SetPosition(1, line.transform.InverseTransformPoint(worldBottom));
                marker.transform.position = worldBottom;
                
                UpdateLineTiling();
                
                textMesh.text = (worldTop.y - groundY).ToString("F2") + "m";
            }
        }
    }

    // ==========================================
    // ANIMATION & BEHAVIOR
    // ==========================================

    private IEnumerator IntroRoutine(){
        isAnimating = true;
        Vector3 worldTop = target.position;
        Vector3 worldBottom = new Vector3(worldTop.x, groundY, worldTop.z);

        // 1. Initial State
        marker.transform.localScale = Vector3.zero;
        marker.transform.position = worldBottom;
        Color mColor = markerMat.color;
        mColor.a = 0f;
        markerMat.color = mColor;

        line.SetPosition(0, Vector3.zero);
        line.SetPosition(1, Vector3.zero);
        
        textMesh.transform.localScale = Vector3.zero;
        textMesh.text = "0.00m";

        // 2. Animate Marker (Scale with bounce and fade in)
        /*float markerDuration = 0.6f;
        for (float t = 0; t < 1f; t += Time.deltaTime / markerDuration){
            // Bouncy overshoot formula
            float scale = 1f - Mathf.Cos(t * Mathf.PI * 2.5f) * Mathf.Exp(-t * 4f);
            marker.transform.localScale = Vector3.one * scale;
            
            mColor.a = Mathf.Lerp(0f, 0.8f, t);
            markerMat.color = mColor;
            yield return null;
        }
        marker.transform.localScale = Vector3.one;

        // 3. Animate Line descending and Text counting up
        float descendDuration = 1.5f;
        for (float t = 0; t < 1f; t += Time.deltaTime / descendDuration){
            // Smooth ease-out
            float easeT = 1f - Mathf.Pow(1f - t, 3f);
            float currentY = Mathf.Lerp(worldTop.y, groundY, easeT);
            
            line.SetPosition(1, line.transform.InverseTransformPoint(new Vector3(worldTop.x, currentY, worldTop.z)));
            UpdateLineTiling();

            // Text visual updates
            textMesh.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, easeT * 2f); // Pops in early
            textMesh.text = (worldTop.y - currentY).ToString("F2") + "m";
            UpdateAlignments(currentY);

            yield return null;
        }*/

        // combined duration
        float duration = 1.0f;
        for (float t = 0; t < 1f; t += Time.deltaTime / duration){
            // Animate Marker (Scale with bounce and fade in)
            // Bouncy overshoot formula
            float scale = 1f - Mathf.Cos(t * Mathf.PI * 2.5f) * Mathf.Exp(-t * 4f);
            marker.transform.localScale = Vector3.one * scale;
            mColor.a = Mathf.Lerp(0f, 0.8f, t);
            markerMat.color = mColor;

            // Animate Line descending and Text counting up
            // Smooth ease-out
            float easeT = 1f - Mathf.Pow(1f - t, 3f);
            float currentY = Mathf.Lerp(worldTop.y, groundY, easeT);
            line.SetPosition(1, line.transform.InverseTransformPoint(new Vector3(worldTop.x, currentY, worldTop.z)));
            UpdateLineTiling();
            // Text visual updates
            textMesh.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, easeT * 2f); // Pops in early
            textMesh.text = (worldTop.y - currentY).ToString("F2") + "m";
            UpdateAlignments(currentY);

            yield return null;
        }
        marker.transform.localScale = Vector3.one;

        // Snap to exact finals
        line.SetPosition(1, line.transform.InverseTransformPoint(worldBottom));
        textMesh.text = (worldTop.y - groundY).ToString("F2") + "m";
        UpdateLineTiling();
        UpdateAlignments(groundY);

        // 4. Blink line and flash text
        MeshRenderer textRend = textMesh.GetComponent<MeshRenderer>();
        for (int i = 0; i < 4; i++){
            line.enabled = !line.enabled;
            textRend.enabled = line.enabled;
            yield return new WaitForSeconds(0.12f);
        }
        line.enabled = true;
        textRend.enabled = true;

        isAnimating = false;
    }

    private void UpdateAlignments(float currentBottomY){
        if (target == null || cam == null) return;

        Vector3 worldTop = target.position;
        Vector3 worldBottom = new Vector3(worldTop.x, currentBottomY, worldTop.z);

        // --- 1. Viewport Clamping for Text ---
        Vector3 screenTop = cam.WorldToViewportPoint(worldTop);
        Vector3 screenBottom = cam.WorldToViewportPoint(worldBottom);

        // Fallback to strict midpoint if behind the camera
        if (screenTop.z < 0 || screenBottom.z < 0){
            textMesh.transform.position = (worldTop + worldBottom) * 0.5f + cam.transform.right * textOffsetRight;
        }else{
            // Calculate safe visible screen boundaries (10% padding from top/bottom)
            float safeMax = Mathf.Min(Mathf.Max(screenTop.y, screenBottom.y), 0.9f);
            float safeMin = Mathf.Max(Mathf.Min(screenTop.y, screenBottom.y), 0.1f);
            
            // Aim for the center of the safe area
            float targetScreenY = (safeMax + safeMin) * 0.5f;

            // Clamp strict to ensure it never leaves the line segment boundaries
            targetScreenY = Mathf.Clamp(targetScreenY, Mathf.Min(screenTop.y, screenBottom.y), Mathf.Max(screenTop.y, screenBottom.y));

            // Map the clamped screen fraction back to the 3D world line segment
            float t = Mathf.InverseLerp(screenBottom.y, screenTop.y, targetScreenY);
            Vector3 worldPos = Vector3.Lerp(worldBottom, worldTop, t);

            // Apply the offset dynamically to the right of the camera view
            textMesh.transform.position = worldPos + cam.transform.right * textOffsetRight;
        }

        // --- 2. Camera Facing ---
        textMesh.transform.rotation = Quaternion.LookRotation(textMesh.transform.position - cam.transform.position);
    }

    private void UpdateLineTiling(){
        Vector3 worldStart = line.transform.TransformPoint(line.GetPosition(0));
        Vector3 worldEnd = line.transform.TransformPoint(line.GetPosition(1));
        float currentLength = Vector3.Distance(worldStart, worldEnd);
        
        // Scaling the X (or U) coordinate by length ensures the pattern pins to the top and visually 
        // repeats downwards instead of stretching like a rubber band.
        lineMat.mainTextureScale = new Vector2(currentLength * dashesPerMeter, 1f);
    }

    // ==========================================
    // PROCEDURAL GRAPHICS GENERATION
    // ==========================================

    private void CreateGraphics(bool isSelected){
        rootObj = new GameObject("HeightOverGround_Viz");
        rootObj.transform.SetParent(target); // Attach to keep hierarchy clean

        // 1. Setup Dotted Line
        GameObject lineObj = new GameObject("LineViz");
        lineObj.transform.SetParent(rootObj.transform);
        line = lineObj.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.startWidth = 0.01f;
        line.endWidth = 0.01f;
        line.positionCount = 2;
        line.textureMode = LineTextureMode.Tile; // Crucial for non-stretching dashed lines
        line.startColor = materialColor;
        line.endColor = materialColor;

        lineMat = new Material(Shader.Find("Unlit/Transparent")) { mainTexture = GenerateDashedTexture() };
        ConfigureParticleLineMaterial(lineMat);
        lineMat.SetFloat("_ZTest", 4f);
        line.material = lineMat;

        // 2. Setup Floating Text
        GameObject textObj = new GameObject("TextViz");
        textObj.transform.SetParent(rootObj.transform);
        textMesh = textObj.AddComponent<TextMesh>();
        textMesh.anchor = TextAnchor.MiddleLeft;
        textMesh.alignment = TextAlignment.Left;
        textMesh.fontSize = 100;
        textMesh.characterSize = 0.015f; // Keeps it sharp
        textMesh.color = materialColor;

        textMat = new Material(Shader.Find("Custom/TextZTest")) {
            mainTexture = textMesh.GetComponent<MeshRenderer>().sharedMaterial.mainTexture
        };
        textMat.SetFloat("_ZTest", 4f); // Standard-Tiefe aktivieren

        // 3. Setup Ground Marker
        marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
        marker.name = "GroundMarker";
        marker.transform.SetParent(rootObj.transform);
        Destroy(marker.GetComponent<Collider>()); // Clean up physics
        marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Face upwards
        marker.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

        markerMat = new Material(Shader.Find("Custom/HeightMarker")) {
            mainTexture = GenerateMarkerTexture(),
            color = materialColor // Light blue
        };
        // Ensure it starts in standard depth mode (4 = LEqual)
        markerMat.SetFloat("_ZTest", 4f);

        marker.GetComponent<MeshRenderer>().material = markerMat;

        if(isSelected && SHOW_ONLY_AT_TRANSLATE_GIZMO) {
            manager.UI2DCreated += UiCreationFinished;
        }

        rootObj.transform.localPosition = Vector3.zero;
        rootObj.SetActive(false);
        isCreated = true;
    }

    private Texture2D GenerateDashedTexture(){
        Texture2D tex = new Texture2D(32, 1, TextureFormat.RGBA32, false);
        for (int x = 0; x < 32; x++){
            // Half white, half transparent
            tex.SetPixel(x, 0, x < 16 ? Color.white : Color.clear);
        }
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        return tex;
    }

    private static Texture2D GenerateMarkerTexture(){
        if(generatedMarkerTexture)
            return generatedMarkerTexture;

        int size = 128;
        generatedMarkerTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        
        for (int x = 0; x < size; x++){
            for (int y = 0; y < size; y++){
                float dist = Vector2.Distance(new Vector2(x, y), center);
                
                // Draw Outer Circle
                bool isCircle = Mathf.Abs(dist - (size * 0.45f)) < 3f;
                
                // Draw Inner 'X'
                bool isX = (Mathf.Abs(x - y) < 4f || Mathf.Abs(x - (size - y)) < 4f) && dist < (size * 0.3f);

                if (isCircle || isX)
                    generatedMarkerTexture.SetPixel(x, y, Color.white);
                else
                    generatedMarkerTexture.SetPixel(x, y, Color.clear);
            }
        }
        generatedMarkerTexture.filterMode = FilterMode.Trilinear;
        generatedMarkerTexture.Apply();
        return generatedMarkerTexture;
    }

    public void ConfigureParticleLineMaterial(Material mat){
        if (mat == null) return;

        // 1. Assign the standard particle unlit shader
        mat.shader = Shader.Find("Particles/Standard Unlit");

        // 2. Set Rendering Mode to "Fade" (Index 2 in the dropdown)
        mat.SetFloat("_Mode", 2f);

        // 3. Manually apply the under-the-hood blend math for "Fade"
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        // 4. Toggle the correct Shader Keywords for Fade
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.DisableKeyword("_ALPHAMODULATE_ON");

        // 5. Set Color Mode to "Multiply" (Index 0 in the dropdown)
        // This allows the LineRenderer's Start/End gradient vertex colors to multiply properly
        mat.SetFloat("_ColorMode", 0f);
    }

    //only important to check for current selector if we have SHOW_ONLY_AT_TRANSLATE_GIZMO = true
    private void UiCreationFinished(object sender, UnityEngine.EventSystems.UIBehaviour uib) {
        if(selectorSnapSelect)
            selectorSnapSelect.parameterChanged -= ParamChange;

        if (uib != null && manager.SelectedObjects.Count > 0){
            selectorSnapSelect = (SnapSelect)uib;
            selectorSnapSelect.parameterChanged += ParamChange;
            
            selectorCurrentSelectedSnapSelectElement = 0;
            selectedAbstractParam = manager.SelectedObjects[0].parameterList[selectorCurrentSelectedSnapSelectElement];
            if(selectedAbstractParam.name != "position") {
                HideViz();
            }
        }
    }
    private void ParamChange(object sender, int manipulatorMode){
        selectorCurrentSelectedSnapSelectElement = manipulatorMode;
        selectedAbstractParam = manager.SelectedObjects[0].parameterList[selectorCurrentSelectedSnapSelectElement];
        if(selectedAbstractParam.name != "position") {
            HideViz();
        } else {
            ShowViz(true);
        }
    }

    public void DestroyViz() {
        Destroy(generatedMarkerTexture);
        generatedMarkerTexture = null;
        Destroy(rootObj);
        Destroy(this);
    }
}