
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
       _splineHolder.transform.position = new Vector3(0,0,0);
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
            _animCreatorButton = null;
        }

        if (sceneObjects.Count > 0)
        {
            _animCreatorButton = new MenuButton("", StartAnimGen, null, "animCreatorButton ");
            _animCreatorButton.setIcon("Images/animationCreator");
            _mUIManager.addButton(_animCreatorButton);
            _animationTarget = sceneObjects[0];
            _animCreatorButton.isHighlighted = false;
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
            _spline =_splineGameObject.GetComponent<SplineContainer>();
        }
        
        _addKeyButton = new MenuButton("", AddKey, null, "_addKeyButton ");
        _addKeyButton.setIcon("Images/key");
        _mUIManager.addButton(_addKeyButton);
        
    }

    public void AddKey()
    {
        if (!_animationTarget.position.isAnimated)
        {
            _animationTarget.position = (AnimationParameter<Vector3>)_animationTarget.position.getAnimationParameter();
        }

        //AnimationParameter<Vector3> paramAnim = (AnimationParameter<Vector3>)_animationTarget.position;


        _pos = _animationTarget.position.value;
        _spline.Spline.Add(new BezierKnot(new float3(_pos.x,_pos.y,_pos.z)));
 
        ((AnimationParameter<Vector3>)_animationTarget.position).setKey();
        
        //TODO find way to edit them in scene!!! 
        CreateSplineControlPoint("knot", _pos, _spline.gameObject);
    }

    public SplineLine(string name, Manager manager) : base(name, manager)
    {
    }
    
    public GameObject CreateNewSplineGo(string childName)
    {
        // Create a new GameObject
        GameObject childObject = new GameObject(childName);

        // Set the parent of the new GameObject to the specified parent
        childObject.transform.SetParent(_splineHolder.transform);

        // Set the local position of the child GameObject relative to its parent
        childObject.transform.localPosition = new Vector3(0,0,0);

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
}
    
    





