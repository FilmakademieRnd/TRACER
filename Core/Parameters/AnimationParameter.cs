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
        //! Update the key at given _animationManager.time by manipulated the sceneObject via Gizmo while a keyframe is selected
        //!
        //! @ param index The index of the key for which the value is to be changed.
        //!
        public void updateKey(int index);
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

        public void createKeyList(AbstractKey[] _keys);
        public void createKey(AbstractKey _key);
    }

    //!
    //! This is an extansion for the Parameter class containing animation functionality.
    //!
    public partial class Parameter<T> : BaseParameter<T>, IAnimationParameter
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

            int exactIndex = -1;
            int low = 0;
            int high = _keyList.Count - 1;

            while (low <= high)
            {
                int mid = low + ((high - low) >> 1);
                if (_keyList[mid].time == key.time)
                {
                    exactIndex = mid;
                    break;
                }
                if (_keyList[mid].time < key.time)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            // If the time already exists, overwrite the value. 
            if (exactIndex != -1)
            {
                if (_keyList[exactIndex] is Key<T> existingKey)
                    existingKey.value = key.value;
            }
            else
            {
                // If it's a new time, find the correct sorted insertion index
                int insertIndex = findNextKeyIndex(key.time);

                if (insertIndex == -1)
                    _keyList.Add(key);
                else
                    // Insert at the correct positions to keep the list sorted
                    _keyList.Insert(insertIndex, key);
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
            if (!_isAnimated)
                return;

            if (_keyList.Remove(key))
            {
                if (_keyList.Count == 0)
                {
                    // Unsubscribe immediately to prevent memory leaks
                    _animationManager.animationUpdate -= updateParameterValue;
                    _isAnimated = false;
                }

                // Reset tracking indices since the list structure changed
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
            if (!_isAnimated || index < 0 || index >= _keyList.Count)
                return;

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
        //! Update the key at given _animationManager.time by manipulated the sceneObject via Gizmo while a keyframe is selected
        //! 
        //! @ param index The index of the key for which the value is to be changed.
        //!
        public void updateKey(int index){
            if (index < 0 || index >= _keyList.Count)
                return;

            if (_keyList[index] is Key<T> key)
                key.value = _value;
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
            (key as Key<T>).value = value;
        }

        //!
        //! Sets the value of a key in the key list at the given index.
        //!
        //! @ param index The index of the key for which the value is to be changed.
        //! @ param value The new value for the given key.
        //!
        public void setKeyValue (int index, T value)
        {
            (_keyList[index] as Key<T>).value = value;
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
            if (key == null || !_keyList.Contains(key))
                return;

            // We remove the key, update its time, and let addKey handle the sorted re-insertion.
            _keyList.Remove(key);
            key.time = time;

            // Convert to the generic type safely via pattern matching
            if (key is Key<T> genericKey)
                addKey(genericKey);
        }

        //!
        //! Sets the time of a key in the key list at the given index.
        //! The internal keylist will be automatically reordered by time. 
        //!
        //! @ param index The index of the key for which the time is to be changed.
        //! @ param time The time the geven key shall be moved to.
        //!
        public void setKeyTime(int index, float time){
            if (index < 0 || index >= _keyList.Count)
            {
                Debug.LogWarning($"setKeyTime::index ({index}) of _keyList would be out of bounds.");
                return;
            }

            // Forward to the object-based method
            setKeyTime(_keyList[index], time);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InvokeKeyHasChanged()
        {
            keyHasChanged?.Invoke(this, EventArgs.Empty);
        }

        public void createKeyList(AbstractKey[] _keys){
            if(_keyList == null)
                _keyList = new();
            if (!_isAnimated)
                InitAnimation();

            foreach(AbstractKey k in _keys){
                _keyList.Add(k);
                //Debug.Log(k.time + ", "+k.getValueString());
            }
                
            InvokeHasChanged();
        }

        public void createKey(AbstractKey _key){
            if(_keyList == null)
                _keyList = new();
            if (!_isAnimated)
                InitAnimation();

            _keyList.Add(_key);    
            InvokeHasChanged();
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
            // OPTIMIZATION: Avoid casting _parent every frame. 
            // Ideally, cache 'SceneObject parentSceneObject' once in Start/Init instead of doing this cast!
            if (!(_parent is SceneObject parentSceneObject) || parentSceneObject._lock || !_isAnimated)
                return;

            int count = _keyList.Count;

            if (count > 1)
            {
                if (_prevIdx < count && _nextIdx < count && _keyList[_prevIdx].time <= time && time <= _keyList[_nextIdx].time)
                {
                    value = interpolateLinear(time);
                }
                else
                {
                    // Current time is NOT in between the two active keys
                    int i = findNextKeyIndex(time);

                    // Current time is bigger than all keys in list
                    if (i == -1)
                    {
                        _prevIdx = count - 1;
                        if (_keyList[_prevIdx] is Key<T> lastKey)
                            value = lastKey.value; // Still update animation to the last key's value
                    }
                    // Current time is smaller than all keys in list
                    else if (i == 0)
                    {
                        _nextIdx = 0;
                        if (_keyList[0] is Key<T> firstKey)
                            value = firstKey.value; // Still update animation to the first key's value
                    }
                    // Current time is somewhere between all keys in list
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
                _nextIdx = 0;
                _prevIdx = 0;

                // Still update animation to the single key's value
                if (count == 1 && _keyList[0] is Key<T> singleKey)
                    value = singleKey.value;
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
            return findNextKeyIndex(key.time);
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
            int low = 0;
            int high = _keyList.Count - 1;
            int result = -1;

            // Binary search algorithm: O(log N) runtime instead of O(N) linear scan
            while (low <= high)
            {
                int mid = low + ((high - low) >> 1);
                if (_keyList[mid].time > time)
                {
                    result = mid; // Potential candidate found, look further left
                    high = mid - 1;
                }
                else
                {
                    low = mid + 1; // Look in the right half
                }
            }
            return result;
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
            // Cache list lookups to avoid redundant array indexing
            var prevKey = _keyList[_prevIdx];
            var nextKey = _keyList[_nextIdx];

            float prevTime = prevKey.time;
            float nextTime = nextKey.time;

            if (!(prevKey is Key<T> prevGenericKey) || !(nextKey is Key<T> nextGenericKey))
                return default;

            T pv = prevGenericKey.value;
            T nv = nextGenericKey.value;

            if (nextTime == prevTime)
                return nv;

            float inBetween = (time - prevTime) / (nextTime - prevTime);

            switch (_type)
            {
                case ParameterType.FLOAT:
                    if (pv is float fPv && nv is float fNv)
                    {
                        float fResult = fPv * (1.0f - inBetween) + fNv * inBetween;
                        return (T)(object)fResult;
                    }
                    break;

                case ParameterType.VECTOR3:
                    if (pv is Vector3 vPv && nv is Vector3 vNv)
                    {
                        Vector3 vResult = vPv * (1.0f - inBetween) + vNv * inBetween;
                        return (T)(object)vResult;
                    }
                    break;

                case ParameterType.QUATERNION:
                    if (pv is Quaternion qPv && nv is Quaternion qNv)
                    {
                        Quaternion qResult = Quaternion.SlerpUnclamped(qPv, qNv, inBetween);
                        return (T)(object)qResult;
                    }
                    break;

                case ParameterType.COLOR:
                    if (pv is Color cPv && nv is Color cNv)
                    {
                        Color cResult = Color.LerpUnclamped(cPv, cNv, inBetween);
                        return (T)(object)cResult;
                    }
                    break;
            }

            return default;
        }
    }

}