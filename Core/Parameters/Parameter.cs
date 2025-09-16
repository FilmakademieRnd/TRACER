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
using System.Text;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using System.Runtime.InteropServices;

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
        public abstract int dataSize();
        public abstract int defaultDataSize();
        public abstract void OverrideParameterID(short objectId);
    }

    [Serializable]
    //!
    //! Parameter class defining the fundamental functionality and interface
    //!
    public partial class Parameter<T> : AbstractParameter, IAnimationParameter
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
        //! Getter for the size of the serialized sourceSpan of the parameter.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int dataSize()
        {
            if (_isAnimated)
                // parameterValue<ParamValueSize> + countKeys<short> + nbrKeys * (type<byte> + time<float> + tangentTime1<float> + tangentTime2<float> + value<ParamValueSize> + tangentvalue1<ParamValueSize> + tangentvalue2<ParamValueSize>) 
                return _dataSize + 2 + _keyList.Count * (1 + 3 * sizeof(float) + 3 * _dataSize);

            return defaultDataSize();
        }
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
        public override void OverrideParameterID(short paraID)
        {
            _id = paraID;
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
        public Parameter(T value, string name, ParameterObject parent = null, bool distribute = true, UIManager.Roles role = UIManager.Roles.VIEWER)
        {
            _value = value;
            _name = name;
            _parent = parent;
            _type = toTracerType(typeof(T));
            _distribute = distribute;
            _role = role;
            _initialValue = value;
            _nextIdx = 0;
            _prevIdx = 0;
            _keyList = new List<AbstractKey>();

            // initialize sourceSpan size
            switch (_type)
            {
                case ParameterType.NONE:
                case ParameterType.ACTION:
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
        public Parameter(Parameter<T> p)
        {
            _value = p._value;
            _name = p._name;
            _parent = p._parent;
            _id = p._id;
            _type = p._type;
            _dataSize = p._dataSize;
            _distribute = p._distribute;
            _initialValue = p._initialValue;
            _nextIdx = p._nextIdx;
            _prevIdx = p._prevIdx;
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
                _value = ((Parameter<T>)p).value;
                hasChanged?.Invoke(this, ((Parameter<T>)p).value);
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
            clearKeys();
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

                BitConverter.TryWriteBytes(targetSpan.Slice(offset, 2), keyCount); // nbr. of keys
                offset += 2;
                //Debug.Log("\tnr of key: "+keyCount);
                for (int i = 0; i < keyCount; i++)
                {
                    Key<T> key = (Key<T>)_keyList[i];
                    //Debug.Log("\tkey "+i+" Value: "+key.value+" at time "+key.time);
                    BitConverter.TryWriteBytes(targetSpan.Slice(offset, 1), (byte)key.interpolation); // interpolation
                    BitConverter.TryWriteBytes(targetSpan.Slice(offset += 1, 4), key.time); // time
                    BitConverter.TryWriteBytes(targetSpan.Slice(offset += 4, 4), key.tangentTime1); // tangent time 1
                    BitConverter.TryWriteBytes(targetSpan.Slice(offset += 4, 4), key.tangentTime2); // tangent time 2
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
        protected void SerializeData(Span<byte> targetSpan, in T value)
        {
            switch (_type)
            {
                case ParameterType.BOOL:
                    {
                        FromBool(value, targetSpan);
                        break;
                    }
                case ParameterType.INT:
                    {
                        FromInt(value, targetSpan);
                        break;
                    }
                case ParameterType.FLOAT:
                    {
                        FromFloat(value, targetSpan);
                        break;
                    }
                case ParameterType.VECTOR2:
                    {
                        FromVector2(value, targetSpan);
                        break;
                    }
                case ParameterType.VECTOR3:
                    {
                        FromVector3(value, targetSpan);
                        break;
                    }
                case ParameterType.VECTOR4:
                    {
                        FromVector4(value, targetSpan);
                        break;
                    }
                case ParameterType.QUATERNION:
                    {
                        FromQuaternion(value, targetSpan);
                        break;
                    }
                case ParameterType.COLOR:
                    {
                        FromColor(value, targetSpan);
                        break;
                    }
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
                    Key<T>.InterplolationTypes interplolation = MemoryMarshal.Read<Key<T>.InterplolationTypes>(sourceSpan.Slice(offset)); 
                    float time = MemoryMarshal.Read<float>(sourceSpan.Slice(offset += 1));
                    float tangenttime1 = MemoryMarshal.Read<float>(sourceSpan.Slice(offset += 4));
                    float tangenttime2 = MemoryMarshal.Read<float>(sourceSpan.Slice(offset += 4));
                    T value = deSerializeData(sourceSpan.Slice(offset += 4));
                    T tangentvalue1 = deSerializeData(sourceSpan.Slice(offset += _dataSize));
                    T tangentvalue2 = deSerializeData(sourceSpan.Slice(offset += _dataSize));
                    offset += _dataSize;

                    _keyList.Add(new Key<T>(time, value, tangenttime1, tangentvalue1, tangenttime2, tangentvalue2 , interplolation));
                }
                //_animationManager.keyframesUpdated(this);
                keyHasChanged?.Invoke(this, EventArgs.Empty);
            }

            _networkLock = true;
            hasChanged?.Invoke(this, _value);
            _networkLock = false;

            if (_type == ParameterType.ACTION) 
                ((Action)(object)_value)?.Invoke();
        }


        //!
        //! Function for deserializing parameter _data.
        //! 
        //! @param sourceSpan The byte data as span to be deserialized.
        //! @return The deserialized value as T.
        //! 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected T deSerializeData(ReadOnlySpan<byte> sourceSpan)
        {
            T returnvalue;
            switch (_type)
            {
                // case action: to avoid overwriting the action by a received value
                case ParameterType.ACTION:  
                    returnvalue = _value;
                    break;
                case ParameterType.BOOL:
                    returnvalue = ToBool(sourceSpan);
                    break;
                case ParameterType.INT:
                    returnvalue = ToInt(sourceSpan);
                    break;
                case ParameterType.FLOAT:
                    returnvalue = ToFloat(sourceSpan);
                    break;
                case ParameterType.VECTOR2:
                    returnvalue = ToVector2(sourceSpan);
                    break;
                case ParameterType.VECTOR3:
                    returnvalue = ToVector3(sourceSpan);
                    break;
                case ParameterType.VECTOR4:
                    returnvalue = ToVector4(sourceSpan);
                    break;
                case ParameterType.QUATERNION:
                    returnvalue = ToQuaternion(sourceSpan);
                    break;
                case ParameterType.COLOR:
                    returnvalue = ToColor(sourceSpan);
                    break;
                case ParameterType.STRING:
                    returnvalue = ToString(sourceSpan);
                    break;
                default:
                    returnvalue = default;
                    break;
            }

            return returnvalue;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InvokeHasChanged()
        {
            hasChanged?.Invoke(this, _value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void FromBool(in T value, Span<byte> target)
        {
            target[0] = Convert.ToByte(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void FromInt(in T value, Span<byte> target)
        {
            BitConverter.TryWriteBytes(target, Convert.ToInt32(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void FromFloat(in T value, Span<byte> target)
        {
            BitConverter.TryWriteBytes(target, Convert.ToSingle(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void FromVector2(in T value, Span<byte> target)
        {
            Vector2 obj = (Vector2)(object)value;

            BitConverter.TryWriteBytes(target.Slice(0, 4), obj.x);
            BitConverter.TryWriteBytes(target.Slice(4, 4), obj.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void FromVector3(in T value, Span<byte> target)
        {
            Vector3 obj = (Vector3)(object)value;

            BitConverter.TryWriteBytes(target.Slice(0, 4), obj.x);
            BitConverter.TryWriteBytes(target.Slice(4, 4), obj.y);
            BitConverter.TryWriteBytes(target.Slice(8, 4), obj.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void FromVector4(in T value, Span<byte> target)
        {
            Vector4 obj = (Vector4)(object)value;

            BitConverter.TryWriteBytes(target.Slice(0, 4), obj.x);
            BitConverter.TryWriteBytes(target.Slice(4, 4), obj.y);
            BitConverter.TryWriteBytes(target.Slice(8, 4), obj.z);
            BitConverter.TryWriteBytes(target.Slice(12, 4), obj.w);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void FromQuaternion(in T value, Span<byte> target)
        {
            Quaternion obj = (Quaternion)(object)value;

            BitConverter.TryWriteBytes(target.Slice(0, 4), obj.x);
            BitConverter.TryWriteBytes(target.Slice(4, 4), obj.y);
            BitConverter.TryWriteBytes(target.Slice(8, 4), obj.z);
            BitConverter.TryWriteBytes(target.Slice(12, 4), obj.w);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void FromColor(in T value, Span<byte> target)
        {
            Color obj = (Color)(object)value;

            BitConverter.TryWriteBytes(target.Slice(0, 4), obj.r);
            BitConverter.TryWriteBytes(target.Slice(4, 4), obj.g);
            BitConverter.TryWriteBytes(target.Slice(8, 4), obj.b);
            BitConverter.TryWriteBytes(target.Slice(12, 4), obj.a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void FromString(in T value, Span<byte> target)
        {
            string obj = (string)Convert.ChangeType(value, typeof(string));
            target = Encoding.UTF8.GetBytes(obj);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static T ToBool(ReadOnlySpan<byte> source)
        {
            return (T)(object)MemoryMarshal.Read<bool>(source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static T ToInt(ReadOnlySpan<byte> source)
        {
            return (T)(object)MemoryMarshal.Read<int>(source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static T ToFloat(ReadOnlySpan<byte> source)
        {
            return (T)(object)MemoryMarshal.Read<float>(source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static T ToVector2(ReadOnlySpan<byte> source)
        {
            return (T)(object)new Vector2(MemoryMarshal.Read<float>(source),
                               MemoryMarshal.Read<float>(source.Slice(4)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static T ToVector3(ReadOnlySpan<byte> source)
        {
            return (T)(object)new Vector3(MemoryMarshal.Read<float>(source),
                               MemoryMarshal.Read<float>(source.Slice(4)),
                               MemoryMarshal.Read<float>(source.Slice(8)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static T ToVector4(ReadOnlySpan<byte> source)
        {
            return (T)(object)new Vector4(MemoryMarshal.Read<float>(source),
                               MemoryMarshal.Read<float>(source.Slice(4)),
                               MemoryMarshal.Read<float>(source.Slice(8)),
                               MemoryMarshal.Read<float>(source.Slice(12)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static T ToQuaternion(ReadOnlySpan<byte> source)
        {
            return (T)(object)new Quaternion(MemoryMarshal.Read<float>(source),
                               MemoryMarshal.Read<float>(source.Slice(4)),
                               MemoryMarshal.Read<float>(source.Slice(8)),
                               MemoryMarshal.Read<float>(source.Slice(12)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static T ToColor(ReadOnlySpan<byte> source)
        {
            return (T)(object)new Color(MemoryMarshal.Read<float>(source),
                               MemoryMarshal.Read<float>(source.Slice(4)),
                               MemoryMarshal.Read<float>(source.Slice(8)),
                               MemoryMarshal.Read<float>(source.Slice(12)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static T ToString(ReadOnlySpan<byte> source)
        {
            return (T)(object)new string(Encoding.UTF8.GetString(source));
        }

    }

}