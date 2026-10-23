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
//! @version 1
//! @date 23.10.2026
//! @changed doxygen style documentation

using System.Collections;
using tracer;
using UnityEngine;

    //!
    //! implements a vizualisation of a height over a certain  "ground"
    //! gets initialized on choosen objects via IconCreatorModule (right now only cameras)
    //!

public class HeightOverGround : MonoBehaviour{

    //!
    //! show this only on selected objects or on all objects this got initialized at
    //!
    private const bool SHOW_ONLY_SELECTED = true;
    //!
    //! show it only on selected object in translate-manipulation mode or all the time?
    //!
    private const bool SHOW_ONLY_AT_TRANSLATE_GIZMO = true;
    //!
    //! we create a viz at runtime, only once and share it with all instances of this
    //!
    private static Texture2D generatedMarkerTexture;
    //!
    //! we create a viz at runtime, only once and share it with all instances of this
    //!
    private static Texture2D generatedDashTexture;

    //!
    //! reference to the UI manager, since we are not a module but a helper class
    //!
    private UIManager manager;
    //!
    //! parameter reference so we know what we are currently editing (showing in ui), see SHOW_ONLY_AT_TRANSLATE_GIZMO
    //!
    private AbstractParameter selectedAbstractParam;
    //!
    //! reference to the ui element that we add listeners to, to check if its state changed to tranlsate (see above)
    //!
    private SnapSelect selectorSnapSelect;
    //!
    //! current calculated height over ground value
    //!
    private float groundY = 0f;
    
    // --- TO TWEAK ---
    //!
    //! update value independent within our Update function
    //!
    private const bool ONGOING_SELF_UPDATE = true;
    //!
    //! text mesh pro text transform offset to the right side of our "height line"
    //!
    private const float TEXT_OFFSET_RIGHT = 0.02f;
    //!
    //! our materials mainTextureScale X value which indicates the "dots per meter"
    //!
    private const float VIZ_DASHES_PER_METER = 16f;

    // --- State Variables ---
    //!
    //! the transform target we calculate the height for
    //!
    private Transform target;
    //!
    //! the camera used for aligning the viz to
    //!
    private Camera cam;
    //!
    //! temporary saved height y value to stop ONGOING_SELF_UPDATE if difference to current value is too small
    //!
    private float lastTargetY;
    //!
    //! temporary saved ground y value to stop ONGOING_SELF_UPDATE if difference to current value is too small
    //!
    private float lastGroundY;
    //!
    //! bools to avaid updating alignment if any is not set
    //!
    private bool isAnimating, isCreated, isShown = false;
    //!
    //! temporary fix to avoid flipping, because we often deselect all SOs when making a selection
    //!
    private float lastShownTime = 0;

    // --- Generated Graphics ---
    //!
    //! reference to runtime created root GameObject of our visualisation
    //!
    private GameObject rootObj;
    //!
    //! reference to runtime created height line
    //!
    private LineRenderer line;
    //!
    //! reference to runtime created text mesh pro for height-text
    //!
    private TextMesh textMesh;
    //!
    //! reference to runtime created ground marker
    //!
    private GameObject marker;
    //!
    //! reference to runtime created Material for height line
    //!
    private Material lineMat;
    //!
    //! reference to runtime created Material for marker
    //!
    private Material markerMat;
    //!
    //! reference to runtime created Material for text
    //!
    private Material textMat;
    //!
    //! tweakable value for all colors (line, marker, text)
    //!
    private readonly Color materialColor = new Color(0.2f, 0.6f, 1f, 0.8f) ;

    //!
    //! Call this to initialize the visualization on a specific transform.
    //!
    //! @param targetTransform the transform where we calculate the height for
    //! @param _manager reference to the UIManager (to access MeshQuadTree)
    //!
    public void Initialize(Transform targetTransform, UIManager _manager){
        manager = _manager;
        target = targetTransform;
        cam = Camera.main;
        lastTargetY = target.position.y;

        if (manager.GetStaticMeshQuadTree() != null){
            groundY = manager.GetStaticMeshQuadTree().GetHeightOverGround(RayMeshUtility.Accuracy.NearestVertex, target.position, 0f);
        }
        lastGroundY = groundY;

        if(!SHOW_ONLY_SELECTED)
            ShowViz();
    }

    /*********** LÖSUNG FOR 3DGS ****************
    Lösung für Splats? (und komplexe Geometrie allgemein): Der Depth-Buffer Ansatz:

    Gaussian Splats, extrem dichte Punktwolken oder hochkomplexe prozedurale Geometrie 
    (die für mathematisches Raycasting zu teuer ist) Support: Paradigma-Wechsel:
        - Anstatt mathematische Linien per RAM zu ziehen -> GPU!
        - > unsichtbare Orthographic-Camera (1x1 Pixel), schaut vom Objekt strikt nach unten
        - > rendert in RenderTexture (Format: Depth)
        - > read Single Pixel aus Depth-Texture
        - > Depth-Wert (0.0 bis 1.0) über Cam' Near/Far-Clip-Plane in Weltkoordinaten umrechnen

    Vorteil: 
    Cam rendert genau das, was visuell da ist. 
    Wenn ein Gaussian Splat in der Szene sichtbar ist, schreibt er automatisch in den Z-Buffer (Depth-Buffer) der GPU. 
    Die GPU rechnet dir die absolute, pixelperfekte Kollision mit der Splat-Wolke aus – 
    und das in Bruchteilen einer Millisekunde, völlig unabhängig davon, ob es ein Mesh, ein Splat oder ein Terrain ist.
    *********************************************/

    //!
    //! Call to show Height Over Ground Viz. Creates viz (if not already), animates it and updates it
    //!
    //! @param isSelected show because it is selected?
    //!
    public void ShowViz(bool isSelected = false) {
        if(SHOW_ONLY_SELECTED && !isSelected)
            return;

        //Debug.Log("SHOW VIZ");

        //create if not already done
        if(!isCreated)
            CreateGraphics(isSelected);
        //abort if could not be created
        if(!isCreated)
            return;

        //calculate height via given settings
        if (manager.GetStaticMeshQuadTree() != null){
            groundY = manager.GetStaticMeshQuadTree().GetHeightOverGround(RayMeshUtility.Accuracy.NearestVertex, target.position, 0f);
        }

        //set active
        rootObj.SetActive(true);

        //if last selection time was long ago, play intro routine
        if(lastShownTime == 0 || Time.time - lastShownTime < 5)
            StartCoroutine(IntroRoutine());

        //update the material settings accordingly
        UpdateDepthViz(isSelected);
        
        //line mat would not be transparent
        //lineMat.SetFloat("_ZTest", zTestValue);

        isShown = true;
    }

    //!
    //! Update marker and text materials ZTestr value accordingly to if the object is selected
    //! because we will show the viz through walls if we only show the selected ones
    //!
    //! @param isSelected do we call this on the selected object?
    //!
    private void UpdateDepthViz(bool isSelected) {
        // Debug.Log("UPDATE DEPTH VIZ");
        // In Unity's CompareFunction Enum:
        // 8 = Always (Renders through walls and clutter)
        // 4 = LessEqual (Normal 3D behavior, hidden by walls/objects in front of it)
        float zTestValue = isSelected ? 8f : 4f;
        markerMat.SetFloat("_ZTest", zTestValue);
        textMat.SetFloat("_ZTest", zTestValue);
    }

    //!
    //! hide the visualisation
    //!
    public void HideViz() {
        if(isShown)
            lastShownTime = Time.time;

        if(isCreated)
            rootObj.SetActive(false);

        isShown = false;
    }
    //!
    //! Update function for either ongoing calculation or just updating the viz to face the camera
    //!
    private void Update(){
        if (isAnimating || !isCreated || !isShown || target == null) return;

        // Update the text alignment and facing every frame so it tracks the camera smoothly
        UpdateAlignments(groundY);

        if (ONGOING_SELF_UPDATE){
            //for now, just assume we ware selected
            if (SHOW_ONLY_SELECTED) {
                if (manager.GetStaticMeshQuadTree() != null){
                    groundY = manager.GetStaticMeshQuadTree().GetHeightOverGround(RayMeshUtility.Accuracy.NearestVertex, target.position, 0f);
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
    //!
    //! intro animation (scales up ground marker, extend line from a to b, fold out text and count height op)
    //!
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

    //!
    //! sets position and rotation of viz to face camera, positions text in the middle OR within camera viewport
    //!
    //! @param currentBottomY
    //!
    private void UpdateAlignments(float currentBottomY){
        if (target == null || cam == null) return;

        Vector3 worldTop = target.position;
        Vector3 worldBottom = new Vector3(worldTop.x, currentBottomY, worldTop.z);

        // --- 1. Viewport Clamping for Text ---
        Vector3 screenTop = cam.WorldToViewportPoint(worldTop);
        Vector3 screenBottom = cam.WorldToViewportPoint(worldBottom);

        // Fallback to strict midpoint if behind the camera
        if (screenTop.z < 0 || screenBottom.z < 0){
            textMesh.transform.position = (worldTop + worldBottom) * 0.5f + cam.transform.right * TEXT_OFFSET_RIGHT;
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
            textMesh.transform.position = worldPos + cam.transform.right * TEXT_OFFSET_RIGHT;
        }

        // --- 2. Camera Facing ---
        textMesh.transform.rotation = Quaternion.LookRotation(textMesh.transform.position - cam.transform.position);
    }
    //!
    //! updates line tiling by height distance (so it stays the same)
    //!
    private void UpdateLineTiling(){
        Vector3 worldStart = line.transform.TransformPoint(line.GetPosition(0));
        Vector3 worldEnd = line.transform.TransformPoint(line.GetPosition(1));
        float currentLength = Vector3.Distance(worldStart, worldEnd);
        
        // Scaling the X (or U) coordinate by length ensures the pattern pins to the top and visually 
        // repeats downwards instead of stretching like a rubber band.
        lineMat.mainTextureScale = new Vector2(currentLength * VIZ_DASHES_PER_METER, 1f);
    }

    // ==========================================
    // PROCEDURAL GRAPHICS GENERATION
    // ==========================================

    //!
    //! Looks up a shader by name and logs an error instead of returning null silently.
    //! Only shaders that ship in a Resources folder are guaranteed to exist in player builds;
    //! built-in shaders are stripped unless referenced (Editor always finds them, devices don't).
    //!
    //! @param name name/path of the Shader to find
    //!
    private Shader FindShaderOrLog(string name){
        Shader shader = Shader.Find(name);
        if (shader == null)
            Debug.LogError("HeightOverGround: shader '" + name + "' not found. It is probably stripped from this build.");
        return shader;
    }

    //!
    //! create the viz (new gameobject, linerenderer, textmesh) and add listener if applicable
    //!
    //! @param isSelected did we call this via selection?
    //!
    private void CreateGraphics(bool isSelected){
        Shader lineShader = FindShaderOrLog("Custom/HeightLine");
        Shader textShader = FindShaderOrLog("Custom/TextZTest");
        Shader markerShader = FindShaderOrLog("Custom/HeightMarker");
        if (lineShader == null || textShader == null || markerShader == null)
            return; // isCreated stays false, ShowViz() becomes a no-op

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
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        lineMat = new Material(lineShader) { mainTexture = GenerateDashedTexture() };
        lineMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
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

        Material fontMaterial = textMesh.GetComponent<MeshRenderer>().sharedMaterial;
        textMat = new Material(textShader) {
            mainTexture = fontMaterial != null ? fontMaterial.mainTexture : null
        };
        textMat.SetFloat("_ZTest", 4f); // Standard-Tiefe aktivieren

        // 3. Setup Ground Marker
        marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
        marker.name = "GroundMarker";
        marker.transform.SetParent(rootObj.transform);
        Destroy(marker.GetComponent<Collider>()); // Clean up physics
        marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Face upwards
        marker.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

        markerMat = new Material(markerShader) {
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
    //!
    //! create our dash texture at runtime (once)
    //!
    private Texture2D GenerateDashedTexture(){
        if(generatedDashTexture)
            return generatedDashTexture;

        generatedDashTexture = new Texture2D(32, 1, TextureFormat.RGBA32, false);
        for (int x = 0; x < 32; x++){
            // Half white, half transparent
            generatedDashTexture.SetPixel(x, 0, x < 16 ? Color.white : Color.clear);
        }
        generatedDashTexture.wrapMode = TextureWrapMode.Repeat;
        generatedDashTexture.filterMode = FilterMode.Point;
        generatedDashTexture.Apply();
        return generatedDashTexture;
    }
    //!
    //! create our marker texture at runtime (once)
    //!
    private Texture2D GenerateMarkerTexture(){
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

    //!
    //! Listener on UI2DCreated
    //! only done to check for current selector if we have set SHOW_ONLY_AT_TRANSLATE_GIZMO
    //!
    private void UiCreationFinished(object sender, UnityEngine.EventSystems.UIBehaviour uib) {
        if(selectorSnapSelect)
            selectorSnapSelect.parameterChanged -= ParamChange;

        if (uib != null && manager.SelectedObjects.Count > 0){
            selectorSnapSelect = (SnapSelect)uib;
            selectorSnapSelect.parameterChanged += ParamChange;
            
            int manipulatorMode = 0;
            selectedAbstractParam = manager.SelectedObjects[0].parameterList[manipulatorMode];
            if(selectedAbstractParam.name != "position") {
                HideViz();
            }
        }
    }
    //!
    //! Listener on parameterChanged for our SnapSelect
    //! only done to check for current selector if we have set SHOW_ONLY_AT_TRANSLATE_GIZMO
    //!
    private void ParamChange(object sender, int manipulatorMode){
        selectedAbstractParam = manager.SelectedObjects[0].parameterList[manipulatorMode];
        if(selectedAbstractParam.name != "position") {
            HideViz();
        } else {
            ShowViz(true);
        }
    }
    //!
    //! Destroy our viz via IconCreatoreModul.disposeIcons
    //!
    public void DestroyViz() {
        Destroy(generatedMarkerTexture);
        generatedMarkerTexture = null;
        if (rootObj)
            Destroy(rootObj);
        Destroy(this);
    }
}