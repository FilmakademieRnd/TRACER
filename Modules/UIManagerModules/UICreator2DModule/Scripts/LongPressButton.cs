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

//! @file "LongPressButton.cs"
//! @brief UI helper class enabling long press events on Buttons
//! @author Jonas Trottnow
//! @version 0
//! @date 24.03.2022

using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System;

namespace tracer
{
    public class LongPressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public int holdTime = 5;
        public GameObject warning;
        int countDown;
        private PointerEventData m_eventData;

        //!
        //! Event emitted when long press hold time elapsed
        //!
        public event EventHandler<bool> longPress;

        public void OnPointerDown(PointerEventData eventData)
        {
            m_eventData = eventData;
            countDown = holdTime;
            InvokeRepeating("ButtonHeld", 2, 1);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (warning)
                warning.SetActive(false);
            CancelInvoke("ButtonHeld");
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (warning)
                warning.SetActive(false);
            CancelInvoke("ButtonHeld");
        }

        void ButtonHeld()
        {
            if (warning)
            {
                m_eventData.eligibleForClick = false;
                warning.transform.GetChild(0).GetComponent<TMP_Text>().text = countDown.ToString();
                warning.SetActive(true);
            }
            if (countDown == 0)
            {
                if (warning)
                    warning.SetActive(false);
                CancelInvoke("ButtonHeld");
                longPress?.Invoke(this,true);
            }
            countDown--;
        }
    }
}