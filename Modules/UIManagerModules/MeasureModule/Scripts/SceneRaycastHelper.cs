using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class SceneRaycastHelper{
    public static bool DidHitSpecificUI(Vector2 point, params GameObject[] allowedToHit){
        List<RaycastResult> raycastHits = GetTappedUIElements(point);
        if(raycastHits.Count > 0){
            foreach(RaycastResult rayresult in raycastHits){
                //Debug.Log("\tui hit "+rayresult.gameObject.name);
                if(IsChildOf(rayresult.gameObject, allowedToHit)){
                    //Debug.Log("Hit Button to stop placing - so dont do any raycast positioning queries!");
                    return true;
                }
            }
        }
        return false;
    }

    public static bool RaycastIntoScene(GameObject sceneRoot, Vector2 point, out RaycastHit finalHit){
        //?? mixture of raycast and depth buffer check to place object in scene ??
        //check each renderer bounds extents if eligible

        //quick and dirty

        finalHit = new RaycastHit();
        
        //gather all objects in current views frustrum
        List<SphereCollider> tmpColliders;
        List<MeshRenderer> allVisibleMeshRenderer = GatherVisibleMeshRenderer(sceneRoot.GetComponentsInChildren<MeshRenderer>(), out tmpColliders);
        
        Debug.Log("<color=yellow>"+allVisibleMeshRenderer.Count+" VISIBLE OBJECTS FOUND</color>");

        Ray ray = Camera.main.ScreenPointToRay(point);

        //also use all collider when we are within (e.g. we are above a ground plane - the SphereCollider would be very big!)
        List<Transform> objectsToCheckList = new();
        foreach(SphereCollider sphere in tmpColliders){
            if(sphere.bounds.Contains(ray.origin))
                objectsToCheckList.Add(sphere.transform);
        }
        
        RaycastHit[] hits;
        int layerMaskNoUI = ~(1 << 5); //5 is UI and use the inverse
        hits = Physics.RaycastAll(ray, 100, layerMaskNoUI);
        foreach(RaycastHit hit in hits){
            objectsToCheckList.Add(hit.transform);
        }

        if(objectsToCheckList.Count == 0){
            Debug.Log("<color=red>NO HIT</color>");
            foreach(SphereCollider c in tmpColliders)
                Component.DestroyImmediate(c);
            return false;
        }

        //cast ray, check first X hits (must allow hit from within?)
        Debug.Log("<color=green>HIT "+objectsToCheckList.Count+" OBJECTS</color>");
    
        List<MeshCollider> tmpMeshColliders = new();
        foreach(Transform hitTransforms in objectsToCheckList){
            Debug.Log("<color=green>\t"+hitTransforms.gameObject.name+"</color>");
            if(!hitTransforms.GetComponent<MeshCollider>() && hitTransforms.GetComponent<MeshRenderer>()){
                tmpMeshColliders.Add(hitTransforms.gameObject.AddComponent<MeshCollider>());
            }
        }
        
        //remove sphere collider, add MeshCollider to these hit objects
        foreach(SphereCollider c in tmpColliders)
            Component.DestroyImmediate(c);

        //another raycast, place object on point
        if(Physics.Raycast(ray, out finalHit, 100, layerMaskNoUI)){
            //Debug.DrawLine(Camera.main.ScreenPointToRay(point).origin, finalHit.point, Color.red, 4f);
            //visualize hit at object we hit and with particle that is aligned like an arrow-target
            Debug.Log("<color=green>HIT AT"+finalHit.point+" on "+finalHit.transform.gameObject.name+"</color>");
            //remove sphere collider, add MeshCollider to these hit objects
            foreach(MeshCollider c in tmpMeshColliders)
                Component.DestroyImmediate(c);
            return true;
        }else{
            Debug.Log("<color=red>NO FINAL HIT</color>");
            //remove sphere collider, add MeshCollider to these hit objects
            foreach(MeshCollider c in tmpMeshColliders)
                Component.DestroyImmediate(c);
            return false;
        }
    }

    private static List<MeshRenderer> GatherVisibleMeshRenderer(MeshRenderer[] _mrs, out List<SphereCollider> _tmpColliders){
        Vector3 boundsSize;
        List<MeshRenderer> visibleMrs = new();
        _tmpColliders = new();
        foreach(MeshRenderer mr in _mrs){
            if(mr.isVisible){
                //TODO ignore gizmos and uis !?

                visibleMrs.Add(mr);
                //add sphere collider to objects encapuslating its bounds
                if(!mr.GetComponent<Collider>()){
                    _tmpColliders.Add(mr.gameObject.AddComponent<SphereCollider>());
                    //Debug.Log(mr.gameObject.name+" bounds are "+mr.bounds.size.ToString());
                    //tmpColliders[^1].radius = Mathf.Max(mr.bounds.size.x, mr.bounds.size.y, mr.bounds.size.z)/10f;   ///10 would be fitting, but missing edged of a cube
                    //tmpColliders[^1].radius = mr.bounds.size.magnitude/mr.transform.localScale.magnitude/2f;
                    boundsSize = mr.bounds.size;    //could also use meshfilter bounds size and multiply with scale
                    //not correct, but that sufficient (improved would be here https://stackoverflow.com/questions/57711849/meshrenderer-has-wrong-bounds-when-rotated)
                    //if rotated and scaled non uniform, it could mess up big times
                    Vector3 scale = mr.transform.localScale;
                    Vector3 correctedBoundsSize = boundsSize;
                    correctedBoundsSize.x *= scale.x;
                    correctedBoundsSize.y *= scale.y;
                    correctedBoundsSize.z *= scale.z;
                    if(correctedBoundsSize.x > correctedBoundsSize.y){
                        if(correctedBoundsSize.x > correctedBoundsSize.z)
                            _tmpColliders[^1].radius = boundsSize.x/scale.x/2f;
                        else
                            _tmpColliders[^1].radius = boundsSize.z/scale.z/2f;
                    }else{
                        if(correctedBoundsSize.y > correctedBoundsSize.z)
                            _tmpColliders[^1].radius = boundsSize.y/scale.y/2f;
                        else
                            _tmpColliders[^1].radius = boundsSize.z/scale.z/2f;
                    }
                    //editor visualize
                    //...
                }
            }
        }
        return visibleMrs;
    }

    //!
    //! returns the list of hit ui elements (it goes over all raycaster in the scene - ideally that would be GraphicRaycaster from the 2D UI)
    //!
    //! @param pos position to check
    //!
    private static List<RaycastResult> GetTappedUIElements(Vector2 _pos){
        PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
        eventDataCurrentPosition.position = _pos;
        List<RaycastResult> raycastHitResultList = new();
        EventSystem.current.RaycastAll(eventDataCurrentPosition, raycastHitResultList);

        return raycastHitResultList;
    }
    //!
    //! returns whether a gameobject is part (child) of others (RaycastAll returns the Image, not the Button)
    //!
    //! @param pos position to check
    //!
    private static bool IsChildOf(GameObject _toCheck, GameObject[] _parents){
        foreach(GameObject partOf in _parents){
            if(_toCheck.transform.IsChildOf(partOf.transform))
                return true;
        }
        return false;
    }

}
