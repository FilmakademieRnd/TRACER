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

//! @file "tracer.cs"
//! @brief TRACER core implementation
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 23.02.2021

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace tracer
{
    //!
    //! Central class for TRACER initalization.
    //! Manages all Tracer Managers and their modules.
    //!
    public class CoreInterface : MonoBehaviour
    {
        //!
        //! List of all registered Tracer Managers.
        //!
        protected Dictionary<Type,Manager> m_managerList;

        //!
        //! Constructor
        //!
        public CoreInterface()
        {
            m_managerList = new Dictionary<Type, Manager>();
        }

        //!
        //! Returns the TRACER manager with the given type.
        //!
        //! @tparam T The type of manager to be requested.
        //! @return The requested manager or null if not registered. 
        //!
        public T getManager<T>()
        {
            Manager manager = null;

            if (!m_managerList.TryGetValue(typeof(T), out manager))
                Helpers.Log(this.GetType().ToString() + " no manager of type " + typeof(T).ToString() + " registered.", Helpers.logMsgType.WARNING);

            return (T)(object) manager;
        }

        internal List<Manager> getManagers()
        {
            return new List<Manager>(m_managerList.Values);
        }
    }
}