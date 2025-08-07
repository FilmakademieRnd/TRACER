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


//! @file "ModuleInterface.cs"
//! @brief The base implementation of the TRACER module interface.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 03.02.2022

using System;
using UnityEngine;

namespace tracer
{
    //!
    //! module interface definition
    //!
    interface ModuleInterface 
    {

    }

    //!
    //! module interface implementation
    //!
    public class Module : ModuleInterface, IDisposable
    {
        //!
        //! name of the module
        //!
        protected string m_name;

        //!
        //! manager of this module
        //! assigned in addModule function in Manager.
        //!
        protected Manager m_manager;

        //!
        //! Returns a reference to the TRACER _core.
        //!
        protected ref Core core { get => ref m_manager.core; }

        //!
        //! Flad determin whether a module is loaded or not.
        //!
        public bool load = true;

        //!
        //! constructor
        //! @param  name name of the module.
        //!
        public Module(string name, Manager manager)
        {
            m_name = name;
            m_manager = manager;

            m_manager.initEvent += Init;
            m_manager.startEvent += Start;
        }

        public virtual void Dispose()
        {
            m_manager.initEvent -= Init;
            m_manager.startEvent -= Start;
        }

        //!
        //! Get the name of the module.
        //! @return name of the module.
        //!
        public ref string name { get => ref m_name; }

        //! 
        //! Virtual function called when Unity initializes the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected virtual void Init(object sender, EventArgs e) { }
        //! 
        //! Virtual function called after the Init function.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected virtual void Start(object sender, EventArgs e) { }
    }
}
