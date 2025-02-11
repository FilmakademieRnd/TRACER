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

//! @file "ManagerInterface.cs"
//! @brief base tracer manager interface
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 25.06.2021

using System.Collections.Generic;
using System;

namespace tracer
{
    //!
    //! manager class interface definition
    //!
    interface ManagerInterface 
    {

    }

    //!
    //! manager class implementation
    //!
    public class Manager : ManagerInterface
    {

        //!
        //! A reference to TRACER _core.
        //!
        private Core m_core;

        //!
        //! Returns a reference to the TRACER _core.
        //!
        public ref Core core { get => ref m_core; }

        //!
        //! Dictionary of loaded modules.
        //!
        private Dictionary<Type, Module> m_modules;

        //!
        //! The managers settings. 
        //!
        internal Settings _settings;

        //!
        //! Event invoked when an TRACER _core Awake() callback is triggered.
        //!
        public event EventHandler initEvent;

        //!
        //! Event invoked when an TRACER _core Start() callback is triggered.
        //!
        public event EventHandler startEvent;

        //!
        //! Event invoked when an TRACER _core OnDestroy() callback is triggered.
        //!
        public event EventHandler cleanupEvent;

        //!
        //! Constructor
        //! @param  moduleType The type of modules to be loaded by this manager.
        //! @param tracerCore A reference to the TRACER _core.
        //!
        public Manager(Type moduleType, Core tracerCore)
        {
            m_modules = new Dictionary<Type, Module>();
            m_core = tracerCore;
            Type[] modules = Helpers.GetAllTypes(AppDomain.CurrentDomain, moduleType);

            m_core.awakeEvent += Init;
            m_core.startEvent += Start;
            m_core.destroyEvent += Cleanup;

            foreach (Type t in modules)
            {
                Module module = (Module)Activator.CreateInstance(t, t.ToString(), this);
                if (module.load)
                    addModule(module, t);
                else {
                    module.Dispose();
                }
            }

            Type[] settingTypes = Helpers.GetAllTypes(AppDomain.CurrentDomain, typeof(Settings));
            Type[] managerTypes = GetType().GetNestedTypes();

            Type settingsType = Helpers.FindFirst<Type>(settingTypes, managerTypes);

            if (settingsType != null)
                _settings = (Settings)Activator.CreateInstance(settingsType);
        }

        //! 
        //! Virtual function called when Unity initializes the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected virtual void Init(object sender, EventArgs e) 
        {
            initEvent?.Invoke(this, e);
        }

        //! 
        //! Virtual function called when Unity calls it's Start function.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected virtual void Start(object sender, EventArgs e) 
        {
            startEvent?.Invoke(this, e);
        }

        //! 
        //! Virtual function called before Unity destroys the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected virtual void Cleanup(object sender, EventArgs e) 
        {
            cleanupEvent?.Invoke(this, e);
            m_core.awakeEvent -= Init;
            m_core.startEvent -= Start;
            m_core.destroyEvent -= Cleanup;
        }

        //!
        //! Function to add a module to the manager.
        //! @param  module  module to be added
        //! @return returns false if a module of same type already exists, true otherwise. 
        //!
        protected bool addModule(Module module, Type type)
        {
            if (m_modules.ContainsKey(type))
                return false;
            else
            {
                m_modules.Add(type, module);
                return true;
            }
        }

        //!
        //! Function that returns a module based on a given type <T>.
        //! @tparam T The type of module to be requested.
        //! @return requested module or null if no module of this type is registered.
        //!
        public T getModule<T>()
        {
            Module module;
            if (!m_modules.TryGetValue(typeof(T), out module))
                Helpers.Log(this.GetType().ToString() + " no module of type " + typeof(T).ToString() + " registered.", Helpers.logMsgType.WARNING);
            return (T)(object) module;
        }

        //!
        //! Removes a module from the manager.
        //! @return returns false if module does not exist, true otherwise.
        //!
        protected bool removeModule(Type type)
        {
            return m_modules.Remove(type);
        }


    }
}