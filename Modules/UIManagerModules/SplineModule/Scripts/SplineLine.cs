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
//! @version 0
//! @date 23.08.2024


using System;
using System.Collections.Generic;
using tracer;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Splines;
using UnityEngine.UI;
using Object = UnityEngine.Object;


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
    //! The index of the currently selected SnapSelect element in the selector
    //!
    private int _selectorCurrentSelectedSnapSelectElement;
    //!
    //! Reference to the selector SnapSelect component
    //!
    private SnapSelect _selectorSnapSelect;
    //!
    //! The currently selected abstract parameter
    //!
    private AbstractParameter _selectedAbstractParam;
    //!
    //! The Animation Manager.
    //!
    private AnimationManager _animationManager;
    //!
    //! Controll panel for addin and removing keys.
    //!
    private GameObject _addRemoveKeyPanel;
    //!
    //! Ref to the UICreator2DModule.
    //!
    private UICreator2DModule _creator2DModule;
    //!
    //! Ref to InputManager.
    //!
    private InputManager _inputManager;
    //!
    //! Boolean for updateing the size of the line renderer and knots(Sphear GO).
    //!
    private bool _updateLineWhenZooming;
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
    private List<GameObject> _keyframeSpheres;
    //!
    //! Reference to UIManager
    //!
    UIManager _mUIManager;
    //!
    //! The UI button for starting the Animation ui.
    //!
    private MenuButton _animCreatorButton;
    //!
    //! The UI button to add a key.
    //!
    private Button _addKeyButton;
    //!
    //! The UI button to remove a key.
    //!
    private Button _removeKeyButton;
    //!
    //! The UI button to remove all keys.
    //!
    private Button _removeAnimationButton;
    //!
    //! Transform of the KeyCanvas.
    //!
    private Transform _keyCanvasTrans;
    //!
    //! The Line renderer.
    //!
    private LineRenderer _lineRenderer;

    //! 
    //! Function called when an Unity Start() m_callback is triggered
    //! 
    protected override void Start(object sender, EventArgs e)
    {
        base.Start(sender, e);
        _mUIManager = core.getManager<UIManager>();
        _animationManager = core.getManager<AnimationManager>();
        _inputManager = core.getManager<InputManager>();
        _addRemoveKeyPanel = Resources.Load<GameObject>("Prefabs/AddRemoveKeyPanel");
        
        
        
        _mUIManager.selectionChanged += selection;
        _mUIManager.UI2DCreated += grabUI2D;
        _splineHolder = new GameObject("SplineHolder");
        _splineHolder.transform.position = new Vector3(0, 0, 0);
        
    }

    //! 
    //! Function called before Unity destroys the TRACER _core.
    //! 
    protected override void Cleanup(object sender, EventArgs e)
    {
        base.Cleanup(sender, e);
        _mUIManager.selectionChanged -= selection;
        _mUIManager.UI2DCreated -= grabUI2D;
    }

    //!
    //! Function called when selection has changed.
    //!
    private void selection(object sender, List<SceneObject> sceneObjects)
    {
        if (_animCreatorButton != null)
        {
            _mUIManager.removeButton(_animCreatorButton);
            DelleteSplineContainer();
            _selectorSnapSelect.parameterChanged -= ParamChange;
            _animCreatorButton = null;

            if (_keyCanvasTrans != null)
            {
                GameObject.DestroyImmediate(_keyCanvasTrans.gameObject);
                _addKeyButton.onClick.RemoveAllListeners();
                _removeKeyButton.onClick.RemoveAllListeners();
                _removeAnimationButton.onClick.RemoveAllListeners();

            }

            if (_updateLineWhenZooming)
            {
                _inputManager.pinchEvent -= EventCallDrawLineBetweenPoints;
                _updateLineWhenZooming = false;
            }
            
        }

        if (sceneObjects.Count > 0)
        {
            _animCreatorButton = new MenuButton("", StartAnimGen, null, "animCreatorButton ");
            _animCreatorButton.setIcon("Images/animationCreator");
            _mUIManager.addButton(_animCreatorButton);
            _animationTarget = sceneObjects[0];
            
            _selectedAbstractParam = _animationTarget.parameterList[_selectorCurrentSelectedSnapSelectElement];
            _animCreatorButton.isHighlighted = false;
        }
    }

    //!
    //! Function to get the UI2D.
    //!
    void grabUI2D(object sender, UIBehaviour ui)
    {
        _selectorSnapSelect = (SnapSelect) ui;
        _selectorSnapSelect.parameterChanged += ParamChange;
    }

    //!
    //! Function called when _animCreatorButton is pressed 
    //!
    public void StartAnimGen()
    {
        Transform ui2D = _mUIManager.getModule<UICreator2DModule>().UI2DCanvas;
        _keyCanvasTrans = SceneObject.Instantiate(_addRemoveKeyPanel.transform, ui2D);
        
        _animCreatorButton.isHighlighted = true;
        RenewContainer();
        
        _addKeyButton = _keyCanvasTrans.GetChild(1).GetComponent<Button>();
        _addKeyButton.onClick.AddListener(AddKey);
        _removeKeyButton = _keyCanvasTrans.GetChild(2).GetComponent<Button>();
        _removeKeyButton.onClick.AddListener(RemoveKey);
        _removeAnimationButton = _keyCanvasTrans.GetChild(3).GetComponent<Button>();
        _removeAnimationButton.onClick.AddListener(RemoveAnimation);

    }




    //!
    //! Function used to add a key.
    //!
    public void AddKey()
    {
        UpdateKey(false);
    }

    //!
    //! Function used to remove a key.
    //!
    public void RemoveKey()
    {
        UpdateKey(true);
    }
    
    //!
    //! Function used to remove all keys.
    //!
    public void RemoveAnimation()
    {
        UpdateKey(false, true);
    }

    //!
    //! Function used to Update a key (add or remove).
    //!
    public void UpdateKey(bool removeKey, bool removeAll = false)
    {
        if (_selectedAbstractParam is Parameter<bool> boolParam)
        {
            ApplyKeyUpdate(boolParam, removeKey, removeAll);
        }
        if (_selectedAbstractParam is Parameter<int> intParam)
        {
            ApplyKeyUpdate(intParam, removeKey, removeAll);
        }
        if (_selectedAbstractParam is Parameter<float> floatParam)
        {
            ApplyKeyUpdate(floatParam, removeKey, removeAll);
        }
        if (_selectedAbstractParam is Parameter<Vector2> vector2Param)
        {
            ApplyKeyUpdate(vector2Param, removeKey, removeAll);
        }
        else if (_selectedAbstractParam is Parameter<Vector3> vector3Param)
        {
            ApplyKeyUpdate(vector3Param, removeKey, removeAll);
            if (_selectedAbstractParam.name == "position")
            {
                RenewContainer();
            }
        }
        if (_selectedAbstractParam is Parameter<Vector4> vector4Param)
        {
            ApplyKeyUpdate(vector4Param, removeKey, removeAll);
        }
        if (_selectedAbstractParam is Parameter<quaternion> quaternionParam)
        {
            ApplyKeyUpdate(quaternionParam, removeKey, removeAll);
        }
        
        _animationManager.keyframesUpdated(_selectedAbstractParam as IAnimationParameter);
    }
    
    //!
    //! Function used to apply the key update
    //!
    public void ApplyKeyUpdate<T>(Parameter<T> parameter, bool removeKey = false, bool removeAll = false)
    {
        if (removeKey && !removeAll)
        {
            int idx = parameter.getKeys().FindIndex(i => Mathf.RoundToInt(i.time * 30) == Mathf.RoundToInt(_animationManager.time * 30));
            if (idx >= 0)
            {
                parameter.removeKeyAtIndex(idx);
            }
        }
        else
        {
            parameter.setKey();
        }

        if (removeAll)
        {
            parameter.clearKeys();
        }
    
    }
    
    //!
    //! Function that Destroy the spline when an object is deselected
    //!
    public void DelleteSplineContainer()
    {
        if (_splineGameObject!= null)
        {
            Object.DestroyImmediate(_splineGameObject);
        }
    }

    //!
    //! Function that creates a spline
    //!
    public void CreateSplineContainer()
    {
        String splineName = new string(_animationTarget.name + "Spline");
        _splineGameObject = CreateNewSplineGo(splineName);
        _sceneObjectsSplines.Add(_splineGameObject, splineName);
        _spline = _splineGameObject.AddComponent<SplineContainer>();
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
    //! Function that redraw the spline
    //!
    private void RedrawSpline()
    {
        if (_animationTarget.position.getKeys() != null)
        {
            foreach (var key in _animationTarget.position.getKeys())
            {
                CreateSplineControlPoint("knot", ((Key<Vector3>)key).value, _spline);
            }

            if (_animationTarget.position.getKeys().Count >= 2)
            {
                if (_lineRenderer == null)
                {
                    DrawLineBetweenPoints();
                }

                if (!_updateLineWhenZooming)
                {
                    _inputManager.pinchEvent += EventCallDrawLineBetweenPoints;
                    _updateLineWhenZooming = true;
                }
            }
        }
    }

    //!
    //! event listner when pinchEvent is triggerd 
    //!
    private void EventCallDrawLineBetweenPoints(object sender, float distance)
    {
        DrawLineBetweenPoints();
    }

    //!
    //! Function that draws the LineRenderer between the knots(key points)
    //!
    private void DrawLineBetweenPoints()
    {
        if (_lineRenderer == null)
        {
            _lineRenderer = _splineGameObject.AddComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = true;
        }
        List<AbstractKey> keyList = _animationTarget.position.getKeys();
        
        
        // make line nice
        Vector3 lineMiddle = Vector3.zero;
        foreach (var key in _animationTarget.position.getKeys())
        {
            lineMiddle += _animationTarget.transform.parent.TransformPoint(((Key<Vector3>)key).value);
        }

        lineMiddle /= _animationTarget.position.getKeys().Count;
        _keyHandleScale = Vector3.Distance(Camera.main.transform.position, lineMiddle) / 100f;
        _lineRenderer.startWidth = _keyHandleScale /3f;
        _lineRenderer.endWidth = _lineRenderer.startWidth;
        foreach (var obj in _spline.gameObject.GetComponentsInChildren<Transform>())
        {
            if (obj != _spline.transform)
            {
                obj.localScale = new Vector3(_keyHandleScale, _keyHandleScale, _keyHandleScale);
            }
        }
        // nice nice 
        
        _lineRenderer.material = Resources.Load<Material>("Materials/LineRendererMaterial");
        int lineSegmentCount = 100;
        _lineRenderer.positionCount = lineSegmentCount + 1;
        
        for (int i = 0; i <= lineSegmentCount; i++)
        {
            float t = i / (float)lineSegmentCount;  // Normalized time along the spline
            Vector3 point = _spline.EvaluatePosition(t);  // Evaluate the position on the spline at this time
            _lineRenderer.SetPosition(i, point);  // Set this position on the LineRenderer
        }
    }

    public SplineLine(string name, Manager manager) : base(name, manager)
    {
    }

    //!
    //!Function that creates a new Spline GO
    //!
    public GameObject CreateNewSplineGo(string childName)
    {
        // Create a new GameObject
        GameObject childObject = new GameObject(childName);

        // Set the _parent of the new GameObject to the specified _parent
        childObject.transform.SetParent(_splineHolder.transform);

        // Set the local position of the child GameObject relative to its _parent
        childObject.transform.localPosition = new Vector3(0, 0, 0);

        return childObject;
    }


    //!
    //!Function that creates a new Knot GO
    //!
    public void CreateSplineControlPoint(string childName, Vector3 pos, SplineContainer spline)
    {
        BezierKnot knot = new BezierKnot(new float3(pos.x, pos.y, pos.z));
        _spline.Spline.Add(knot);
        _spline.Spline.SetTangentMode(0);
        // Create a new GameObject
        GameObject splineControlPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        splineControlPoint.GetComponent<Renderer>().material = Resources.Load<Material>("Materials/keySphereMat");

        splineControlPoint.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

        // Set the _parent of the new GameObject to the specified _parent
        splineControlPoint.transform.SetParent(spline.gameObject.transform);

        // Set the local position of the child GameObject relative to its _parent
        splineControlPoint.transform.localPosition = pos;
    }
    
    //!
    //!Function called when parameter has changed
    //!
    public void ParamChange(object sender, int manipulatorMode)
    {
        _selectorCurrentSelectedSnapSelectElement = manipulatorMode;
        _selectedAbstractParam = _animationTarget.parameterList[_selectorCurrentSelectedSnapSelectElement];
        
    }
}







