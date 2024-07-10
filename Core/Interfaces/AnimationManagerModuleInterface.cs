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

//! @file "AnimationManagerModuleInterface.cs"
//! @brief Implementation of the animation manager module interface.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 22.08.2022

namespace tracer
{
    public class AnimationManagerModule : Module
    {
        //!
        //! constructor
        //! @param  name The name of the module.
        //!
        public AnimationManagerModule(string name, Manager manager) : base(name, manager) { }

        //!
        //! set/get the manager of this module.
        //!
        public AnimationManager manager
        {
            get => (AnimationManager)m_manager;
        }
    }
}