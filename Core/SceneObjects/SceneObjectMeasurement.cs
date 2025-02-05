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

//! @file "SceneObjectMeasurement.cs"
//! @brief implementation SceneObjectMeasurement as a specialisation of the Measurement Modules' objects
//! @author Thomas Krüger
//! @version 0
//! @date 05.02.2025

using System;
using UnityEngine;

namespace tracer
{
    //!
    //! Implementation of the TRACER spot light object as a specialisation of the Measurement Modules' objects. They will never send any data over the network, but should have
    //! the same movement/rotation/selection functionality
    //!
    public class SceneObjectMeasurement : SceneObject{
        
        //Specific EventHandler for the local measurement object, if position has changed, update stuff
        //we do not use the SceneObject's "hasChanged", because thats triggered only for objects sending into the network
        public event EventHandler posChanged;
        private Vector3 previousPosWas;

        //!
        //! Factory to create a new SceneObject and do it's initialisation.
        //! Use this function instead GameObject.AddComponen<>!
        //!
        //! @param gameObject The gameObject the new SceneObject will be attached to.
        //! @sceneID The scene ID for the new SceneObject. sceneID 0 is for local objects that do not send
        //!
        public static new SceneObjectMeasurement Attach(GameObject gameObject, byte sceneID = 0){
            SceneObjectMeasurement obj = gameObject.AddComponent<SceneObjectMeasurement>();
            obj.Init(sceneID);
            return obj;
        }

        // Start is called before the first frame update
        public override void Awake(){
            base.Awake();
            //dont send any data out
            position = null;
            rotation = null;
            scale = null;   
            //thats why its always locked
            _lock = true;
        }

        public override void Update(){
            if(transform.position != previousPosWas){
                previousPosWas = transform.position;
                posChanged.Invoke(this, null);
            }
        }

        //!
        //! Function called, when Unity emit it's OnDestroy event.
        //!
        public override void OnDestroy(){
            //since we dont send any data out, we dont need to cleanup
            //base.OnDestroy();
        }

        protected override bool IsLocked(){ return true;}   //always locked
        protected override void SetLocked(bool v){ }        //never change
    }
}
