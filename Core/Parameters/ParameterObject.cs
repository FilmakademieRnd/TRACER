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

//! @file "ParameterObject.cs"
//! @brief Implementation of the TRACER ParameterObject, collecting parameters and providing parameter update functionalities.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 01.03.2022

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace tracer
{
    //!
    //! Implementation of the TRACER ParameterObject, collecting parameters and providing parameter update functionalities.
    //!
    [System.Serializable]
    public class ParameterObject : MonoBehaviour
    {
        //!
        //! The global _id counter for generating unique parameterObject IDs.
        //!
        private static short s_id = 1;
        //!
        //! The unique ID of this parameter object.
        //!
        public short _id { get; protected set; }
        //!
        //! The unique ID of this parameter object.
        //!
        public byte _sceneID { get; protected set; } = 255;
        //!
        //! The name of this parameter object.
        //!
        protected string _name = "";
        //!
        //! The name of this parameter object.
        //!
        public string objectName
        {
            get => _name;
            set => _name = value;
        }
        protected readonly HashSet<AbstractParameter> _dirtyParameters = new HashSet<AbstractParameter>();
        protected bool _isDirty = false;
        //!
        //! A reference to the tracer _core.
        //!
        static public Core _core { get; protected set; } = null;
        public class ChangedArgs : EventArgs
        {
            public HashSet<AbstractParameter> Parameters { get; internal set; }
        }
        protected readonly ChangedArgs _cachedArgs = new ChangedArgs();
        //!
        //! Event emitted when parameter changed.
        //!
        public event EventHandler<ChangedArgs> hasChanged;
        //!
        //! List storing all parameters of this SceneObject.
        //!
        internal List<AbstractParameter> _parameterList;
        //!
        //! Getter for parameter list
        //!
        public IReadOnlyList<AbstractParameter> parameterList => _parameterList;
        //!
        //! Function that emits the parameter objects hasChanged event. (Used for parameter updates)
        //!
        //! @param parameter The parameter that has changed. 
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void emitHasChanged(AbstractParameter parameter)
        {
            if (!parameter._distribute)
                return;

            if (_dirtyParameters.Add(parameter))
                _isDirty = true;
        }
        //!
        //! Function that searches and returns a parameter of this parameter object based on a given name.
        //!
        //! @param name The name of the parameter to be returned.
        //!
        public Parameter<T> getParameter<T>(string name) where T : struct
        {
            for (int i = 0; i < _parameterList.Count; i++)
            {
                var parameter = _parameterList[i];

                if (string.Equals(parameter.name, name, StringComparison.Ordinal))
                    return parameter as Parameter<T>;
            }

            return null;
        }
        //!
        //! Factory to create a new ParameterObject and do it's initialisation.
        //! Use this function instead GameObject.AddComponen<>!
        //!
        //! @param gameObject The gameObject the new ParameterObject will be attached to.
        //! @sceneID The scene ID for the new ParameterObject.
        //!
        public static ParameterObject Attach(GameObject gameObject, byte sceneID = 255)
        {
            ParameterObject obj = gameObject.AddComponent<ParameterObject>();
            obj.Init(sceneID);
            
            return obj;
        }
        //!
        //! Initialisation
        //!
        public void Init(byte sceneID)
        {
            _core.removeParameterObject(this);
            _sceneID = sceneID;
            _core.addParameterObject(this);
        }
        //!
        //! Initialisation
        //!
        public virtual void Awake()
        {
            if (_core == null)
                _core = GameObject.FindObjectOfType<Core>();

            _sceneID = 255;

            _id = s_id++;
            //Debug.Log("Create parameter object " + name + " ID:" + _id + " SID:" + _sceneID);
            _parameterList = new List<AbstractParameter>();
            _dirtyParameters.EnsureCapacity(_parameterList.Count);

            _core.addParameterObject(this);
        }
        public List<AbstractParameter> getParametersByRole(UIManager.Roles role)
        {
            return _parameterList.FindAll(p => p._role >= role);
        }

        //!
        //! Late update function, called after Unity's scene render is finished.
        //!
        private void LateUpdate()
        {
            if (!_isDirty) return;

            _cachedArgs.Parameters = _dirtyParameters;
            hasChanged?.Invoke(this, _cachedArgs); // Direktes Senden!
            _cachedArgs.Parameters = null;

            _dirtyParameters.Clear();
            _isDirty = false;
        }
    }
}
