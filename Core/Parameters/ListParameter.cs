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
using UnityEngine;

namespace tracer
{
    [Serializable]
    //!
    //! ListParameter class defining the fundamental functionality and interface
    //!
    public class ListParameter : Parameter<int>
    {
        //!
        //! The ListParamters constructor, initializing members.
        //!
        //! @param parameterList The list of parameders with the given type T.
        //! @param name The parameters name.
        //! @param name The parameters parent ParameterObject.
        //! @param name Flag that determines whether a Parameter will be distributed.
        //!
        public ListParameter(List<AbstractParameter> parameterList, string name, ParameterObject parent = null, bool distribute = true) : base(0, name, parent, distribute)
        {
            _parameterList = parameterList;
            _type = ParameterType.LIST;
        }

        //!
        //! Constructor initializing members.
        //!
        public ListParameter(string name, ParameterObject parent = null) : this(new List<AbstractParameter>(), name, parent)
        { }

        [SerializeField]
        //!
        //! The ListParameters parameter list.
        //!
        private List<AbstractParameter> _parameterList;

        //!
        //! Getter and setter for the parameter list.
        //!
        public List<AbstractParameter> parameterList
        {
            set => _parameterList = value;
            get => _parameterList;
        }

        //!
        //! The function called to change a parameter in the parameter list.
        //! @param idx The list index of the parameter to be replaced.
        //! @param p The new parameter.
        //!
        public void setParameter(int idx, AbstractParameter p)
        {
            if (idx < _parameterList.Count)
            {
                _parameterList[idx] = p;
            }
            else
                Helpers.Log("Parameter index for" + p.name + "exceeds length of list " + this.name, Helpers.logMsgType.WARNING);
        }

        //!
        //! The function for adding a parameter to the parameter list.
        //! @param p The parameter to be added to the parameter list.
        //!
        public void addParameter(AbstractParameter p)
        {
            if (!_parameterList.Contains(p))
            {
                _parameterList.Add(p);
            }
            else
                Helpers.Log("Parameter " + p.name + " already exists in list " + this.name, Helpers.logMsgType.WARNING);
        }

        //!
        //! The function for removing a parameter from the parameter list.
        //! @param p The parameter to be femoved from the parameter list.
        //!
        public void removeParameter(AbstractParameter p)
        {
            if (_parameterList.Contains(p))
            {
                _parameterList.Remove(p);
            }
            else
                Helpers.Log("Parameter " + p.name + " does not exists in list " + this.name, Helpers.logMsgType.WARNING);
        }

        //!
        //! Function for selecting the active index.
        //!
        //! @param idx The new active index to be selected.
        //!
        public void select(int idx)
        {
            setValue(idx);
        }
    }
}
