/*
VPET - Virtual Production Editing Tools
tracer.research.animationsinstitut.de
https://github.com/FilmakademieRnd/VPET

Copyright (c) 2023 Filmakademie Baden-Wuerttemberg, Animationsinstitut R&D Lab

This project has been initiated in the scope of the EU funded project
Dreamspace (http://dreamspaceproject.eu/) under grant agreement no 610005 2014-2016.

Post Dreamspace the project has been further developed on behalf of the
research and development activities of Animationsinstitut.

In 2018 some features (Character Animation Interface and USD support) were
addressed in the scope of the EU funded project SAUCE (https://www.sauceproject.eu/)
under grant agreement no 780470, 2018-2022

This program is free software; you can redistribute it and/or modify it under
the terms of the MIT License as published by the Open Source Initiative.

This program is distributed in the hope that it will be useful, but WITHOUT
ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
FOR A PARTICULAR PURPOSE. See the MIT License for more details.

You should have received a copy of the MIT License along with
this program; if not go to
https://opensource.org/licenses/MIT
*/

//! @file "Manipulator.cs"
//! @brief base class of a manipulator for the 2D UI
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @author Justus Henne
//! @version 0
//! @date 02.02.2022

using System;
using UnityEngine;
//using UnityEngine.EventSystems;

namespace tracer
{
    public abstract class Manipulator : MonoBehaviour
    {
        //!
        //! Reference to the AbstractParameter to be edited by the manipulator
        //!
        protected AbstractParameter abstractParam;

        //!
        //! Event emitted when parameter has changed
        //!
        public event EventHandler<AbstractParameter> doneEditing;

        //!
        //! function to initalize the spinner or colorselect
        //!
        public virtual void Init(AbstractParameter para, UIManager m){

        }

        //!
        //! event invoking the doneEditing event whenever the user stops editing a parameter (e.g. finger lifted)
        //! @param sender source of the event
        //! @param e payload
        //!
        protected void InvokeDoneEditing(object sender, bool e){
            //Debug.Log("<color=yellow>Manipulator.InvokeDoneEditing</color>");
            doneEditing?.Invoke(this, abstractParam);
        }
    }
}