
using System;
using System.Collections.Generic;
using tracer;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
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

    private MenuButton _addKeyButton;

    private MenuButton _removeKeyButton;

    private bool _removeKey;

    public MenuButton animCreatorButton()
    {
        return _animCreatorButton;
    }

    protected override void Start(object sender, EventArgs e)
    {
        base.Start(sender, e);
        _mUIManager = core.getManager<UIManager>();
        _animationManager = core.getManager<AnimationManager>();
        _mUIManager.selectionChanged += selection;
        _splineHolder = new GameObject("SplineHolder");
        _splineHolder.transform.position = new Vector3(0, 0, 0);
        
    }

    protected override void Cleanup(object sender, EventArgs e)
    {
        base.Cleanup(sender, e);
        _mUIManager.selectionChanged -= selection;
    }


    private void selection(object sender, List<SceneObject> sceneObjects)
    {
        if (_animCreatorButton != null)
        {
            _mUIManager.removeButton(_animCreatorButton);
            if (_addKeyButton != null)
            {
                _mUIManager.removeButton(_addKeyButton);
                _mUIManager.removeButton(_removeKeyButton);

            }
            DelleteSplineContainer();
            _selectorSnapSelect.parameterChanged -= ParamChange;
            _animCreatorButton = null;
        }

        if (sceneObjects.Count > 0)
        {
            _animCreatorButton = new MenuButton("", StartAnimGen, null, "animCreatorButton ");
            _animCreatorButton.setIcon("Images/animationCreator");
            _mUIManager.addButton(_animCreatorButton);
            _animationTarget = sceneObjects[0];
            
            _selectedAbstractParam = _animationTarget.parameterList[_selectorCurrentSelectedSnapSelectElement];
            
            _selectorSnapSelect = GameObject.Find("PRE_UI_AddSelector(Clone)").GetComponent<SnapSelect>();
            _animCreatorButton.isHighlighted = false;
            _selectorSnapSelect.parameterChanged += ParamChange;
        }
    }

    public void StartAnimGen()
    {
        _animCreatorButton.isHighlighted = true;
        RenewContainer();
        
        _addKeyButton = new MenuButton("", AddKey, null, "_addKeyButton ");
        _addKeyButton.setIcon("Images/key");
        _mUIManager.addButton(_addKeyButton);
        
        
        //TODO MAKE THIS BUTTON ONLY VISIBLE IF THERE IS A KEY AT THAT FRAME!
        _removeKeyButton = new MenuButton("", RemoveKey, null, "_removeKeyButton ");
        _removeKeyButton.setIcon("Images/notkey");
        _mUIManager.addButton(_removeKeyButton);

    }





    public void AddKey()
    {
        UpdateKey(false);
    }

    public void RemoveKey()
    {
        UpdateKey(true);
    }

    public void UpdateKey(bool removeKey)
    {
        if (_selectedAbstractParam is Parameter<bool> boolParam)
        {
            ApplyKeyUpdate(boolParam, removeKey);
        }
        if (_selectedAbstractParam is Parameter<int> intParam)
        {
            ApplyKeyUpdate(intParam, removeKey);
        }
        if (_selectedAbstractParam is Parameter<float> floatParam)
        {
            ApplyKeyUpdate(floatParam, removeKey);
        }
        if (_selectedAbstractParam is Parameter<Vector2> vector2Param)
        {
            ApplyKeyUpdate(vector2Param, removeKey);
        }
        else if (_selectedAbstractParam is Parameter<Vector3> vector3Param)
        {
            ApplyKeyUpdate(vector3Param, removeKey);
            if (_selectedAbstractParam.name == "position")
            {
                RenewContainer();
            }
        }
        if (_selectedAbstractParam is Parameter<Vector4> vector4Param)
        {
            ApplyKeyUpdate(vector4Param, removeKey);
        }
        if (_selectedAbstractParam is Parameter<quaternion> quaternionParam)
        {
            ApplyKeyUpdate(quaternionParam, removeKey);
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
        if (_animationTarget.position.keys != null)
        {
            foreach (var key in _animationTarget.position.keys)
            {
                CreateSplineControlPoint("knot", key.value, _spline);
            }
        }
    }

    public void ApplyKeyUpdate<T>(Parameter<T> parameter, bool removeKey = false)
    {
        if (removeKey)
        {
            int idx = parameter.keys.FindIndex(i => i.time == _animationManager.time);
            if (idx >= 0)
            {
                parameter.removeKeyAtIndex(idx);
            }
        }
        else
        {
            parameter.setKey();
        }
    
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
        
        _spline.Spline.Add(new BezierKnot(new float3(pos.x, pos.y, pos.z)));
        // Create a new GameObject
        GameObject splineControlPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);

        splineControlPoint.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

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







