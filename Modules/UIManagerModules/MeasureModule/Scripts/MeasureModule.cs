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

//! @file "MeasureModule.cs"
//! @brief Implementation of the TRACER MeasureModule, ui interface for ingame measurement
//! @author Thomas "Kruegbert" Krüger
//! @version 0
//! @date 02.01.2025


using System;
using System.Collections.Generic;
using UnityEngine;

namespace tracer{
        
    //!
    //! UI to enable the ingame distance interface
    //!     BEWARE: this ui could be active AND the other ui when selected a scene object to edit it (even the Tineline could be active too!)
    //!     TODO and add/remove measurement points
    //!

    public class MeasureModule : UIManagerModule{

        //!
        //! Event linked to the UI command of de/activating this ui
        //!
        public event EventHandler<bool> measurementUIActiveEvent;    

        //!
        //! is the measure ui active or not
        //!
        private bool isActive = false;

        //!
        //! Constructor
        //! @param name Name of this module
        //! @param Manager reference for this module
        //!
        public MeasureModule(string name, Manager manager) : base(name, manager)
        {
            //load = false;
        }

        #region Public Functions
        public bool IsMeasureModuleActive(){ return isActive; }

        #endregion

        #region Setup
        //!
        //! Function when Unity is loaded, create the top most ui button
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //!
        protected override void Init(object sender, EventArgs e)
        {
            Debug.Log("<color=orange>Init MeasureModule</color>");
            MenuButton measureUIButton = new MenuButton("", ToggleMeasureUI, new List<UIManager.Roles>() { UIManager.Roles.SET });
            measureUIButton.setIcon("Images/button_measure_off");
            manager.addButton(measureUIButton);
        }

        //! 
        //! Function called before Unity destroys the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Cleanup(object sender, EventArgs e)
        {
            base.Cleanup(sender, e);
        }
        #endregion

        private void ToggleMeasureUI(){
            isActive = !isActive;
            measurementUIActiveEvent?.Invoke(this, isActive);
            Debug.Log("<color=orange>ToggleMeasureUI: "+isActive+"</color>");
        }

    }
}
