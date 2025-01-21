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

//! @file "SceneObjectPath.cs"
//! @brief Implementation of the TRACER SceneObjectPath, connecting Unity and TRACER functionalty.
//! @author Thomas Krüger
//! @version 0
//! @date 21.01.2025

using System.Runtime.CompilerServices;
using UnityEngine;

namespace tracer
{
    //!
    //! Implementation of the TRACER SceneObjectPath, connecting Unity and TRACER functionalty 
    //! around 3D scene specific objects.
    //!
    [DisallowMultipleComponent]
    public class SceneObjectPath : SceneObject
    {
        //!
        //! Factory to create a new SceneObject and do it's initialisation.
        //! Use this function instead GameObject.AddComponen<>!
        //!
        //! @param gameObject The gameObject the new SceneObject will be attached to.
        //! @sceneID The scene ID for the new SceneObject.
        //!
        public static new SceneObjectPath Attach(GameObject gameObject, byte sceneID = 255)
        {
            SceneObjectPath obj = gameObject.AddComponent<SceneObjectPath>();
            obj.Init(sceneID);

            return obj;
        }
        
        // public Parameter<Vector3> pathPos;
        // public Parameter<Quaternion> pathRot;
        
        // public Parameter<bool> createPath;  //ListParameter?
        // public RPCParameter<int> animHostGen;



        // Start is called before the first frame update
        public override void Awake()
        {
            base.Awake();
            //SceneObjectID should be 255
            //(objet)id should be 1
            //animHostGen rpc parameter should be 0, its value 3

            pathPos = new Parameter<Vector3>(transform.localPosition, "pathPosition", this);
            position.hasChanged += updatePathPosition;
            pathRot = new Parameter<Quaternion>(transform.localRotation, "pathRotation", this);
            rotation.hasChanged += updatePathRotation;

            createPath = new Parameter<bool>(false, "createPath", this);
            createPath.hasChanged += updateCreatePath;

            animHostGen = new RPCParameter<int>(0, "animHostGen", this);
            animHostGen.hasChanged += triggerAnimHostGen;
        }

        //!
        //! Function called, when Unity emit it's OnDestroy event.
        //!
        public override void OnDestroy()
        {
            base.OnDestroy();
            pathPos.hasChanged -= updatePathPosition;
            pathRot.hasChanged -= updatePathRotation;
            createPath.hasChanged -= updateCreatePath;
            animHostGen.hasChanged -= triggerAnimHostGen;
        }

         //!
        //! Update path position value
        //! @param   sender     Object calling the update function
        //! @param   a          new position value
        //!
        private void updatePathPosition(object sender, Vector3 a){
            transform.localPosition = a;
            emitHasChanged((AbstractParameter)sender);
        }

        //!
        //! Update path rotation value
        //! @param   sender     Object calling the update function
        //! @param   a          new rotation value
        //!
        private void updatePathRotation(object sender, Quaternion a){
            transform.localRotation = a;
            emitHasChanged((AbstractParameter)sender);
        }

        //!
        //! Update its value
        //! @param   sender     Object calling the update function
        //! @param   b          does the object has a path?
        //!
        private void updateCreatePath(object sender, bool b)
        {
            //transform.localScale = a;
            //emitHasChanged((AbstractParameter)sender);
            Debug.Log("called updateCreatePath "+b);
        }

        private void triggerAnimHostGen(object sender, int i)
        {
            emitHasChanged((AbstractParameter)sender);
            Debug.Log("called triggerAnimHostGen "+i);
        }
        
    }
}
