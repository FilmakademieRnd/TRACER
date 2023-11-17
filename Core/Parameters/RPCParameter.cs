/*
TRACER FOUNDATION - 
Toolset for Realtime Animation, Collaboration & Extended Reality
tracer.research.animationsinstitut.de
https://github.com/FilmakademieRnd/TRACER

Copyright (c) 2023 Filmakademie Baden-Wuerttemberg, Animationsinstitut R&D Lab

TRACER is a development by Filmakademie Baden-Wuerttemberg, Animationsinstitut
R&D Labs in the scope of the EU funded project MAX-R (101070072) and funding on
the own behalf of Filmakademie Baden-Wuerttemberg.  Former EU projects Dreamspace
(610005) and SAUCE (780470) have inspired the TRACER development.

This program is distributed in the hope that it will be useful, but WITHOUT
ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
FOR A PARTICULAR PURPOSE. See the MIT License for more details.
You should have received a copy of the MIT License along with this program; 
if not go to https://opensource.org/licenses/MIT
*/

using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using tracer;

namespace tracer
{
    //!
    //! RPCParameter class defining the fundamental functionality and interface
    //!
    public class RPCParameter<T> : Parameter<T>
    {
        public RPCParameter(T parameterValue, string name, ParameterObject parent, bool distribute = true) : base(parameterValue, name, parent, distribute) { }

        //!
        //! Action that will be executed when the parameter is evaluated.
        //!
        private Action<T> m_action;

        //!
        //! Function to set the action to be executed.
        //! 
        //! @param action The action to be set.
        //!
        public void setCall(Action<T> action)
        {
            m_action = action;
        }

        //!
        //! Function for deserializing parameter _data.
        //! 
        //! @param _data The byte _data to be deserialized and copyed to the parameters value.
        //! 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void deSerialize(Span<byte> data, int offset)
        {
            base.deSerialize(data, offset);
            m_action.Invoke(_value);
        }

        //!
        //! Function to call the action associated with the Parameter. 
        //!
        public void Call()
        {
            InvokeHasChanged();
        }
    }

    //!
    //! RPCParameter class defining the fundamental functionality and interface
    //!
    public class RPCParameter : RPCParameter<object>
    {
        //! Simple constructor without RPC parameter.
        public RPCParameter(string name, ParameterObject parent, bool distribute = true) : base(parent, name, parent, distribute) { }

        //!
        //! Overrides the Parameters deserialization functionality, because we do not have a payload.
        //! 
        //! @param _data The byte _data to be deserialized and copyed to the parameters value. (not used)
        //! @param _offset The start offset in the given data array. (not used)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void deSerialize(Span<byte> data, int offset)
        {
            _networkLock = true;
            InvokeHasChanged();
            _networkLock = false;
        }
    }

}