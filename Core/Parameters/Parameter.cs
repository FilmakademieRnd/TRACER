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
using System.Runtime.InteropServices;
using System.Text;
using tracer;
using UnityEngine;
using static tracer.AbstractParameter;
using static UnityEngine.GraphicsBuffer;

namespace tracer
{

    //!
    //! Parameter base class.
    //!
    [Serializable]
    public abstract class AbstractParameter
    {
        //!
        //! The name of the parameter.
        //!
        [SerializeField]
        protected string _name;
        //!
        //! List for mapping TRACER parameter types to C# types and visa versa.
        //!
        private static readonly List<Type> _paramTypes = new List<Type> { typeof(void),
                                                                          typeof(Action),
                                                                          typeof(bool),
                                                                          typeof(int),
                                                                          typeof(float),
                                                                          typeof(Vector2),
                                                                          typeof(Vector3),
                                                                          typeof(Vector4),
                                                                          typeof(Quaternion),
                                                                          typeof(Color),
                                                                          typeof(string),
                                                                          typeof(int)
        };
        //!
        //! Definition of Tracer's parameter types
        //!
        public enum ParameterType : byte { NONE, ACTION, BOOL, INT, FLOAT, VECTOR2, VECTOR3, VECTOR4, QUATERNION, COLOR, STRING, LIST, UNKNOWN = 100 }
        //!
        //! The parameters C# type.
        //!
        [SerializeField]
        protected ParameterType _type;
        //!
        //! A reference to the parameters _parent object.
        //!
        public ParameterObject _parent { get; internal set; }
        //!
        //! The unique _id of this parameter.
        //!
        public short _id { get; internal set; }
        //!
        //! Flag that determines whether a Parameter will be distributed.
        //!
        public bool _distribute { get; protected set; }
        //!
        //! Flag that determines whether a Parameter will be networl locked.
        //!
        public bool _networkLock { get; protected set; } = false;
        //!
        //! Flag that determines whether a Parameter will be a RPC parameter.
        //!
        public bool _isRPC { get; protected set; } = false;
        //!
        //! Flag that determines whether a Parameter is animated.
        //!
        public bool _isAnimated { get; protected set; } = false;
        //!
        //! Role of a Parameter, determines it's visibility in the UI.
        //!
        public UIManager.Roles _role { get; protected set; } = UIManager.Roles.VIEWER;
        //!
        //! Getter for parameters C# type.
        //!
        public Type cType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => toCType(_type);
        }
        //!
        //! Getter for parameters TRACER type.
        //!
        public ParameterType tracerType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _type;
        }
        //!
        //! Getter for parameters name.
        //!
        public ref string name
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _name;
        }
        //!
        //! Function to initialize animation funcctionality of a parameter.
        //!
        public abstract void InitAnimation();
        //!
        //! abstract reset function for the abstract parameter
        //!
        public abstract void reset();
        //!
        //! Fuction that determines a parameters C# type from a TRACER type.
        //!
        //! @param t The C# type from which the TRACER type is to be determined. 
        //! @return The determined C# type.
        //!
        public static Type toCType(ParameterType t)
        {
            return _paramTypes[(int)t];
        }
        //!
        //! Fuction that determines a parameters TRACER type from a C# type.
        //!
        //! @param t The TRACER type from which the C# type is to be determined. 
        //! @return The determined TRACER type.
        //!
        protected static ParameterType toTracerType(Type t)
        {
            int idx = _paramTypes.FindIndex(item => item.Equals(t));
            if (idx == -1)
                return (ParameterType)100;
            else
                return (ParameterType)idx;
        }
        //!
        //! Abstract definition of the function for serializing the parameters sourceSpan.
        //! 
        //! @param startoffset The offset in bytes within the generated array at which the sourceSpan should start at.
        //! 
        public abstract void Serialize(Span<byte> targetSpan);
        //!
        //! Abstract definition of the function for deserializing parameter sourceSpan.
        //! 
        //! @param sourceSpan The byte sourceSpan to be deserialized and copyed to the parameters value.
        //! 
        public abstract void deSerialize(ReadOnlySpan<byte> data);

        //!
        //! Abstract definition of function called to copy value of other parameter
        //! @param v new value to be set. Value will be casted automatically
        //!
        public abstract void copyValue(AbstractParameter v);
        //!
        //! Abstract definition of function used to calculate a parameter's data size.
        //!
        //! @return The size of the data stored in a parameter in byte.
        //!
        public abstract int dataSize();
        //!
        //! Abstract definition of function used get the parameter's default data size.
        //!
        //! @return The default size of the data stored in a parameter in byte.
        //!
        public abstract int defaultDataSize();
    }

    [Serializable]
    //!
    //! Parameter class defining the fundamental functionality and interface
    //!
    public abstract class BaseParameter<T> : AbstractParameter
    {
        [SerializeField]
        //!
        //! The parameters value as a template.
        //!
        protected T _value;
        [SerializeField]
        //!
        //! The initial value of the parameter at constuction time.
        //!
        protected T _initialValue;
        //!
        //! The size of the serialized sourceSpan of the parameter.
        //!
        protected short _dataSize = -1;
        //!
        //! Getter for the size of the serialized sourceSpan of the parameter in byte.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int dataSize()
        {
            return defaultDataSize();
        }
        //!
        //! Getter for the default size of the serialized sourceSpan of the parameter in byte.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int defaultDataSize()
        {
            return _dataSize;
        }
        //!
        //! Event emitted when parameter changed.
        //!
        public event EventHandler<T> hasChanged;

        //!
        //! The paramters constructor, initializing members.
        //!
        //! @param value The value of the parameder as the defined type T.
        //! @param name The parameters name.
        //! @param name The parameters _parent ParameterObject.
        //! @param name Flag that determines whether a Parameter will be distributed.
        //!
        public BaseParameter(T value, string name, ParameterObject parent = null, bool distribute = true, UIManager.Roles role = UIManager.Roles.VIEWER)
        {
            _value = value;
            _name = name;
            _parent = parent;
            _type = toTracerType(typeof(T));
            _distribute = distribute;
            _role = role;
            _initialValue = value;

            // check _parent
            if (parent)
            {
                _id = (short)_parent.parameterList.Count;
                _parent.parameterList.Add(this);
            }
            else
            {
                _id = -1;
                _distribute = false;
            }
        }

        //!
        //! Copy Constructor
        //! @param p source parameter to copy values from
        //!
        public BaseParameter(BaseParameter<T> p)
        {
            _value = p._value;
            _name = p._name;
            _parent = p._parent;
            _id = p._id;
            _type = p._type;
            _dataSize = p._dataSize;
            _distribute = p._distribute;
            _initialValue = p._initialValue;
            _role = p._role;

            hasChanged = p.hasChanged;
        }

        //!
        //! Getter and setter for the parameters value. 
        //!
        public T value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _value;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { setValue(value); }
        }

        //!
        //! function called to change a parameters value.
        //! @param   v new value to be set
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void setValue(T v)
        {
            _value = v;
            hasChanged?.Invoke(this, v);
        }

        //!
        //! function called to copy value of other parameter
        //! might break if parameter types do not match 
        //! @param p parameter to copy value from
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void copyValue(AbstractParameter p)
        {
            try
            {
                _value = ((BaseParameter<T>)p).value;
                hasChanged?.Invoke(this, ((BaseParameter<T>)p).value);
            }
            catch
            {
                Debug.Log("Could not cast parameter while executing copyValue() for parameter " + this.name + " from " + this._parent.name);
            }
        }

        //!
        //! reset parameter to initial value
        //!
        public override void reset()
        {
            if (!EqualityComparer<T>.Default.Equals(_value, _initialValue))
            {
                _value = _initialValue;
                hasChanged?.Invoke(this, _value);
            }
        }

        //!
        //! Sets the role of a parameter.
        //!
        //! @param role The new role the parameter will be set to. 
        //!
        public void setRole(UIManager.Roles role)
        {
            _role = role;
        }

        /////////////////////////////////////////////////////////////
        /////////////////////// Serialisation ///////////////////////
        /////////////////////////////////////////////////////////////

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InvokeHasChanged()
        {
            hasChanged?.Invoke(this, _value);
        }
    }

    [Serializable]
    //!
    //! Parameter class defining the fundamental functionality and interface
    //!
    public partial class Parameter<T> : BaseParameter<T>, IAnimationParameter where T : struct
    {
        //!
        //! Getter for the size of the serialized sourceSpan of the parameter in byte.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int dataSize()
        {
            if (_isAnimated)
                // parameterValue<ParamValueSize> + countKeys<short> + nbrKeys * (type<byte> + time<float> + tangentTime1<float> + tangentTime2<float> + value<ParamValueSize> + tangentvalue1<ParamValueSize> + tangentvalue2<ParamValueSize>) 
                return _dataSize + 2 + _keyList.Count * (1 + 3 * sizeof(float) + 3 * _dataSize);

            return defaultDataSize();
        }
        //!
        //! Getter for the default size of the serialized sourceSpan of the parameter in byte.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int defaultDataSize()
        {
            return _dataSize;
        }

        //!
        //! The paramters constructor, initializing members.
        //!
        //! @param value The value of the parameder as the defined type T.
        //! @param name The parameters name.
        //! @param name The parameters _parent ParameterObject.
        //! @param name Flag that determines whether a Parameter will be distributed.
        //!
        public Parameter(T value, string name, ParameterObject parent = null, bool distribute = true, UIManager.Roles role = UIManager.Roles.VIEWER) : base(value, name, parent, distribute, role)
        {
            _nextIdx = 0;
            _prevIdx = 0;
            _keyList = new List<AbstractKey>();

            // initialize sourceSpan size
            switch (_type)
            {
                case ParameterType.NONE:
                    _dataSize = 0;
                    break;
                case ParameterType.BOOL:
                    _dataSize = 1;
                    break;
                case ParameterType.INT:
                case ParameterType.FLOAT:
                    _dataSize = 4;
                    break;
                case ParameterType.VECTOR2:
                    _dataSize = 8;
                    break;
                case ParameterType.VECTOR3:
                    _dataSize = 12;
                    break;
                case ParameterType.VECTOR4:
                case ParameterType.QUATERNION:
                case ParameterType.COLOR:
                    _dataSize = 16;
                    break;
                default:
                    _dataSize = -1;
                    break;
            }
        }

        //!
        //! Copy Constructor
        //! @param p source parameter to copy values from
        //!
        public Parameter(Parameter<T> p) : base(p)
        {
            _nextIdx = p._nextIdx;
            _prevIdx = p._prevIdx;
        }

        /////////////////////////////////////////////////////////////
        /////////////////////// Serialisation ///////////////////////
        /////////////////////////////////////////////////////////////

        //!
        //! Function for serializing the parameters value into the targetSpan.
        //! 
        //! @param startoffset The offset in bytes within the generated array at which the sourceSpan should start at.
        //! 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Serialize(Span<byte> targetSpan)
        {
            SerializeData(targetSpan, _value);

            if (_isAnimated)
            {
                int offset = _dataSize;
                short keyCount = (short)_keyList.Count;

                //Debug.Log("<color=yellow>SERIALIZE DATA</color>");

                MemoryMarshal.Write(targetSpan.Slice(offset, 2), ref keyCount); // nbr. of keys
                offset += 2;
                //Debug.Log("\tnr of key: "+keyCount);
                for (int i = 0; i < keyCount; i++)
                {
                    Key<T> key = (Key<T>)_keyList[i];
                    //Debug.Log("\tkey "+i+" Value: "+key.value+" at time "+key.time);
                    targetSpan[offset] = (byte) key.interpolation; // interpolation
                    MemoryMarshal.Write(targetSpan.Slice(offset += 1, 4), ref key.time); // time
                    MemoryMarshal.Write(targetSpan.Slice(offset += 4, 4), ref key.tangentTime1); // tangent time 1
                    MemoryMarshal.Write(targetSpan.Slice(offset += 4, 4), ref key.tangentTime2); // tangent time 2
                    SerializeData(targetSpan.Slice(offset += 4, _dataSize), key.value); // value
                    SerializeData(targetSpan.Slice(offset += _dataSize, _dataSize), key.tangentValue1); // tangent value 1
                    SerializeData(targetSpan.Slice(offset += _dataSize, _dataSize), key.tangentValue2); // tangent value 2
                    offset += _dataSize;
                }
            }
        }

        //!
        //! Function for serializing the parameters sourceSpan.
        //! 
        //! @param targetSpan The target span to write the serialized data to.
        //! @param value The value to be serialized.
        //! 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void SerializeData(Span<byte> targetSpan, T value)
        {
            MemoryMarshal.Write(targetSpan, ref value);
        }

        //!
        //! Function for deserializing parameter _data.
        //! 
        //! @param sourceSpan The byte data as span to be deserialized and copyed to the parameters value.
        //! 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void deSerialize(ReadOnlySpan<byte> sourceSpan)
        {
            _value = deSerializeData(sourceSpan);

            if (_isAnimated)
            {
                // determine the correct offset in the span
                int offset = _dataSize;
                short keyCount = MemoryMarshal.Read<short>(sourceSpan.Slice(offset, 2));
                offset += 2;

                _keyList.Clear();

                for (int i = 0; i < keyCount; i++)
                {
                    AbstractKey.InterplolationTypes interplolation = (AbstractKey.InterplolationTypes) MemoryMarshal.Read<byte> (sourceSpan.Slice(offset));
                    float time = MemoryMarshal.Read<float>(sourceSpan.Slice(offset += 1));
                    float tangenttime1 = MemoryMarshal.Read<float>(sourceSpan.Slice(offset += 4));
                    float tangenttime2 = MemoryMarshal.Read<float>(sourceSpan.Slice(offset += 4));
                    T value = deSerializeData(sourceSpan.Slice(offset += 4));
                    T tangentvalue1 = deSerializeData(sourceSpan.Slice(offset += _dataSize));
                    T tangentvalue2 = deSerializeData(sourceSpan.Slice(offset += _dataSize));
                    offset += _dataSize;

                    _keyList.Add(new Key<T>(time, value, tangenttime1, tangentvalue1, tangenttime2, tangentvalue2, interplolation));
                }
                //_animationManager.keyframesUpdated(this);
                keyHasChanged?.Invoke(this, EventArgs.Empty);
            }

            _networkLock = true;
            InvokeHasChanged();
            _networkLock = false;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected T deSerializeData(ReadOnlySpan<byte> sourceSpan)
        {
            return MemoryMarshal.Read<T>(sourceSpan);
        }
    }

    [Serializable]
    //!
    //! Parameter class defining the fundamental functionality and interface
    //!
    public partial class ClassParameter<T> : BaseParameter<T> where T : class
    {
        //!
        //! Getter for the size of the serialized sourceSpan of the parameter in byte.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int dataSize()
        {
            return defaultDataSize();
        }
        //!
        //! Getter for the default size of the serialized sourceSpan of the parameter in byte.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int defaultDataSize()
        {
            switch (_type)
            {
                case ParameterType.STRING:
                    return ((string)Convert.ChangeType(_value, typeof(string))).Length;
                default:
                    return _dataSize;
            }
        }

        //!
        //! The paramters constructor, initializing members.
        //!
        //! @param value The value of the parameder as the defined type T.
        //! @param name The parameters name.
        //! @param name The parameters _parent ParameterObject.
        //! @param name Flag that determines whether a Parameter will be distributed.
        //!
        public ClassParameter(T value, string name, ParameterObject parent = null, bool distribute = true, UIManager.Roles role = UIManager.Roles.VIEWER) : base(value, name, parent, distribute, role)
        {
            // initialize sourceSpan size
            switch (_type)
            {
                case ParameterType.NONE:
                case ParameterType.ACTION:
                    _dataSize = 0;
                    break;
                default:
                    _dataSize = -1;
                    break;
            }
        }

        //!
        //! Copy Constructor
        //! @param p source parameter to copy values from
        //!
        public ClassParameter(ClassParameter<T> p) : base(p) 
        {
        }

        //!
        //! Function to initialize animation funcctionality of a parameter.
        //!
        public override void InitAnimation() { /*Empty*/ }


        /////////////////////////////////////////////////////////////
        /////////////////////// Serialisation ///////////////////////
        /////////////////////////////////////////////////////////////

        //!
        //! Function for serializing the parameters value into the targetSpan.
        //! 
        //! @param startoffset The offset in bytes within the generated array at which the sourceSpan should start at.
        //! 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Serialize(Span<byte> targetSpan)
        {
            switch (_type)
            {
                case ParameterType.STRING:
                    {
                        FromString(value, targetSpan);
                        break;
                    }
                default:
                    break;
            }
        }

        //!
        //! Function for deserializing parameter _data.
        //! 
        //! @param sourceSpan The byte data as span to be deserialized and copyed to the parameters value.
        //! 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void deSerialize(ReadOnlySpan<byte> sourceSpan)
        {
            switch (_type)
            {
                // case action: to avoid overwriting the action by a received value
                case ParameterType.ACTION:
                    break;
                case ParameterType.STRING:
                    _value = ToString(sourceSpan);
                    break;
                default:
                    _value = default;
                    break;
            }

            _networkLock = true;
            InvokeHasChanged();
            _networkLock = false;

            if (_type == ParameterType.ACTION)
                ((Action)(object)_value)?.Invoke();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void FromString(in T value, Span<byte> target)
        {
            string obj = (string)Convert.ChangeType(value, typeof(string));
            target = Encoding.UTF8.GetBytes(obj);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static T ToString(ReadOnlySpan<byte> source)
        {
            if (Encoding.UTF8.GetString(source) is T resultStr)
                return resultStr;
            else
                return default;
        }

    }
}