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

//! @file "key.cs"
//! @brief Implementation of the tracer parameter key
//! @author Simon Spielmann
//! @version 0
//! @date 22.08.2022


namespace tracer
{

    //!
    //! Parameter base class.
    //!
    public class Key<T>
    {
        //!
        //! The key's value and tangent value.
        //!
        public T value, tangentValue;
        //!
        //! The key's time and tangent time.
        //!
        public float time, tangentTime;
        //!
        //! Enumeration for the different interpolation types
        //!
        public enum KeyType { STEP, LINEAR, BEZIER }
        //!
        //! The key's type.
        //!
        public KeyType type;

        //!
        //! The Key's constructor for generic types.
        //!
        public Key(float time, T value, float tangentTime = 0, T tangentValue = default(T), KeyType type = KeyType.LINEAR)
        {
            this.time = time;
            this.type = type;
            this.value = value;
            this.tangentTime = tangentTime;
            this.tangentValue = tangentValue;
        }

        //!
        //! The Key's constructor for type LINEAR.
        //!
        public Key (float time, T value)
        {
            this.time = time;
            this.value = value;
            this.type = KeyType.LINEAR;
            this.tangentTime = 0;
            this.tangentValue = default(T);
        }
    }
}