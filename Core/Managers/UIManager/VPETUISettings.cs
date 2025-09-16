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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace tracer
{
    [CreateAssetMenu(fileName = "DATA_VPET_UI", menuName = "TRACER/Create VPET UI settings file", order = 1)]
    public class VPETUISettings : ScriptableObject
    {
        public TMP_FontAsset defaultFont;
        public int defaultFontSize;
        public int smallFontSize;
        public Colors colors;
    }
    [System.Serializable]
    public class Colors
    {
        public Color FontColor;
        public Color FontRegular;
        public Color FontHighlighted;
        public Color MenuBG;
        public Color MenuTitleBG;
        public Color DropDown_TextfieldBG;
        public Color ButtonBG;
        public Color ElementSelection_Highlight;
        public Color ElementSelection_Default;
        public Color DefaultBG;
        public Color FloatingButtonBG;
        public Color FloatingButtonIcon;
    }
}
