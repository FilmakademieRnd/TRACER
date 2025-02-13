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

//! @file "SceneObjectUI.cs"
//! @brief Implementation of the TRACER SceneObjectUI, extending SceneObject functionality to only exist locally AND change parameter values on its reference sceneobject
//! @author Thomas Krüger
//! @version 0
//! @date 13.02.2025

using UnityEngine;

namespace tracer
{
    //!
    //! Implementation of the TRACER SceneObject, connecting Unity and TRACER functionalty 
    //! around 3D scene specific objects.
    //!
    [DisallowMultipleComponent]
    public class SceneObjectUI : SceneObject
    {
        //!
        //! the reference to a (network) scene object to pass for uis queries of this selection SceneObjectUI
        //!
        private SceneObject referencedSceneObject;
        //!
        //! if we are representing an animated parameter, this will be our indice we change on our changed functions
        //!
        private int animatedParameterKeyIndex = -1;
        protected override bool IsLocked(){ return referencedSceneObject._lock;}
        protected override void SetLocked(bool v){ referencedSceneObject._lock = v;}
        
        public static new SceneObjectUI Attach(GameObject parent, byte sceneID){
            SceneObjectUI obj = parent.AddComponent<SceneObjectUI>();
            obj.Init(sceneID);
            return obj;
        }
        //!
        //! Initialisation
        //!
        public override void Awake(){
            base.Awake();
            _core = GameObject.FindObjectOfType<Core>();
            m_uiManager = _core.getManager<UIManager>();
        }

        protected override void InitParameter(){
            //will be called on every ui module change to listening on that specific update/haschanged thing?
            //right now, only utilize T and R (position and rotation)
            position = new Parameter<Vector3>(transform.localPosition, "position", this);
            position.hasChanged += passPositionUpdatesToReference;
            rotation = new Parameter<Quaternion>(transform.localRotation, "rotation", this);
            rotation.hasChanged += passRotationUpdatesToReference;
        }

        protected override void emitHasChanged(AbstractParameter parameter){
            //dont do anything
        }

        private void passPositionUpdatesToReference(object sender, Vector3 a){
            if(!referencedSceneObject)
                return;
            if(animatedParameterKeyIndex < 0){
                //no animated parameter, change refs pos
                referencedSceneObject.transform.localPosition = a;
            }else{
                //change the animated parameters value at this keys index
                //not possible
                //referencedSceneObject.position.getKeys()[animatedParameterKeyIndex].value = a;
                Key<Vector3> key = (Key<Vector3>)referencedSceneObject.position.getKeys()[animatedParameterKeyIndex];
                key = new Key<Vector3>(key.time, a, key.tangentTime1, key.tangentValue1, key.tangentTime2, key.tangentValue2);
                
                referencedSceneObject.position.getKeys()[animatedParameterKeyIndex] = key;
            }
        }

        private void passRotationUpdatesToReference(object sender, Quaternion a){
            if(!referencedSceneObject)
                return;
            if(animatedParameterKeyIndex < 0){
                //no animated parameter, change refs rot
                referencedSceneObject.transform.localRotation = a;
            }else{
                //see above
            }
        }
        /********* 
         * AIM
         * enable us to create objects that have the same behaviour as SceneObjects (gizmo, ui, selection, ... )
         * but dont send any network data or locks for parameter on their own
         * and only change the specific parameter on its referencedSceneObject
         * without the need to completely rewrite the interaction behaviour
         * 
         * EXAMPLE
         * - measurement objects should be able to be moved anywhere (even without a reference scene object [use TRS only]) and be selected, but never send anything out
         * - SceneCharacterObjects PATH should be shown via certain 3d scene objects that can be adjusted in pos and rot. but these objects have to be created dynamically,
         *   be selectable, be able to change the parameter animated values of their reference
         *********/
    }
}
