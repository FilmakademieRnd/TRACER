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

using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace tracer
{
    //!
    //! RPCParameter class defining the fundamental functionality and interface
    //!
    public class AnimationParameter<T> : Parameter<T>
    {
        //!
        //! The next and the previous active keyframe index (for animation).
        //!
        private int _nextIdx, _prevIdx;
        //!
        //! The list of keyframes (for animation).
        //!
        private List<Key<T>> _keyList = null;
        //!
        //! A reference to the key list (for animation).
        //!
        public ref List<Key<T>> keys { get => ref _keyList; }
        //!
        //! A reference to the Animation Manager.
        //!
        private AnimationManager _animationManager = null;
        public AnimationParameter(T parameterValue, string name, ParameterObject parent = null, bool distribute = true) : base(parameterValue, name, parent, distribute)
        {
            if (typeof(T) == typeof(string))
                throw new InvalidOperationException("Type not supported");

            Init();

            _nextIdx = 0;
            _prevIdx = 0;
        }

        //!
        //! Factory Constructor for AnimationParameter from Parameter<T>
        //! @param p source parameter to copy values from
        //!
        public AnimationParameter(Parameter<T> p) : base(p)
        {
            if (typeof(T) == typeof(string))
                throw new InvalidOperationException("Type not supported");

            Init();

            _nextIdx = 0;
            _prevIdx = 0;
        }

        //!
        //! Factory Constructor from AnimationParameter
        //! @return A new Parameter<T> based in the AnimationParameter.
        //!
        public AbstractParameter getParameter()
        {
            return new Parameter<T>(this);
        }

        //!
        //! Copy Constructor
        //! @param p source parameter to copy values from
        //!
        public AnimationParameter(AnimationParameter<T> p) : base(p)
        {
            _nextIdx = 0;
            _prevIdx = 0;
            _keyList = p._keyList;
            _animationManager = p._animationManager;

            if (_keyList != null && _animationManager != null)
            {
                _animationManager.animationUpdate += updateValue;
            }
        }


        //!
        //! Destructor
        //!
        ~AnimationParameter()
        {
            clearKeys();
        }

        //!
        //! Getter for the size of the serialized sourceSpan of the parameter.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int dataSize()
        {
            // count<short> + count *(type<byte> + time<float> + tangentTime<float> + value<ParamValueSize> + tangentvalue<ParamValueSize>) 
            return _dataSize + 2 + _keyList.Count * (1 + 2 * sizeof(float) + 2 * _dataSize);
        }

        /////////////////////////////////////////////////////////
        /////////////////////// Animation ///////////////////////
        /////////////////////////////////////////////////////////

        //!
        //! Initializes the parameters animation functionality,
        //!
        private void Init()
        {
            _keyList ??= new List<Key<T>>();

            if (_animationManager == null)
            {
                _animationManager = ParameterObject.core.getManager<AnimationManager>();
                _animationManager.animationUpdate += updateValue;
            }
        }

        //!
        //! Insert a given key element to the parameters key list, at the corresponding index.
        //!
        //! @param key The key to be added to the parameters key list.
        //!
        public void addKey(Key<T> key)
        {
            int i = findNextKeyIndex(key);
            if (i == -1)
            {
                i = _keyList.FindIndex(i => i.time == key.time);
                if (i > -1)
                    _keyList[i].value = key.value;
                else
                {
                    _keyList.Add(key);
                }
            }
            else
            {
                _keyList.Insert(i, key);
            }

            _networkLock = true;
            InvokeHasChanged();
            _networkLock = false;
        }

        //!
        //! Revove a given key element from the parameters key list.
        //!
        //! @param key The key to be removed from the parameters key list.
        //!
        public void removeKey(Key<T> key)
        {
            if (_keyList != null)
            {
                _keyList.Remove(key);
                if (_keyList.Count == 0)
                    _animationManager.animationUpdate -= updateValue;
            }
        }

        //!
        //! Create and insert a new key element to the parameters key list, 
        //! based on the current parameter value and Animation Manager time.
        //!
        public void setKey()
        {
            addKey(new Key<T>(_animationManager.time, value));
        }

        //!
        //! Clear the parameters key list and disable the animation functionality.
        //!
        private void clearKeys()
        {
            if (_animationManager != null)
                _animationManager.animationUpdate -= updateValue;

            if (_keyList != null)
                _keyList.Clear();
        }

        //!
        //! Calculate the parameters value based on the keylist and given time.
        //!
        //! @param o A reference to the Animation Manager.
        //! @param time The given time used to calulate the parameters new value.
        //!
        private void updateValue(object o, float time)
        {
            if (isAnimated)
            {
                // current time is NOT in between the two active keys
                if (time < _keyList[_prevIdx].time || time > _keyList[_nextIdx].time)
                {
                    int i = findNextKeyIndex(time);
                    // current time is bigger than all keys in list
                    if (i == -1)
                        _nextIdx = _prevIdx = _keyList.Count - 1;
                    else
                    {
                        // current time is smaller than all keys in list
                        if (i == 0)
                            _nextIdx = _prevIdx = 0;
                        // current time is somewhere between all keys in list
                        else
                        {
                            _nextIdx = i;
                            _prevIdx = i - 1;
                        }
                    }
                }
                value = interpolate(time);
            }
        }

        //!
        //! Function for searching the next bigger key index in the key list.
        //!
        //! @param key The key on which the index is to be searched.
        //! @return The next bigger index in the keylist.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int findNextKeyIndex(Key<T> key)
        {
            return _keyList.FindIndex(i => i.time > key.time);
        }

        //!
        //! Function for searching the next bigger key index in the key list.
        //!
        //! @param time The time on which the index is to be searched.
        //! @return The next bigger index in the keylist.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int findNextKeyIndex(float time)
        {
            return _keyList.FindIndex(i => i.time > time);
        }

        //!
        //! Function that returns the current animation state of the parameter.
        //!
        //! @return The current animation state of the parameter.
        //!
        public override bool isAnimated => true;

        //!
        //! Function that interpolates the current parameter value based on a given
        //! time and the previous and next time indices.
        //!
        //! @parameter time The given time used to interpolate the parameters value.
        //! @return The interpolated parameter value.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T interpolate(float time)
        {
            switch (_type)
            {
                case ParameterType.FLOAT:
                    float inBetween = (time - _keyList[_prevIdx].time) / (_keyList[_prevIdx].time - _keyList[_nextIdx].time);
                    float s1 = 1.0f - (_keyList[_nextIdx].time - inBetween) / (_keyList[_nextIdx].time - _keyList[_prevIdx].time);
                    return (T)(object)(((float)(object)_keyList[_prevIdx].value) * (1.0f - s1) + ((float)(object)_keyList[_nextIdx].value) * s1);
                default:
                    return default(T);
            }
        }

        //!
        //! Function for serializing the animation parameters keys.
        //! 
        //! @param _data The byte _data to be deserialized and copyed to the parameters value.
        //! @param _offset The start offset in the given sourceSpan array.
        //! 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Serialize(Span<byte> targetSpan)
        {
            base.Serialize(targetSpan);

            int offset = _dataSize;
            short keyCount = (short) _keyList.Count;
          
            BitConverter.TryWriteBytes(targetSpan.Slice(offset+=2, 2), keyCount); // nbr. of keys
            for (int i=0; i<keyCount; i++)
            {
                Key<T> key = _keyList[i];
                BitConverter.TryWriteBytes(targetSpan.Slice(offset+=1, 1),(byte)key.type); // type
                BitConverter.TryWriteBytes(targetSpan.Slice(offset+=4, 4), key.time); // time
                BitConverter.TryWriteBytes(targetSpan.Slice(offset+=4, 4), key.tangentTime); // tangent time
                SerializeData(targetSpan.Slice(offset+=_dataSize, _dataSize), key.value); // value
                SerializeData(targetSpan.Slice(offset, _dataSize), key.tangentValue); // tangent value
            }
        }

        //!
        //! Function for deserializing the animation parameters keys.
        //! 
        //! @param _data The byte _data to be deserialized and copyed to the parameters value.
        //! @param _offset The start offset in the given sourceSpan array.
        //! 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void deSerialize(ReadOnlySpan<byte> sourceSpan)
        {
            base.deSerialize(sourceSpan);

            // determine the correct offset in the span
            int offset = _dataSize;
            short keyCount = MemoryMarshal.Read<short>(sourceSpan.Slice(offset+=2, 2));

            _keyList.Clear();

            for (int i=0; i<keyCount; i++) 
            {
                Key<T>.KeyType type = MemoryMarshal.Read<Key<T>.KeyType>(sourceSpan.Slice(offset+=1));
                float time = MemoryMarshal.Read<float>(sourceSpan.Slice(offset+=4));
                float tangenttime = MemoryMarshal.Read<float>(sourceSpan.Slice(offset+=4));
                T value = deSerializeData(sourceSpan.Slice(offset+=_dataSize));
                T tangentvalue = deSerializeData(sourceSpan.Slice(offset));

                _keyList.Add(new Key<T>(time, value, tangenttime, tangentvalue, type));
            }
        }
    }
}