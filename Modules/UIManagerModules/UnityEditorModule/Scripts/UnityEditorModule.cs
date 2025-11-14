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

//! @file "PexelSelectorModule.cs"
//! @brief implementation of the TRACER UnityEditorModule, handling unity editor functionality.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 23.11.2021

using System;
using UnityEngine;
using UnityEditor;

namespace tracer
{
    //!
    //! Module to be used for connecting the Unity editor selection mechanism to TRACER.
    //!
    public class UnityEditorModule : UIManagerModule
    {
        //!
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public UnityEditorModule(string name, Manager manager) : base(name, manager)
        {
            load = false;
        }

        //! 
        //! Function called when Unity initializes the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Init(object sender, EventArgs e)
        {
            Selection.selectionChanged += SelectFunction;
        }

        //!
        //! Destructor, cleaning up event registrations. 
        //!
        ~UnityEditorModule()
        {
            Selection.selectionChanged -= SelectFunction;
        }

        //!
        //! Function to connect the Unity editor selection to the TRACER GameObject selection mechanism.
        //!
        private void SelectFunction()
        {
            GameObject gameObj = Selection.activeGameObject;

            if (gameObj != null)
            {
                SceneObject sceneObj = gameObj.GetComponent<SceneObject>();

                if (sceneObj != null)
                {
                    manager.clearSelectedObjects();

                    Helpers.Log("selecting: " + sceneObj.ToString());
                    manager.addSelectedObject(sceneObj);
                }
            }
            else
                manager.clearSelectedObjects();
        }
    }
}