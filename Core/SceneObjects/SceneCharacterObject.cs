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
//! @author Thomas 'Kruegbert' Krüger
//! @version 0
//! @date 02.08.2023

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.CompilerServices;

namespace tracer
{
    //!
    //! Implementation of the TRACER SceneCharacterObject 
    //!
    public class SceneCharacterObject : SceneObject
    {

        //FUNCTIONALITY REMOVED FOR CURRENT BUILD/BERLINALE
        #region PATH VALUES
        //! workaround for generating ScenePathObjects on the run, but they are not sending any message to the network, but change their respective value in here
        //! its their indice thats representing their position in the animatedparameter (list)
        
        //!
        //! path position parameter, this will always be AnimationParameter
        //! needs to be at paremterList index 3
        //!
        public Parameter<Vector3> pathPos;      
        //!
        //! path rotation parameter, this will always be AnimationParameter
        //! needs to be at paremterList index 4
        //!
        public Parameter<Quaternion> pathRot;
        //!
        //! RPC Call to AnimHost to trigger the generation of the path
        //! needs to be at paremterList index 5
        //!
        public RPCParameter<int> animHostGen;

        #endregion
        //!
        //! Dictionary to store bone transforms (position specific for performance in update) by their IDs
        //!
        private Dictionary<int, Transform> boneMapForPosition;
        //!
        //! Dictionary to store bone transforms (rotation specific for performance in update) by their IDs
        //!
        private Dictionary<int, Transform> boneMapForRotation;
        
        public List<string> boneNamesOrder;
        //!
        //! event to listen on from e.g. the PathGenerationModule to update the path lines and scene objects
        //!
        public EventHandler onPathPositionChanged;

        //!
        //! Factory to create a new SceneObject and do it's initialisation.
        //! Use this function instead GameObject.AddComponen<>!
        //!
        //! @param gameObject The gameObject the new SceneObject will be attached to.
        //! @sceneID The scene ID for the new SceneObject.
        //!
        public static new SceneCharacterObject Attach(GameObject gameObject, byte sceneID = 254){
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
            boneMapForPosition = new Dictionary<int, Transform>();
            boneMapForRotation = new Dictionary<int, Transform>();
            
            //  If server setBones is called on awake if not setBones is called from SceneCreatorModule line 137
            if (_core.isServer)
            {
                setBones();
                connectAndStart(null, null);
            }else
                _core.getManager<SceneManager>().sceneReady += connectAndStart;    

        }

        private void connectAndStart(object sender, EventArgs e){
            //do stuff that needs a ready scene prior to this
        }

        #region PATH FUNCTIONS
        public bool HasPath(){
            return pathPos._isAnimated;
            //return pathPos.getKeys() != null && pathPos.getKeys().Count > 1;
        }
        //check if any bone (just first one) has a valid animated parameter
        public bool HasPathAnimation(){ return boneMapForPosition.Count > 0 && parameterList[boneMapForPosition.ElementAt(0).Key]._isAnimated; }
        public Vector3[] GetPathPositions(){
            //generate it here instead of within an update of updatePathPositions, because it would trigger a generation more often!
            Vector3[] pathPositions = new Vector3[pathPos.getKeys().Count];
            int i = 0;
            foreach(var abstractKey in pathPos.getKeys()){
                pathPositions[i] = ((Key<Vector3>)abstractKey).value;
                i++;
            }
            return pathPositions;
        }
        private void CreatePathParameters(){
            pathPos = new Parameter<Vector3>(transform.localPosition, "pathPositions", this);
            pathPos.hasChanged += updatePathPositions;
            pathRot = new Parameter<Quaternion>(transform.localRotation, "pathRotations", this);
            pathRot.hasChanged += updatePathRotations;

            animHostGen = new RPCParameter<int>(0, "animHostGen", this);
            animHostGen.hasChanged += triggerAnimHostGen;
        }

        //!
        //! Emit the new path or change of any animated parameter path position object into the network
        //! @param   sender     Object calling this function
        //! @param   a          dummy value
        //!
        private void updatePathPositions(object sender, Vector3 a){
            onPathPositionChanged?.Invoke(null, null);
            emitHasChanged((AbstractParameter)sender);
        }
        //!
        //! Emit the new path or change of any animated parameter path rotation object into the network
        //! @param   sender     Object calling this function
        //! @param   a          dummy value
        //!
        private void updatePathRotations(object sender, Quaternion a){
            emitHasChanged((AbstractParameter)sender);
        }
        //!
        //! Emit the RPCParameter (for AnimHost to trigger the CharacterAnimation) into the network
        //! @param   sender     Object calling this function
        //! @param   a          dummy value
        //!
        private void triggerAnimHostGen(object sender, int i){
            emitHasChanged((AbstractParameter)sender);
            //.call?

            //Show fast path particle (TrailRenderer from start to end)
            //will be shown on every client!
        }

        public override void OnDestroy(){
            base.OnDestroy();
            pathPos.hasChanged -= updatePathPositions;
            pathRot.hasChanged -= updatePathRotations;
            animHostGen.hasChanged -= triggerAnimHostGen;
            _core.getManager<SceneManager>().sceneReady -= connectAndStart;
        }
        #endregion
        
        //!
        //!Setting up all the bone rotation parameters
        //!
        public void setBones()
        {
            // Get the array of bone transforms from the SkinnedMeshRenderer component.
            Transform[] bones = GetComponentInChildren<SkinnedMeshRenderer>().bones;

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
                    localBoneRotationParameter.hasChanged += updateBoneRotation;
                    
                    // Use the parameter's ID as the key to store the bone transform in the dictionary.
                    var id = localBoneRotationParameter._id;
                    boneMapForRotation.Add(id, boneTransform);
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
                    localBonePositionParameter.hasChanged += updateBonePosition;
                    
                    // Use the parameter's ID as the key to store the bone transform in the dictionary.
                    var id = localBonePositionParameter._id;
                    boneMapForPosition.Add(id, boneTransform);
                    //boneNamesOrder.Add(boneTransform.name);
                    //Debug.Log(_id+"-"+boneTransform.name);
                }
            }   
        }
       
       //!
       //! Callback method triggered when the bone transform's local rotation is updated.
       //! @param   sender     Object calling the update function
       //! @param   a          new rotation value
       //!
       private void updateBoneRotation(object sender, Quaternion a)
        {
            // Retrieve the ID of the parameter whose value has changed.
            int id = ((Parameter<Quaternion>)sender)._id;
            
            // Update the bone transform's local rotation based on the new value.
            boneMapForRotation[id].localRotation = a;
            
            // Emit a signal to notify that the parameter has changed (if necessary).
            emitHasChanged((AbstractParameter)sender);
        }
       
        private void updateBonePosition(object sender, Vector3 a)
        {
            // Retrieve the ID of the parameter whose value has changed.
            int id = ((Parameter<Vector3>)sender)._id;
            
            // Update the bone transform's local rotation based on the new value.
            boneMapForPosition[id].localPosition = a;
            
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
        private void UpdateBoneTransform(){

            //much better way to iterate
            /*foreach(KeyValuePair<int, Transform> bonePair in boneMapForRotation){
                Parameter<Quaternion> parameter = (Parameter<Quaternion>)parameterList[bonePair.Key];
                //Quaternion valueAtPos = ;
                
                if (boneAtPos.Value.localRotation != (Quaternion)parameter.value)
                {
                    // If the local rotation has changed, update the parameter's value to match the bone transform.
                    parameter.setValue(boneAtPos.Value.localRotation);
                    //Debug.Log("UpdateBoneRotation");
                }
            }*/

            KeyValuePair<int, Transform> boneAtPos;
            // Loop through each bone transform stored in the dictionary.
            for (int i = 0; i < boneMapForRotation.Count; i++)
            {
                boneAtPos = boneMapForRotation.ElementAt(i);
                Parameter<Quaternion> parameter = ((Parameter<Quaternion>)parameterList[boneAtPos.Key]);
                Quaternion valueAtPos = parameter.value;
                
                if (boneAtPos.Value.localRotation != valueAtPos)
                {
                    // If the local rotation has changed, update the parameter's value to match the bone transform.
                    parameter.setValue(boneAtPos.Value.localRotation);
                    //Debug.Log("UpdateBoneRotation");
                }
            }

            for (int i = 0; i < boneMapForPosition.Count; i++){
                boneAtPos = boneMapForPosition.ElementAt(i);
                Parameter<Vector3> parameter = ((Parameter<Vector3>)parameterList[boneAtPos.Key]);
                Vector3 valueAtPos = parameter.value;  
                
                if (boneAtPos.Value.localPosition != valueAtPos)
                {
                    // If the local position has changed, update the parameter's value to match the bone transform.
                    parameter.setValue(boneAtPos.Value.localPosition);
                    //Debug.Log("UpdateBonePosition");
                }
            }
        }
    }
}
