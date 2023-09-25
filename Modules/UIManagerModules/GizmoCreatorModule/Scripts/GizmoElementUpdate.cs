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

//! @file "GizmoElementUpdate.cs"
//! @brief Implementation of the TRACER GizmoElementUpdate component, updating line based gizmo objects.
//! @author Simon Spielmann
//! @version 0
//! @date 18.02.2022

using UnityEngine;

namespace tracer
{
    //!
    //! Implementation of the TRACER GizmoElementUpdate component, updating line based gizmo objects. 
    //!
    public class GizmoElementUpdate : MonoBehaviour
    {
        //!
        //! The default width parameter for the line renderer.
        //!
        private float m_lineWidth = 1.0f;
        //!
        //! The calculated Depth between main camera and gizmo from last frame call.
        //!
        private float m_oldDepth = 0.0f;
        //!
        //! The gizmos line renderer. 
        //!
        private LineRenderer m_lineRenderer;

        //!
        //! Start is called before the first frame update
        //!
        void Start()
        {
            m_lineRenderer = transform.gameObject.GetComponent<LineRenderer>();
            m_lineWidth = m_lineRenderer.startWidth;
        }

        //!
        //! Update is called once per frame
        //!
        void Update()
        {
            Transform camera = Camera.main.transform;
            float depth = Vector3.Dot(camera.position - transform.position, camera.forward);

            if (m_oldDepth != depth)
            {
                m_lineRenderer.startWidth = m_lineWidth * depth;
                m_lineRenderer.endWidth = m_lineWidth * depth;
                m_oldDepth = depth;
            }
        }
    }
}
