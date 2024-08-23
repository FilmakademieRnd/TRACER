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

//! @file "AnimationManager.cs"
//! @brief Implementation of the TRACER Animation Manager, managing all animation.
//! @author Simon Spielmann
//! @version 0
//! @date 22.08.2022


using System;
using System.Collections.Generic;
using UnityEngine;

namespace tracer
{
    //!
    //! Class implementing the input manager, managing all user inupts and mapping.
    //!
    public class AnimationManager : Manager
    {
        private float m_time;
        public float time { get => m_time; }
        public event EventHandler<float> animationUpdate;
        public event EventHandler<IAnimationParameter> keyframeUpdate;
        public AnimationManager(Type moduleType, Core tracerCore) : base(moduleType, tracerCore)
        {
            m_time = 0;
        }
        public void timelineUpdated(float time)
        {
            m_time = time;
            animationUpdate?.Invoke(this, time);
        }
        public void keyframesUpdated(IAnimationParameter parameter)
        {
            keyframeUpdate?.Invoke(this, parameter);
        }
    }

}
