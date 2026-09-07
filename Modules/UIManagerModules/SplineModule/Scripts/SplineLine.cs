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

//! @file "SplineLine.cs"
//! @brief Implementation of the 3D representation of the splinme and UI to add and remove keis.
//! @author Alexandru-Sebastian Tufis-Schwartz
//! @author Thomas Krüger
//! @author Simon Spielmann
//! @version 1.2
//! @date 1.09.2026


using System;
using System.Collections.Generic;
using tracer;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines; 

public class SplineLine : UIManagerModule
{
    //!
    //! Currently selected object.
    //!
    private SceneObject _animationTarget;
    //!
    //! The spline container.
    //!
    private SplineContainer _spline;
    //!
    //! The Animation Manager.
    //!
    private AnimationManager _animationManager;
    //!
    //! Distance between camera and spline.
    //!
    private float _keyHandleScale;
    //!
    //! Dictionary of spline existing in the World
    //!
    private Dictionary<GameObject, string> _sceneObjectsSplines = new Dictionary<GameObject, string>();
    //!
    //! Spline GO _parent
    //!
    private GameObject _splineHolder;
    //!
    //! Spline GO.
    //!
    private GameObject _splineGameObject;
    //!
    //! list of keyframe representing spheres
    //!
    private List<GameObject> _keyframeSpheres = new List<GameObject>();
    //!
    //! The Line renderer.
    //!
    private LineRenderer _lineRenderer;
    //! 
    //! The lines Material. 
    //! 
    private Material _lineMaterial;
    //! 
    //! The sheres Material. 
    //!
    private Material _sphereMaterial;
    //! 
    //! The number defining the lines resolution. 
    //!
    private const int LINE_SEGMENT_COUNT = 100; 
    //!
    //! Cached scnene manager.
    //!
    private SceneManager _sceneManager;

    //!
    //! Constructor
    //!
    public SplineLine(string name, Manager manager) : base(name, manager)
    {
    }

    //! 
    //! Function called when an Unity Start() m_callback is triggered
    //! 
    protected override void Start(object sender, EventArgs e)
    {
        base.Start(sender, e);
        _animationManager = core.getManager<AnimationManager>();
        _sceneManager = core.getManager<SceneManager>();

        // Subscribe to global managers
        manager.selectionChanged += selection;
        _animationManager.renewSplineContainer += executeRenewContainer;

        _lineMaterial = Resources.Load<Material>("Materials/LineRendererMaterial");
        _sphereMaterial = Resources.Load<Material>("Materials/keySphereMat");

        _splineHolder = new GameObject("SplineHolder");
        _splineHolder.transform.position = Vector3.zero;
        _splineHolder.layer = 11;
    }

    //! 
    //! Function called before Unity destroys the TRACER _core.
    //! 
    public override void Dispose()
    {
        base.Dispose();
        manager.selectionChanged -= selection;
        if (_animationManager != null)
        {
            _animationManager.renewSplineContainer -= executeRenewContainer;
            _animationManager.startAnimaGeneration -= StartAnimGen;
            _animationManager.stopAnimaGeneration -= StopAnimGen;
        }
    }

    //!
    //! Function called when selection has changed.
    //!
    private void selection(object sender, List<SceneObject> sceneObjects)
    {
        _animationManager.startAnimaGeneration -= StartAnimGen;
        _animationManager.stopAnimaGeneration -= StopAnimGen;

        if (sceneObjects == null || sceneObjects.Count < 1)
        {
            DelleteSplineContainer();
            _animationTarget = null;
            return;
        }

        // Setup new target safely
        _animationManager.startAnimaGeneration += StartAnimGen;
        _animationManager.stopAnimaGeneration += StopAnimGen;
        _animationTarget = sceneObjects[0];
    }

    //!
    //! Function called when _animCreatorButton is pressed 
    //!
    public void StartAnimGen(object sender, IAnimationParameter animationParameter) => RenewContainer();

    //!
    //! Function called when crate animation has been ended.  
    //!
    public void StopAnimGen(object sender, IAnimationParameter animationParameter) => DelleteSplineContainer();

    //!
    //! Function that creates a new spline when a key is updated
    //!
    private void executeRenewContainer(object sender, IAnimationParameter animationParameter) => RenewContainer();

    //!
    //! Function that Destroy the spline when an object is deselected
    //!
    public void DelleteSplineContainer()
    {
        for (int i = _keyframeSpheres.Count - 1; i >= 0; i--)
        {
            if (_keyframeSpheres[i] != null)
                UnityEngine.Object.Destroy(_keyframeSpheres[i]);
        }
        _keyframeSpheres.Clear();

        if (_splineGameObject != null)
        {
            _sceneObjectsSplines.Remove(_splineGameObject);
            UnityEngine.Object.Destroy(_splineGameObject);
            _splineGameObject = null;
            _spline = null;
            _lineRenderer = null;
        }
    }

    //!
    //! Function that creates a spline
    //!
    public void CreateSplineContainer()
    {
        if (_animationTarget == null) return;

        string splineName = $"{_animationTarget.name}Spline";
        _splineGameObject = CreateNewSplineGo(splineName);
        _sceneObjectsSplines.TryAdd(_splineGameObject, splineName); 
        _spline = _splineGameObject.AddComponent<SplineContainer>();
    }

    //!
    //! Function that redraw the spline
    //!
    private void RedrawSpline()
    {
        if (_animationTarget == null || _spline == null) return;

        var keys = _animationTarget.position.getKeys();
        if (keys == null) return;

        int keyCount = keys.Count;
        for (int i = 0; i < keyCount; i++)
        {
            if (keys[i] is Key<Vector3> v3Key)
            {
                CreateSplineControlPoint("knot", v3Key.value, v3Key.inTangent, v3Key.outTangent, v3Key.interpolation, _spline);
            }
        }

        if (keyCount >= 2)
        {
            DrawLineBetweenPoints();
        }
    }

    //!
    //! Function that draws the LineRenderer between the knots(key points)
    //!
    private void DrawLineBetweenPoints()
    {
        if (_animationTarget == null || _spline == null) return;

        var keys = _animationTarget.position.getKeys();
        if (keys == null || keys.Count == 0) return;

        if (_lineRenderer == null)
        {
            _lineRenderer = _splineGameObject.AddComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.material = _lineMaterial;

            _lineRenderer.sortingOrder = 999;              // High number ensures it draws last (on top)
        }

        Transform parentTransform = _animationTarget.transform.parent;
        Vector3 lineMiddle = Vector3.zero;
        int count = keys.Count;

        for (int i = 0; i < count; i++)
        {
            if (keys[i] is Key<Vector3> v3Key)
                lineMiddle += parentTransform != null ? parentTransform.TransformPoint(v3Key.value) : v3Key.value;
        }

        if (count > 0)
            lineMiddle /= count;

        // Dynamic scaling based on camera distance
        if (_sceneManager.mainCamera != null)
        {
            _keyHandleScale = Vector3.Distance(_sceneManager.mainCamera.transform.position, lineMiddle) / 100f;
            _lineRenderer.startWidth = _keyHandleScale / 3f;
            _lineRenderer.endWidth = _lineRenderer.startWidth;

            for (int i = 0; i < _keyframeSpheres.Count; i++)
            {
                if (_keyframeSpheres[i] != null)
                    _keyframeSpheres[i].transform.localScale = new Vector3(_keyHandleScale, _keyHandleScale, _keyHandleScale);
            }
        }

        // Draw the evaluated points along the spline curves
        _lineRenderer.positionCount = LINE_SEGMENT_COUNT + 1;
        for (int i = 0; i <= LINE_SEGMENT_COUNT; i++)
        {
            float t = i / (float)LINE_SEGMENT_COUNT;
            Vector3 worldSplinePoint = _spline.EvaluatePosition(t);
            _lineRenderer.SetPosition(i, worldSplinePoint);
        }
    }

    //!
    //! Function that creates a new spline when a key is updated
    //!
    public void RenewContainer()
    {
        DelleteSplineContainer();
        CreateSplineContainer();
        RedrawSpline();
    }

    //!
    //!Function that creates a new Spline GO
    //!
    public GameObject CreateNewSplineGo(string childName)
    {
        GameObject childObject = new GameObject(childName);
        childObject.layer = 11;
        childObject.transform.SetParent(_splineHolder.transform);
        childObject.transform.localPosition = Vector3.zero;
        return childObject;
    }

    //!
    //!Function that creates a new Knot GO
    //!
    public void CreateSplineControlPoint(string childName, Vector3 pos, float inTangentSlope, float outTangentSlope, AbstractKey.InterplolationTypes mode, SplineContainer spline)
    {
        BezierKnot knot;
        int newKnotIndex;

        switch (mode)
        {
            case AbstractKey.InterplolationTypes.BEZIER:
                // BEZIER: Calculate handle vectors based on the float slopes
                float handleXOffset = 1.0f; // Scale factor for the curve tightness
                float3 inHandle = new float3(-handleXOffset, inTangentSlope * -handleXOffset, 0f);
                float3 outHandle = new float3(handleXOffset, outTangentSlope * handleXOffset, 0f);

                knot = new BezierKnot(new float3(pos.x, pos.y, pos.z), inHandle, outHandle);
                _spline.Spline.Add(knot);

                // Set tangent mode to Broken so Unity evaluates the customized handles
                newKnotIndex = _spline.Spline.Count - 1;
                _spline.Spline.SetTangentMode(newKnotIndex, TangentMode.Broken);
                break;

            case AbstractKey.InterplolationTypes.LINEAR:
                // LINEAR: Create a clean knot without handles
                knot = new BezierKnot(new float3(pos.x, pos.y, pos.z));
                _spline.Spline.Add(knot);

                // Force Unity's spline to draw a perfectly straight line to the next point
                newKnotIndex = _spline.Spline.Count - 1;
                _spline.Spline.SetTangentMode(newKnotIndex, TangentMode.Linear);
                break;

            case AbstractKey.InterplolationTypes.STEP:
                // Create a clean knot without handles
                knot = new BezierKnot(new float3(pos.x, pos.y, pos.z));
                _spline.Spline.Add(knot);

                // For drawing step curves smoothly in a standard spline component, 
                // Unity treats it as linear. (See note below if you need an exact 90-degree visual step)
                newKnotIndex = _spline.Spline.Count - 1;
                _spline.Spline.SetTangentMode(newKnotIndex, TangentMode.Linear);
                break;
        }

        // Visual helper setup (Spheres)
        GameObject splineControlPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        splineControlPoint.name = childName;
        splineControlPoint.layer = 11;

        if (_sphereMaterial != null)
            splineControlPoint.GetComponent<Renderer>().sharedMaterial = _sphereMaterial;

        splineControlPoint.transform.SetParent(spline.gameObject.transform);
        splineControlPoint.transform.localPosition = pos;
        splineControlPoint.transform.localScale = new Vector3(_keyHandleScale, _keyHandleScale, _keyHandleScale);

        _keyframeSpheres.Add(splineControlPoint);
    }
}