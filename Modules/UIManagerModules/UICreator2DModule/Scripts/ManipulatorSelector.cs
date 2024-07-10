/* 
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
*/

//! @file "ManipulatorSelector.cs"
//! @brief implementation of script attached to each button / selector to select a manipulator
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @author Justus Henne
//! @version 0
//! @date 02.02.2022

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace tracer
{
    public class ManipulatorSelector : MonoBehaviour
    {
        //associated button, linked in prefab
        public Button _selectionButton;

        //!
        //! Reference to TRACER UI settings
        //!
        private VPETUISettings _uiSettings;


        //! 
        //! function to initialize the Selector
        //! @param module reference to the UICreator2DModule
        //! @param icon sprite to be used by this button
        //! @param index index of the associated manipulator in UICreator2DModule
        //!
        public void Init(UICreator2DModule module, VPETUISettings uiSettings, Sprite icon, int index)
        {
            _selectionButton.onClick.AddListener(() => module.createManipulator(index));
            _selectionButton.image.sprite = icon;
            _uiSettings = uiSettings;
        }

        //!
        //! function to show button highlighted / selected
        //!
        public void visualizeActive()
        {
            _selectionButton.gameObject.GetComponent<Image>().color = _uiSettings.colors.ElementSelection_Highlight;
        }

        //!
        //! function to show button idle
        //!
        public void visualizeIdle()
        {
            _selectionButton.gameObject.GetComponent<Image>().color = Color.white;
        }
    }
}
