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

//! @file "key.cs"
//! @brief Implementation of the tracer parameter key
//! @author Simon Spielmann
//! @version 0
//! @date 22.08.2022


namespace tracer
{
    public abstract class AbstractKey
    {
        //!
        //! The key's time and tangent time.
        //!
        public float time, tangentTime1, tangentTime2;
        //!
        //! Enumeration for the different interpolation types
        //!
        public enum InterplolationTypes { STEP, LINEAR, BEZIER, BEZIERFREE }
        //!
        //! The key's type.
        //!
        public InterplolationTypes interpolation;

        public abstract string getValueString();
    }
    //!
    //! Parameter base class.
    //!
    public class Key<T> : AbstractKey
    {
        //!
        //! The key's value.
        //!
        public T value { get;  internal set; }
        //!
        //! The key's tangent value 1.
        //!
        public T tangentValue1 { get; internal set; }
        //!
        //! The key's tangent value 2.
        //!
        public T tangentValue2 { get; internal set; }

        //!
        //! The Key's default constructor
        //!
        public Key()
        {
            time = 0;
            value = default(T);
            interpolation = InterplolationTypes.LINEAR;
            tangentTime1 = 0;
            tangentTime2 = 0;
            tangentValue1 = default(T);
            tangentValue2 = default(T);
        }

        public override string getValueString()
        {
            return value.ToString();
        }

        //!
        //! The Key's constructor for generic types.
        //!
        public Key(float time, T value, float tangentTime1 = 0, T tangentValue1 = default(T), float tangentTime2 = 0, T tangentValue2 = default(T), InterplolationTypes interpolation = InterplolationTypes.LINEAR)
        {
            this.time = time;
            this.interpolation = interpolation;
            this.value = value;
            this.tangentTime1 = tangentTime1;
            this.tangentTime2 = tangentTime2;
            this.tangentValue1 = tangentValue1;
            this.tangentValue2 = tangentValue2;
        }

        //!
        //! The Key's constructor for type LINEAR.
        //!
        public Key(float time, T value)
        {
            this.time = time;
            this.value = value;
            this.interpolation = InterplolationTypes.LINEAR;
            this.tangentTime1 = 0;
            this.tangentTime2 = 0;
            this.tangentValue1 = default(T);
            this.tangentValue2 = default(T);
        }
    }
}