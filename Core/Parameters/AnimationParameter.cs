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

//! @file "parameter.cs"
//! @brief Implementation of the tracer parameter
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 01.02.2023

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace tracer
{
    //!
    //! This is the interface for the Parameter class animation extensions.
    //!
    public interface IAnimationParameter
    {
        //!
        //! A reference to the key list (for animation).
        //!
        public ref List<AbstractKey> getKeys();
        //!
        //! Create and insert a new key element to the parameters key list, 
        //! based on the current parameter value and Animation Manager time.
        //!
        public void setKey();
        //!
        //! Clear the parameters key list and disable the animation functionality.
        //!
        public void clearKeys();
        //!
        //! Revove a given key element from the parameters key list.
        //!
        //! @param index is the index of the key to be removed from the parameters key list.
        //!
        public void removeKeyAtIndex(int index);
        //!
        //! Sets the time of a given key.
        //! The internal keylist will be automatically reordered by time. 
        //!
        //! @ param key The key for which the time is to be changed.
        //! @ param time The time the geven key shall be moved to.
        //!
        public void setKeyTime(AbstractKey key, float time);
        //!
        //! Sets the time of a key in the key list at the given index.
        //! The internal keylist will be automatically reordered by time. 
        //!
        //! @ param index The index of the key for which the time is to be changed.
        //! @ param time The time the geven key shall be moved to.
        //!
        public void setKeyTime(int index, float time);
        //!
        //! Event emitted when a keyframe has changed.
        //!
        public event EventHandler keyHasChanged;
        public void InvokeKeyHasChanged();
    }

    //!
    //! This is an extansion for the Parameter class containing animation functionality.
    //!
    public partial class Parameter<T> : AbstractParameter, IAnimationParameter
    {
        //!
        //! Event emitted when a keyframe has changed.
        //!
        public event EventHandler keyHasChanged;
        //!
        //! The next and the previous active keyframe index (for animation).
        //!
        private int _nextIdx = 0, _prevIdx = 0;
        //!
        //! The list of keyframes (for animation).
        //!
        private List<AbstractKey> _keyList = null;
        //!
        //! A reference to the key list (for animation).
        //!
        public ref List<AbstractKey> getKeys() { return ref _keyList; }
        //!
        //! A reference to the Animation Manager.
        //!
        private AnimationManager _animationManager = null;
        //!
        //! Initializes the parameters animation functionality,
        //!
        public override void InitAnimation()
        {
            _animationManager = ParameterObject._core.getManager<AnimationManager>();
            _animationManager.animationUpdate += updateParameterValue;

            _isAnimated = true;
        }

        //!
        //! Insert a given key element to the parameters key list, at the corresponding index.
        //!
        //! @param key The key to be added to the parameters key list.
        //!
        public void addKey(Key<T> key)
        {
            if (!_isAnimated)
                InitAnimation();

            int i = findNextKeyIndex(key);
            if (i == -1)
            {
                i = _keyList.FindIndex(i => i.time == key.time);
                if (i > -1)
                    ((Key<T>)_keyList[i]).value = key.value;
                else
                {
                    _keyList.Add(key);
                }
            }
            else
            {
                _keyList.Insert(i, key);
            }

            InvokeHasChanged();
        }

        //!
        //! Revove a given key element from the parameters key list.
        //!
        //! @param key The key to be removed from the parameters key list.
        //!
        public void removeKey(Key<T> key)
        {
            if (_isAnimated)
            {
                _keyList.Remove(key);

                if (_keyList.Count == 0)
                {
                    _animationManager.animationUpdate -= updateParameterValue;
                    _isAnimated = false;
                }
                _prevIdx = 0;
                _nextIdx = 0;
                InvokeHasChanged();
            }
        }

        //!
        //! Revove a given key element from the parameters key list.
        //!
        //! @param index is the index of the key to be removed from the parameters key list.
        //!
        public void removeKeyAtIndex(int index)
        {
            if (_isAnimated)
            {
                _keyList.RemoveAt(index);

                if (_keyList.Count == 0)
                {
                    _animationManager.animationUpdate -= updateParameterValue;
                    _isAnimated = false;
                }
                _prevIdx = 0;
                _nextIdx = 0;
                InvokeHasChanged();
            }
        }

        //!
        //! Create and insert a new key element to the parameters key list, 
        //! based on the current parameter value and Animation Manager time.
        //!
        public void setKey()
        {
            if (!_isAnimated)
                InitAnimation();

            addKey(new Key<T>(_animationManager.time, _value));
        }

        //!
        //! Create and insert a new key element to the parameters key list, 
        //! based on the given value and at the given time.
        //!
        //! @param time The time at which the new key is to be added.
        //! @param value The the value for the new keyframe to be added.
        //!
        public void setKey(Key<T> key)
        {
            addKey(key);
        }

        //!
        //! Clear the parameters key list and disable the animation functionality.
        //!
        public void clearKeys()
        {
            if (_isAnimated)
            {
                _keyList.Clear();
                _animationManager.animationUpdate -= updateParameterValue;
                _isAnimated = false;
                _prevIdx = 0;
                _nextIdx = 0;
            }
        }

        //!
        //! Sets the value of a given key.
        //!
        //! @ param key The key for which the value is to be changed.
        //! @ param value The new value for the given key.
        //!
        public void setKeyValue(AbstractKey key, T value)
        {
            ((Key<T>)key).value = value;
        }

        //!
        //! Sets the value of a key in the key list at the given index.
        //!
        //! @ param index The index of the key for which the value is to be changed.
        //! @ param value The new value for the given key.
        //!
        public void setKeyValue (int index, T value)
        {
            ((Key<T>)_keyList[index]).value = value;
        }

        //!
        //! Sets the time of a given key.
        //! The internal keylist will be automatically reordered by time. 
        //!
        //! @ param key The key for which the time is to be changed.
        //! @ param time The time the geven key shall be moved to.
        //!
        public void setKeyTime(AbstractKey key, float time)
        {
            setKeyTime(_keyList.IndexOf(key), time);
        }

        //!
        //! Sets the time of a key in the key list at the given index.
        //! The internal keylist will be automatically reordered by time. 
        //!
        //! @ param index The index of the key for which the time is to be changed.
        //! @ param time The time the geven key shall be moved to.
        //!
        public void setKeyTime(int index, float time)
        {
            Key<T> key = (Key<T>)_keyList[index];
            int count = _keyList.Count;
            key.time = time;

            if (count < 2)
                return;

            if (index > 0 && index < count - 1)
            {
                if (time < _keyList[index - 1].time || time > _keyList[index + 1].time)
                {
                    _keyList.Remove(key);
                    addKey(key);
                }
            }
            else if (index == 0)
            {
                if (time > _keyList[index + 1].time)
                {
                    _keyList.Remove(key);
                    addKey(key);
                }
            }
            else if (index == count-1)
            {
                if (time < _keyList[index - 1].time)
                {
                    _keyList.Remove(key);
                    addKey(key);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InvokeKeyHasChanged()
        {
            keyHasChanged?.Invoke(this, EventArgs.Empty);
        }

        //!
        //! Calculate the parameters value based on the keylist and given time.
        //! TODO: add as option to all "still update animation ... "
        //!
        //! @param o A reference to the Animation Manager.
        //! @param time The given time used to calulate the parameters new value.
        //!
        private void updateParameterValue(object o, float time)
        {
            if (((SceneObject)_parent)._lock)
                return;

            if (_isAnimated)
            {
                if (_keyList.Count > 1)
                {
                    if (_keyList[_prevIdx].time <= time && time <= _keyList[_nextIdx].time)
                        value = interpolateLinear(time);
                    else
                    {
                        // current time is NOT in between the two active keys
                        int i = findNextKeyIndex(time);
                        // current time is bigger than all keys in list
                        if (i == -1)
                        {
                            _prevIdx = _keyList.Count - 1;
                            value = ((Key<T>)_keyList[_prevIdx]).value; //still update animation to the last key's value
                        }
                        // current time is smaller than all keys in list
                        else if (i == 0)
                        {
                            _nextIdx = 0;
                            value = ((Key<T>)_keyList[_nextIdx]).value; //still update animation to the first key's value
                        }
                        // current time is somewhere between all keys in list
                        else
                        {
                            _nextIdx = i;
                            _prevIdx = i - 1;
                            value = interpolateLinear(time);
                        }
                    }
                }
                else 
                {
                    _nextIdx = _prevIdx = 0;
                    if (_keyList.Count == 1)    //still update animation to the left key's value
                        value = ((Key<T>)_keyList[_prevIdx]).value;
                    
                }
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
            return _keyList.FindIndex(i => i.time >= time);
        }

        //!
        //! Function that linear interpolates the current parameter value based on a given
        //! time and the previous and next time indices.
        //!
        //! @parameter time The given time used to interpolateLinear the parameters value.
        //! @return The interpolated parameter value.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T interpolateLinear(float time)
        {
            float pt = _keyList[_prevIdx].time;
            float nt = _keyList[_nextIdx].time;
            T pv = ((Key<T>)_keyList[_prevIdx]).value;
            T nv = ((Key<T>)_keyList[_nextIdx]).value;

            if (nt == pt)
                return nv;

            float inBetween = (time - pt) / (nt - pt);

            switch (_type)
            {
                case ParameterType.FLOAT:
                    return (T)(object)((float)(object)pv * (1.0f - inBetween) + (float)(object)nv * inBetween);
                case ParameterType.VECTOR3:
                    return (T)(object)((Vector3)(object)pv * (1.0f - inBetween) + (Vector3)(object)nv * inBetween);
                case ParameterType.QUATERNION:
                    return (T)(object)Quaternion.Slerp((Quaternion)(object)pv, (Quaternion)(object)nv, inBetween);
                case ParameterType.COLOR:
                    return (T)(object)Color.Lerp((Color)(object)pv, (Color)(object)nv, inBetween);
                default:
                    return default(T);
            }
        }
    }

}