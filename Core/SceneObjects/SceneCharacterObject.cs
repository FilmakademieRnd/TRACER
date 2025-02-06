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

//! @file "SceneCharacterObject.cs"
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @author Alexandru-Sebastian Tufis-Schwartz
//! @version 0
//! @date 02.08.2023

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace tracer
{
    //!
    //! Implementation of the TRACER SceneCharacterObject 
    //!
    public class SceneCharacterObject : SceneObject
    {

        //FUNCTIONALITY REMOVED FOR CURRENT BUILD/BERLINALE
        #region PATH VALUES
        /*
        //!
        //! path position parameter, these will be AnimationParameter and <s>are hidden in ui by their name</s> (not hidden, because index will still iterate over it!)
        //! needs to be at paremterList index 3
        //!
        public Parameter<Vector3> pathPos;      
        //!
        //! path rotation parameter, these will be AnimationParameter and <s>are hidden in ui by their name</s> (not hidden, because index will still iterate over it!)
        //! needs to be at paremterList index 4
        //!
        public Parameter<Quaternion> pathRot;
        //!
        //! this will be used to generate a new ui icon we can select for further options
        //!
        public Parameter<bool> createPath;
        //!
        //! necessary rpcparameter to trigger AnimHost. Hidden by name (take care of index - UICreator2DModule.createManipulator()
        //!
        public Parameter<int> animHostPathAnimFinished;
        public RPCParameter<int> animHostGen;
        */
        #endregion

        //!
        //! Dictionary to store bone transforms by their IDs
        //!
        private Dictionary<int, Transform> boneMap;
        //!
        //! The array of bone transforms from the SkinnedMeshRenderer
        //!
        private Transform[] bones;
        public List<string> boneNamesOrder;

        private byte sceneIdWeHad;
        private short objectIdWeHad, paramterIdRPCParaHad;

        private bool resetHipAndRootPosRot = false;

        //!
        //! Factory to create a new SceneObject and do it's initialisation.
        //! Use this function instead GameObject.AddComponen<>!
        //!
        //! @param gameObject The gameObject the new SceneObject will be attached to.
        //! @sceneID The scene ID for the new SceneObject.
        //!
        public static new SceneCharacterObject Attach(GameObject gameObject, byte sceneID = 254)
        {
            SceneCharacterObject obj = gameObject.AddComponent<SceneCharacterObject>();
            obj.Init(sceneID);

            return obj;
        }

        //!
        //! Initialisation
        //!
        public override void Awake()
        {
            base.Awake();

            CreatePathParameters();

            // Initialize the dictionary to store bone transforms by their IDs.
            boneMap = new Dictionary<int, Transform>();
            
            //  If server setBones is called on awake if not setBones is called from SceneCreatorModule line 137
            if (_core.isServer)
            {
                setBones();
                connectAndStart(null, null);
            }else
                _core.getManager<SceneManager>().sceneReady += connectAndStart;    

        }

        private void connectAndStart(object sender, EventArgs e)
        {
            rootStartPos = transform.position;
            rootStartEulerAngles = transform.eulerAngles;

            hip = transform.FindDeepChild("hip");
            if(hip){
                hipStartLocalPos = hip.localPosition;
                hipParentStartLocalPos = hip.parent.localPosition;
                hipParentStartLocalRot = hip.parent.localRotation;
                hipStartLocalRot = hip.localRotation;
            }
        }

        #region PATH FUNCTIONS
        private void CreatePathParameters(){
            Parameter<Vector3> pathPos = new Parameter<Vector3>(transform.localPosition, "pathPosition", this);
            pathPos.hasChanged += updatePathPosition;
            Parameter<Quaternion> pathRot = new Parameter<Quaternion>(transform.localRotation, "pathRotation", this);
            pathRot.hasChanged += updatePathRotation;

            Parameter<bool> createPath = new Parameter<bool>(false, "createPath", this); //no hasChanged function necessary(?)
            createPath.hasChanged += updatePathData;

            
            Parameter<int> animHostPathAnimFinished = new Parameter<int>(0, "animHostPathReady", this);
            animHostPathAnimFinished.hasChanged += triggerAnimPathReady;

            RPCParameter<int> animHostGen = new RPCParameter<int>(0, "animHostGen", this);
            animHostGen.hasChanged += triggerAnimHostGen;
        }

        //see updatePosition
        private void updatePathPosition(object sender, Vector3 a){
            //necessary to emit? emit only on specific TriggerAnimHost function or "EmitPathFunction"?
            //overwrite this into the position animation? so it could be changed?
            //or let these handles to change stuff also be active and possible here?
            emitHasChanged((AbstractParameter)sender);
        }
        //see updateRotation
        private void updatePathRotation(object sender, Quaternion a){
            emitHasChanged((AbstractParameter)sender);
        }
        private void updatePathData(object sender, bool b){
            Debug.Log("Path Button clicked (most likely)");
            emitHasChanged((AbstractParameter)sender);
        }
        //!
        //! Emit the RPCParameter into the network
        //! @param   sender     Object calling this function
        //! @param   a          dummy value
        //!
        private void triggerAnimHostGen(object sender, int i){
            //emitHasChanged((AbstractParameter)sender);
            //.call?
            Debug.Log("called triggerAnimHostGen "+i);


            /*if(pathPos.getKeys() != null && pathPos.getKeys().Count > 0){
                //Show Particle with last pathPosition
                GameObject pathTargetParticle = Resources.Load("Particles_Path_Target", typeof(GameObject)) as GameObject;
                pathTargetParticle.transform.position = ((Key<Vector3>)pathPos.getKeys()[^1]).value;
                Destroy(pathTargetParticle, 8f);
            }*/
        }

        private void triggerAnimPathReady(object sender, int i){
            //emitHasChanged((AbstractParameter)sender);
            //.call?
            if(i == 5 && allowResetLocalPosAtEnd){
                allowResetLocalPosAtEnd = false;

                resetHipAndRootPosRot = true;
                Debug.Log("YAY. Path has finished");
                
                //reselect this object
                _core.getManager<UIManager>().clearSelectedObject();
                _core.getManager<UIManager>().addSelectedObject(this);
            }
        }

        public override void OnDestroy(){
            base.OnDestroy();
            //pathPos.hasChanged -= updatePathPosition;
            //pathRot.hasChanged -= updatePathRotation;
            //animHostGen.hasChanged -= triggerAnimHostGen;
             _core.getManager<SceneManager>().sceneReady -= connectAndStart;
        }

        public void SaveParametersBeforeOverwrite(){
            if(!onlyDoOnce){
                onlyDoOnce = true;
                sceneIdWeHad = _sceneID;
                objectIdWeHad = _id;
                paramterIdRPCParaHad = 7; //animHostGen._id;
            }
        }
        //!
        //! rn necessary to trigger the correct rpc parameter on animhost, which has constant values!
        //! @param   newSceneID     scene id where we want to emit the calls
        //! @param   newObjectID    id that animhost expects
        //! @param   newParameterID id that animhost expects
        //!
        public void OverrideTracerValues(byte newSceneID, short newObjectID, short newParameterID){
            _sceneID = newSceneID;
            _id = newObjectID;
            //animHostGen.OverrideParameterID(newParameterID);
        }
        //! see OverrideTracerValues, we reset them values
        public void ResetOverwrittenTracerValues(){
            _sceneID = sceneIdWeHad;
            _id = objectIdWeHad;
            //animHostGen.OverrideParameterID(paramterIdRPCParaHad);
        }
        public void TriggerAnimHost(){
            //animHostGen.value = 3;  //const value AnimHost expects
        }

        public void SendOutAnimHostTrigger(){
            allowResetLocalPosAtEnd = true;
            RPCParameter<int> sendToAnimHost = new RPCParameter<int>(1, "sendToAnimHost", this);
            _sceneID = 255;
            _id = 1;
            sendToAnimHost.OverrideParameterID(0);
            emitHasChanged((AbstractParameter)sendToAnimHost);
        }

        private bool allowResetLocalPosAtEnd = false;
        private bool onlyDoOnce = false;
        private Transform hip;
        private Vector3 hipParentStartLocalPos, hipStartLocalPos, rootStartPos, rootStartEulerAngles;
        private Quaternion hipParentStartLocalRot, hipStartLocalRot;

        [HideInInspector] public Quaternion calculatedButtonRot;

        protected override void EndOfUpdateFunction(){
            if(_lock)
                return;
            if(resetHipAndRootPosRot){
                resetHipAndRootPosRot = false;
                if(!hip){
                    Debug.LogWarning("no hip to unparent found.");
                    return;
                }

                rootStartEulerAngles = transform.eulerAngles;

                Vector3 newEndPos = hip.position + Vector3.down*hipParentStartLocalPos.y;
                newEndPos.y = rootStartPos.y;
                transform.position = newEndPos;
                //hip.parent.localPosition = hipParentStartLocalPos;
                
                hipStartLocalPos.y = hip.localPosition.y;
                hip.localPosition = hipStartLocalPos;

                transform.rotation = calculatedButtonRot;
                transform.Rotate(rootStartEulerAngles);

                rootStartEulerAngles = transform.eulerAngles;
                
                hip.parent.localRotation = hipParentStartLocalRot;
                hip.localRotation = hipStartLocalRot;

                //Debug.Log("EndOfUpdate Reset. lock: "+_lock);

                /*Vector3 newPos = hip.position;
                Quaternion newRot = hip.rotation;
                hip.parent.parent = null;
                transform.position = newPos;
                transform.rotation = newRot;
                hip.parent.parent = transform;*/
            }
        }
        #endregion
        
        //!
        //!Setting up all the bone rotation parameters
        //!
       public void setBones()
        {
            // Get the array of bone transforms from the SkinnedMeshRenderer component.
            bones = GetComponentInChildren<SkinnedMeshRenderer>().bones;

            boneNamesOrder = new List<string>();
            // Loop through each bone transform obtained from the SkinnedMeshRenderer.
            
            for (int i = 0; i < bones.Length; i++)
            {
                Transform boneTransform = bones[i];
                if (boneTransform != null)
                {
                    // Create a new Quaternion parameter for each bone transform's local rotation.
                    Parameter<Quaternion> localBoneRotationParameter =
                        new Parameter<Quaternion>(boneTransform.localRotation, boneTransform.name, this);
                    
                    // Attach a callback to the parameter's "hasChanged" event, which is triggered when the bone transform is updated.
                    localBoneRotationParameter.hasChanged += updateRotation;
                    
                    // Use the parameter's ID as the key to store the bone transform in the dictionary.
                    var id = localBoneRotationParameter._id;
                    boneMap.Add(id, boneTransform);
                    boneNamesOrder.Add(boneTransform.name);
                    //Debug.Log(_id+"-"+boneTransform.name);
                }
            }
            
            for (int i = 0; i < bones.Length; i++)
            {
                Transform boneTransform = bones[i];
                if (boneTransform != null)
                {
                    // Create a new Quaternion parameter for each bone transform's local rotation.
                    Parameter<Vector3> localBonePositionParameter =
                        new Parameter<Vector3>(boneTransform.localPosition, boneTransform.name, this);
                    
                    // Attach a callback to the parameter's "hasChanged" event, which is triggered when the bone transform is updated.
                    localBonePositionParameter.hasChanged += updatePosition;
                    
                    // Use the parameter's ID as the key to store the bone transform in the dictionary.
                    var id = localBonePositionParameter._id;
                    boneMap.Add(id, boneTransform);
                    //boneNamesOrder.Add(boneTransform.name);
                    //Debug.Log(_id+"-"+boneTransform.name);
                }
            }
            
            Parameter<int> pathSceneObjectRef = new Parameter<int>(0, "pathSceneObjectRef", this);
            pathSceneObjectRef.hasChanged += updateSceneObjectRef;
            pathSceneObjectRef.value = 1;   //hardcoded like ButtonManipulator.174    
        }
       
       //!
       //! Callback method triggered when the bone transform's local rotation is updated.
       //! @param   sender     Object calling the update function
       //! @param   a          new rotation value
       //!
       private void updateRotation(object sender, Quaternion a)
        {
            // Retrieve the ID of the parameter whose value has changed.
            int id = ((Parameter<Quaternion>)sender)._id;
            
            // Update the bone transform's local rotation based on the new value.
            boneMap[id].localRotation = a;
            
            // Emit a signal to notify that the parameter has changed (if necessary).
            emitHasChanged((AbstractParameter)sender);
        }
       
        private void updatePosition(object sender, Vector3 a)
        {
            // Retrieve the ID of the parameter whose value has changed.
            int id = ((Parameter<Vector3>)sender)._id;
            
            // Update the bone transform's local rotation based on the new value.
            boneMap[id].localPosition = a;
            
            // Emit a signal to notify that the parameter has changed (if necessary).
            emitHasChanged((AbstractParameter)sender);
        }
        
       //!
       //! Update is called once per frame
       //!
        public override void Update()
        {
            base.Update();
            UpdateBoneTransform();
        }

        //!
        //! updates the bones rotation and informs all connected parameters about the change
        //!
        private void UpdateBoneTransform()
        {
               // Loop through each bone transform stored in the dictionary.
            for (int i = 0; i < boneMap.Count; i++)
            {
                KeyValuePair<int, Transform> boneAtPos = boneMap.ElementAt(i);
                if (parameterList[boneAtPos.Key].tracerType == AbstractParameter.ParameterType.QUATERNION)
                {
                    Parameter<Quaternion> parameter = ((Parameter<Quaternion>)parameterList[boneAtPos.Key]);
                    Quaternion valueAtPos = parameter.value;
                    
                    if (boneAtPos.Value.localRotation != valueAtPos)
                    {
                        // If the local rotation has changed, update the parameter's value to match the bone transform.
                        parameter.setValue(boneAtPos.Value.localRotation);
                    }
                }
                else
                {
                    Parameter<Vector3> parameter = ((Parameter<Vector3>)parameterList[boneAtPos.Key]);
                    Vector3 valueAtPos = parameter.value;  
                    
                    if (boneAtPos.Value.localPosition != valueAtPos)
                    {
                        // If the local rotation has changed, update the parameter's value to match the bone transform.
                        parameter.setValue(boneAtPos.Value.localPosition);
                    }
                }
            }
        }

        private void updateSceneObjectRef(object sender, int a)
        {
            //pathSceneObjectRef.value = a;
            emitHasChanged((AbstractParameter)sender);
        }
    }
}
