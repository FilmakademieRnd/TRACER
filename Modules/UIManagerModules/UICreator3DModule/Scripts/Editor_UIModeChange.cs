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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace tracer
{
    public class Editor_UIModeChange : MonoBehaviour
    {
        private Core m_vpet;

        //UICreator3DModule UI3DModule;

        public void Awake()
        {
            m_vpet = GameObject.Find("TRACER").GetComponent<Core>();
        }

        public void SetModeT()
        {
            UIManager uiMgr = m_vpet.getManager<UIManager>();
            UICreator3DModule UI3DModule = uiMgr.getModule<UICreator3DModule>();
            UI3DModule.SetModeT();
        }

        public void SetModeR()
        {
            UIManager uiMgr = m_vpet.getManager<UIManager>();
            UICreator3DModule UI3DModule = uiMgr.getModule<UICreator3DModule>();
            UI3DModule.SetModeR();
        }

        public void SetModeS()
        {
            UIManager uiMgr = m_vpet.getManager<UIManager>();
            UICreator3DModule UI3DModule = uiMgr.getModule<UICreator3DModule>();
            UI3DModule.SetModeS();
        }
    }
}