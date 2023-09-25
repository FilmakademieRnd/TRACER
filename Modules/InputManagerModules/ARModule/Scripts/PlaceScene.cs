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

//! @file "ARModule.cs"
//! @brief implementation of VPET AR features
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 09.11.2021

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace tracer
{
    public class PlaceScene : MonoBehaviour
    {
        ARRaycastManager m_RaycastManager;

        static List<ARRaycastHit> m_Hits = new List<ARRaycastHit>();

        private GameObject m_scene;
        public GameObject scene
        {
            set { m_scene = value; }
        }

        // Start is called before the first frame update
        void Start()
        {
            m_RaycastManager = GetComponent<ARRaycastManager>();
        }

        // [REVIEW]
        //public void placeScene(InputAction.CallbackContext context)
        //{
        //    var touchPosition = context.ReadValue<Vector2>();
        //    if (m_RaycastManager.Raycast(touchPosition, m_Hits, TrackableType.PlaneWithinPolygon))
        //    {
        //        // Raycast hits are sorted by distance, so the first one
        //        // will be the closest hit.
        //        var hitPose = m_Hits[0].pose;
        //        if (m_scene)
        //        {
        //            m_scene.transform.position = hitPose.position;
        //            m_scene.transform.rotation = hitPose.rotation;
        //        }
        //    }
        //}
    }
}
