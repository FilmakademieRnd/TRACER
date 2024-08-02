
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

    private SplineContainer _spline;
    
    private int _selectorCurrentSelectedSnapSelectElement;
    
    private SnapSelect _selectorSnapSelect;
    
    private AbstractParameter _selectedAbstractParam;
    
    //TODO MOD WITH REAL TIME 
    private int _timeeee;

    //!
    //! Dictionary of spline existing in the World
    //!
    private Dictionary<GameObject, string> _sceneObjectsSplines = new Dictionary<GameObject, string>();
    //!
    //! Spline GO parent
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

    public MenuButton animCreatorButton()
    {
        return _animCreatorButton;
    }

    protected override void Start(object sender, EventArgs e)
    {
        base.Start(sender, e);
        _mUIManager = core.getManager<UIManager>();
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
            _mUIManager.removeButton(_addKeyButton);
            _selectorSnapSelect.parameterChanged -= ParamChange;
            _animCreatorButton = null;
        }

        if (sceneObjects.Count > 0)
        {
            _animCreatorButton = new MenuButton("", StartAnimGen, null, "animCreatorButton ");
            _animCreatorButton.setIcon("Images/animationCreator");
            _mUIManager.addButton(_animCreatorButton);
            _animationTarget = sceneObjects[0];
            _selectorSnapSelect = GameObject.Find("PRE_UI_AddSelector(Clone)").GetComponent<SnapSelect>();
            _animCreatorButton.isHighlighted = false;
            _selectorSnapSelect.parameterChanged += ParamChange;
        }
        
        
    }

    public void StartAnimGen()
    {
        _animCreatorButton.isHighlighted = true;
        String splineName = new string(_animationTarget.name + "Spline");
        if (!(_splineGameObject = FindChildByNameInDictionary(splineName)))
        {
            _splineGameObject = CreateNewSplineGo(splineName);
            _sceneObjectsSplines.Add(_splineGameObject, splineName);
            _spline = _splineGameObject.AddComponent<SplineContainer>();
        }
        else
        {
            _spline = _splineGameObject.GetComponent<SplineContainer>();
        }

        _addKeyButton = new MenuButton("", AddKey, null, "_addKeyButton ");
        _addKeyButton.setIcon("Images/key");
        _mUIManager.addButton(_addKeyButton);

    }

    public void AddKey()
    {
        _selectedAbstractParam = _animationTarget.parameterList[_selectorCurrentSelectedSnapSelectElement];
        
        AbstractParameter abstractParameter = _selectedAbstractParam;
        if (!abstractParameter.isAnimated)
        {
            abstractParameter = _selectedAbstractParam = _selectedAbstractParam.getAnimationParameter();
        }

        //TODO MOD key so it works for all param and also editable .
        //TODO add rotation for bezier
        if (_selectedAbstractParam.name == "position")
        {
            _pos = _animationTarget.position.value;
            _spline.Spline.Add(new BezierKnot(new float3(_pos.x, _pos.y, _pos.z)));
            CreateSplineControlPoint("knot", _pos, _spline.gameObject);
        }

        //TODO find way to make it for all types 
        setKeyBasedOnType(abstractParameter);
    }

    public SplineLine(string name, Manager manager) : base(name, manager)
    {
    }

    public void setKeyBasedOnType(AbstractParameter param)
    {
        
        switch (param.tracerType)
        {
            
            case AbstractParameter.ParameterType.BOOL:
                ((AnimationParameter<bool>)param).setKey();
                break;
            case AbstractParameter.ParameterType.INT:
                ((AnimationParameter<int>)param).setKey();
                break;
            case AbstractParameter.ParameterType.FLOAT:
                ((AnimationParameter<float>)param).setKey();
                break;
            case AbstractParameter.ParameterType.VECTOR2:
                ((AnimationParameter<Vector2>)param).setKey();
                break;
            case AbstractParameter.ParameterType.VECTOR3:
                ((AnimationParameter<Vector3>)param).setKey();
                break;
            case AbstractParameter.ParameterType.VECTOR4:
                ((AnimationParameter<Vector4>)param).setKey();
                break;
            case AbstractParameter.ParameterType.QUATERNION:
                ((AnimationParameter<Quaternion>)param).setKey();
                break;
            case AbstractParameter.ParameterType.COLOR:
                ((AnimationParameter<Color>)param).setKey();
                break;
            default:
                
                break;
            
        }
    }

    public GameObject CreateNewSplineGo(string childName)
    {
        // Create a new GameObject
        GameObject childObject = new GameObject(childName);

        // Set the parent of the new GameObject to the specified parent
        childObject.transform.SetParent(_splineHolder.transform);

        // Set the local position of the child GameObject relative to its parent
        childObject.transform.localPosition = new Vector3(0, 0, 0);

        return childObject;
    }


    public void CreateSplineControlPoint(string childName, Vector3 pos, GameObject parent)
    {
        // Create a new GameObject
        GameObject splineControlPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);

        splineControlPoint.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        // Set the parent of the new GameObject to the specified parent
        splineControlPoint.transform.SetParent(parent.transform);

        // Set the local position of the child GameObject relative to its parent
        splineControlPoint.transform.localPosition = pos;
    }

    public GameObject FindChildByNameInDictionary(string splineName)
    {
        foreach (var kvp in _sceneObjectsSplines)
        {
            if (kvp.Value == splineName)
            {
                return kvp.Key;
            }
        }
        return null;
    }
    
    public void ParamChange(object sender, int manipulatorMode)
    {
        _selectorCurrentSelectedSnapSelectElement = manipulatorMode;
    }
}







