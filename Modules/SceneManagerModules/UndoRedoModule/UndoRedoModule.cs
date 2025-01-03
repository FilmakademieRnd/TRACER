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

//! @file "UndoRedoModule.cs"
//! @brief implementation of the global undo redo functionality
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @author Justus Henne
//! @author Paulo Scatena
//! @version 0
//! @date 16.03.2022

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace tracer
{
    //!
    //! implementation of TRACER 2D UI scene creator module
    //!
    public class UndoRedoModule : SceneManagerModule
    {
        //!
        //! A list of last editings to parameters for undo/redo functionality
        //!
        List<AbstractParameter> _history;

        //!
        //! The current position in the undo/redo history
        //!
        private int _currentHistoryPos;

        //!
        //! the maximum amount size of the history
        //!
        public int _maxHistory;

        //!
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public UndoRedoModule(string name, Manager manager) : base(name, manager)
        {
            _history = new List<AbstractParameter>();
            _currentHistoryPos = -1;
            _maxHistory = 100;
        }

        protected override void Start(object sender, EventArgs e)
        {
            UpdateReceiverModule updateReceiverModule = core.getManager<NetworkManager>().getModule<UpdateReceiverModule>();
            if (updateReceiverModule != null)
                updateReceiverModule.receivedHistoryUpdate += addHistoryStep;
        }

        //!
        //! adds the current value of the parameter to the history
        //!
        public void addHistoryStep(object sender, AbstractParameter a)
        {
            if (_currentHistoryPos < _history.Count - 1)
                _history.RemoveRange(_currentHistoryPos + 1, (_history.Count - _currentHistoryPos - 1));
            switch (a.tracerType)
            {
                case AbstractParameter.ParameterType.BOOL:
                    _history.Add(new Parameter<bool>((Parameter<bool>)a));
                    break;
                case AbstractParameter.ParameterType.COLOR:
                    _history.Add(new Parameter<Color>((Parameter<Color>)a));
                    break;
                case AbstractParameter.ParameterType.FLOAT:
                    _history.Add(new Parameter<float>((Parameter<float>)a));
                    break;
                case AbstractParameter.ParameterType.INT:
                    _history.Add(new Parameter<int>((Parameter<int>)a));
                    break;
                case AbstractParameter.ParameterType.QUATERNION:
                    _history.Add(new Parameter<Quaternion>((Parameter<Quaternion>)a));
                    break;
                case AbstractParameter.ParameterType.STRING:
                    _history.Add(new Parameter<String>((Parameter<String>)a));
                    break;
                case AbstractParameter.ParameterType.VECTOR2:
                    _history.Add(new Parameter<Vector2>((Parameter<Vector2>)a));
                    break;
                case AbstractParameter.ParameterType.VECTOR3:
                    _history.Add(new Parameter<Vector3>((Parameter<Vector3>)a));
                    break;
                case AbstractParameter.ParameterType.VECTOR4:
                    _history.Add(new Parameter<Vector4>((Parameter<Vector4>)a));
                    break;
            }
            if (_history.Count <= _maxHistory)
                _currentHistoryPos++;
            else
                _history.RemoveAt(0);
        }

        //!
        //! undo the latest change to the parameter
        //!
        public void undoStep()
        {
            if (_currentHistoryPos >= 0)
            {
                AbstractParameter sourceParam = _history[_currentHistoryPos];
                AbstractParameter targetParam = sourceParam._parent.parameterList[sourceParam._id];
                bool success = false;
                for (int i = _currentHistoryPos - 1; i > -1; i--)
                    if (_history[i]._parent._id == sourceParam._parent._id
                        && _history[i]._id == sourceParam._id)
                    {
                        targetParam.copyValue(_history[i]);
                        success = true;
                        break;
                    }
                if (!success)
                    targetParam.reset();

                _currentHistoryPos--;
            }
        }

        //!
        //! redo the next change to the parameter
        //!
        public void redoStep()
        {
            if (_currentHistoryPos < (_history.Count - 1))
            {
                AbstractParameter sourceParam = _history[_currentHistoryPos + 1];
                AbstractParameter targetParam = sourceParam._parent.parameterList[sourceParam._id];
                targetParam.copyValue(sourceParam);
                _currentHistoryPos++;
            }
        }

        //!
        //! vanish history for a SceneObject
        //!
        public void vanishHistory(SceneObject s)
        {
            foreach (AbstractParameter p in _history)
                if (p._parent._id == s._id)
                {
                    _history.Remove(p);
                    _currentHistoryPos--;
                }

        }

        //!
        //! Reset all Parameters of all sceneObjects to their initial value
        //!
        public void resetScene(object sender, bool e)
        {
            foreach (SceneObject s in manager.getAllSceneObjects())
            {
                foreach (AbstractParameter p in s.parameterList)
                    p.reset();
                vanishHistory(s);
                core.getManager<NetworkManager>().getModule<UpdateSenderModule>().queueResetMessage(s);
            }
        }
    }
}