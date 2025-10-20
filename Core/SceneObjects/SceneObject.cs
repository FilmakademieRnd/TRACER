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

//! @file "SceneObject.cs"
//! @brief Implementation of the TRACER SceneObject, connecting Unity and TRACER functionalty.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 01.03.2022

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace tracer
{
    //!
    //! Implementation of the TRACER SceneObject, connecting Unity and TRACER functionalty 
    //! around 3D scene specific objects.
    //!
    [DisallowMultipleComponent]
    public class SceneObject : ParameterObject
    {
        //!
        //! The global _id counter for generating unique parameterObject IDs.
        //!
        private static short s_oid = 1;
        //!
        //! Is the sceneObject locked?
        //!
        public bool _lock {
            get => IsLocked();
            set => SetLocked(value);
        }
        //!
        //! Flag determine if a scene object is locked.
        //!
        private bool m_lock = false;
        //!
        //! Function that returns the lock status of the scene object.
        //!
        //! @return The lock status of the scene object.
        //!
        protected virtual bool IsLocked(){ return m_lock;}
        //!
        //! Function that sets the lock status of the scene object.
        //!
        //! @param l The new lock status of the scene object.
        //!
        protected virtual void SetLocked(bool l){ m_lock = l;}
        //!
        //! Previous lock state for highlighting the sceneObject.
        //!
        private bool m_highlightLock = false;
        //!
        //! Is any parameter value of this sceneObject currently modified by the timeline? (timeline playing or time of timeline modified per drag)
        //! used to deny an unlock if we deselect a currently animated object
        //!
        public bool playedByTimeline = false;
        //!
        //! Is the sceneObject reacting to physics
        //!
        private bool _physicsActive = false;
        //!
        //! Is the sceneObject reacting to physics
        //!
        public bool physicsActive
        {
            get => _physicsActive;
        }
        //!
        //! A reference to the scene objects gizmo.
        //!
        public GameObject _gizmo = null;
        //!
        //! A reference to the scene objects icon.
        //!
        public GameObject _icon = null;
        //!
        //! A reference to the TRACER UI manager.
        //!
        protected UIManager m_uiManager;
        //!
        //! Position of the SceneObject
        //!
        public Parameter<Vector3> position;
        //!
        //! Rotation of the SceneObject
        //!
        public Parameter<Quaternion> rotation;
        //!
        //! Scale of the SceneObject
        //!
        public Parameter<Vector3> scale;

        //!
        //! Factory to create a new SceneObject and do it's initialisation.
        //! Use this function instead GameObject.AddComponen<>!
        //!
        //! @param _parent The gameObject the new SceneObject will be attached to.
        //! @sceneID The scene ID for the new SceneObject. (255 is used for dynamic abstract parameter)
        //!
        public static new SceneObject Attach(GameObject parent, byte sceneID)
        {
            SceneObject obj = parent.AddComponent<SceneObject>();
            obj.Init(sceneID);

            return obj;
        }
        //!
        //! Initialisation
        //!
        public override void Awake()
        {
            //base.Awake();

            if (_core == null)
                _core = GameObject.FindObjectOfType<Core>();

            _sceneID = 255;

            _id = s_oid++;
            _parameterList = new List<AbstractParameter>();

            _core.addParameterObject(this);

            m_uiManager = _core.getManager<UIManager>();

            _physicsActive = false; 
            InitParameter();      
        }

        //!
        //! Function that initializes the parameters of a scene object.
        //!
        protected virtual void InitParameter(){
            position = new Parameter<Vector3>(transform.localPosition, "position", this);
            position.hasChanged += updatePosition;
            rotation = new Parameter<Quaternion>(transform.localRotation, "rotation", this);
            rotation.hasChanged += updateRotation;
            scale = new Parameter<Vector3>(transform.localScale, "scale", this);
            scale.hasChanged += updateScale;     
        }

        //!
        //! Function called, when Unity emit it's OnDestroy event.
        //!
        public virtual void OnDestroy()
        {
            position.hasChanged -= updatePosition;
            rotation.hasChanged -= updateRotation;
            scale.hasChanged -= updateScale;
        }

        //!
        //! Function to lock or unlock the SceneObject.
        //!
        public void lockObject(bool l)
        {
            SceneManager sceneManager = _core.getManager<SceneManager>();
            if (l)
                sceneManager.LockSceneObject(this);
            else
                sceneManager.UnlockSceneObject(this);
        }

        //!
        //! Function to set whether object is currently animated (modified) by a playing timeline (or moving time manually)
        //!
        public void setObjectPlayedByTimeline(bool b)
        {
            playedByTimeline = b;
        }

        //!
        //! Function that emits the scene objects hasChanged event. (Used for parameter updates)
        //!
        //! @param parameter The parameter that has changed. 
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void emitHasChanged (AbstractParameter parameter)
        {
            if (!_lock)
                base.emitHasChanged(parameter);
        }

        //!
        //! Update GameObject local position.
        //! @param   sender     Object calling the update function
        //! @param   a          new position value
        //!
        private void updatePosition(object sender, Vector3 a)
        {
            transform.localPosition = a;
            emitHasChanged((AbstractParameter)sender);
        }

        //!
        //! Update GameObject local rotation.
        //! @param   sender     Object calling the update function
        //! @param   a          new rotation value
        //!
        private void updateRotation(object sender, Quaternion a)
        {
            transform.localRotation = a;
            emitHasChanged((AbstractParameter)sender);
        }

        //!
        //! Update GameObject local scale.
        //! @param   sender     Object calling the update function
        //! @param   a          new scale value
        //!
        private void updateScale(object sender, Vector3 a)
        {
            transform.localScale = a;
            emitHasChanged((AbstractParameter)sender);
        }

        //!
        //! Update is called once per frame
        //!
        public virtual void Update()
        {
            if (_lock != m_highlightLock)
            {
                if (_lock)
                    m_uiManager.highlightSceneObject(this);
                else
                    m_uiManager.unhighlightSceneObject(this);
                m_highlightLock = _lock;
            }

//#if UNITY_EDITOR
            updateSceneObjectTransform();
//#endif
            EndOfUpdateFunction();
        }
        //!
        //! updates the scene objects transforms and informs all connected parameters about the change
        //!
        private void updateSceneObjectTransform()
        {
            if (transform.localPosition != position.value)
                position.value = transform.localPosition;
            if (transform.localRotation != rotation.value)
                rotation.value = transform.localRotation;
            if (transform.localScale != scale.value)
                scale.value = transform.localScale;
        }

        //override to use stuff from Update without overwriting Update
        protected virtual void EndOfUpdateFunction(){
            //not used in current implementation for PathGeneration (but was prior to it)
        }
    }
}
