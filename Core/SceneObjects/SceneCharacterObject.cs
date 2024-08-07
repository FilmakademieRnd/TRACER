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
        //!
        //! Dictionary to store bone transforms by their IDs
        //!
        private Dictionary<int, Transform> boneMap;
        //!
        //! The array of bone transforms from the SkinnedMeshRenderer
        //!
        private Transform[] bones;
        public List<string> boneNamesOrder;

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
            // Initialize the dictionary to store bone transforms by their IDs.
            boneMap = new Dictionary<int, Transform>();
            
            //  If server setBones is called on awake if not setBones is called from SceneCreatorModule line 137
            if (_core.isServer)
            {
                setBones();
            }
            
        }
        
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
                Parameter<Quaternion> parameter = ((Parameter<Quaternion>)parameterList[boneAtPos.Key]);
                Quaternion valueAtPos = parameter.value;
                
                if (boneAtPos.Value.localRotation != valueAtPos)
                {
                    // If the local rotation has changed, update the parameter's value to match the bone transform.
                    parameter.setValue(boneAtPos.Value.localRotation);
                }
            }
        }
    }
}
