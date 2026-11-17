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

//! @file "GenericMenuClickListener.cs"
//! @brief Implementation of the TRACER possibility to add a generic ClickListener for arbitrary event
//! @author Thomas Krüger
//! @version 0
//! @date 12.11.2026

using System;
//using tracer;
using UnityEngine;
using UnityEngine.EventSystems;

//!
//! Implementation of the TRACER possibility to add a generic ClickListener for arbitrary event
//!
public class GenericMenuClickListener : MonoBehaviour, IPointerClickHandler{
    private Action action; //ParameterType.Action is the only useful one?!

    public void InitListener(Action _action) {
        action = _action;
        if(action == null)
            Destroy(this);
    }


    //!
    //! Function called when clicked
    //!
    public void OnPointerClick(PointerEventData pointerEventData){
        //is within our bounds?
        action.Invoke();
    }

}
