
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

    private SplineContainer _spline;

    private List<AbstractParameter> _abstractParametersList;
    
    private int _selectorCurrentSelectedSnapSelectElement;

    private SnapSelect _selectorSnapSelect;

    private AbstractParameter _selectedAbstractParam;

    private AnimationManager _animationManager;

    private GameObject _addRemoveKeyPanel;

    private UICreator2DModule _creator2DModule;

    private InputManager _inputManager;
    private bool _updateLineWhenZooming;

    private float _keyHandleScale;

    //TODO MOD WITH REAL TIME 
    private int _timeeee;

    //!
    //! Dictionary of spline existing in the World
    //!
    private Dictionary<GameObject, string> _sceneObjectsSplines = new Dictionary<GameObject, string>();
    //!
    //! Spline GO _parent
    //!
    private GameObject _splineHolder;

    private GameObject _splineGameObject;

    private Vector3 _pos;

    //!
    //! list of keyframe representing spheres
    //!
    private List<GameObject> _keyframeSpheres;

    //!
    //! Reference to UIManager
    //!
    UIManager _mUIManager;

    //!
    //! The UI button for logging the camera to an object.
    //!
    private MenuButton _animCreatorButton;

    private Button _addKeyButton;

    private Button _removeKeyButton;
    
    private Button _removeAnimationButton;

    private bool _removeKey;

    private Transform _keyCanvasTrans;
    
    private LineRenderer _lineRenderer;

    public MenuButton animCreatorButton()
    {
        return _animCreatorButton;
    }

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

    protected override void Cleanup(object sender, EventArgs e)
    {
        base.Cleanup(sender, e);
        _mUIManager.selectionChanged -= selection;
        _mUIManager.UI2DCreated -= grabUI2D;
    }


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

    void grabUI2D(object sender, UIBehaviour ui)
    {
        _selectorSnapSelect = (SnapSelect) ui;
        _selectorSnapSelect.parameterChanged += ParamChange;
    }

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





    public void AddKey()
    {
        UpdateKey(false);
    }

    public void RemoveKey()
    {
        UpdateKey(true);
    }
    
    public void RemoveAnimation()
    {
        UpdateKey(false, true);
    }

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
        
        _animationManager.keyframesUpdated(_selectedAbstractParam);
    }
    
    public void ApplyKeyUpdate<T>(Parameter<T> parameter, bool removeKey = false, bool removeAll = false)
    {
        if (removeKey && !removeAll)
        {
            int idx = parameter.getKeys().FindIndex(i => i.time == _animationManager.time);
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
    
    public void DelleteSplineContainer()
    {
        if (_splineGameObject!= null)
        {
            Object.DestroyImmediate(_splineGameObject);
        }
    }

    public void CreateSplineContainer()
    {
        String splineName = new string(_animationTarget.name + "Spline");
        _splineGameObject = CreateNewSplineGo(splineName);
        _sceneObjectsSplines.Add(_splineGameObject, splineName);
        _spline = _splineGameObject.AddComponent<SplineContainer>();

    }
    
    public void RenewContainer()
    {
        DelleteSplineContainer();
        CreateSplineContainer();
        RedrawSpline();
    }

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

    private void EventCallDrawLineBetweenPoints(object sender, float distance)
    {
        DrawLineBetweenPoints();
    }

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



        /*for (int i = 0; i < keyList.Count; i++)
        {
            _lineRenderer.SetPosition(i, ((Key<Vector3>)keyList[i]).value);
        }*/

    }

    public SplineLine(string name, Manager manager) : base(name, manager)
    {
    }

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
    
    public void ParamChange(object sender, int manipulatorMode)
    {
        _selectorCurrentSelectedSnapSelectElement = manipulatorMode;
        
    }
}







