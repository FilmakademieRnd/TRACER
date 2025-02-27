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

//! @file "VPETGizmo.cs"
//! @brief Implementation of the VPETGizmo class, providing functionalty for adding line based gozmo objects to a Unity scene.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 22.02.2022

using System.Collections.Generic;
using UnityEngine;

namespace tracer
{
    //!
    //! Implementation of the VPETGizmo class, providing functionalty for adding line based gozmo objects to a Unity scene.
    //!
    public class VPETGizmo 
    {
        //!
        //! The gizmos root game object.
        //!
        private GameObject m_root;
        public GameObject root { get => m_root; }
        //!
        //! A Unity prefab containing a line render object and it's custom update script (GizmoElementUpdate).
        //!
        private static GameObject m_GizmoElementPrefab;
        //!
        //! A List storing all added elements of the gizmo.
        //!
        private List<GameObject> m_GizmoElements;

        //!
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public VPETGizmo(string name, Transform parent = null)
        {
            m_GizmoElementPrefab = Resources.Load("Prefabs/GizmoElement") as GameObject;
            m_GizmoElements = new List<GameObject>();
            m_root = new GameObject(name);

            if (parent != null)
            {
                
                m_root.transform.position = parent.transform.position;
                m_root.transform.rotation = parent.transform.rotation; ;
                m_root.transform.localScale = parent.transform.localScale;
                
                m_root.transform.parent = parent;

                
            }
        }

        //!
        //! Function for cleaning up and disposing all created elements.
        //!
        public void dispose()
        {
            foreach (GameObject gizmoElement in m_GizmoElements)
                Object.DestroyImmediate(gizmoElement);
            Object.DestroyImmediate(m_root);
            m_GizmoElements.Clear();
        }

        //!
        //! Function for adding a new gizmo element to the gizmo.
        //!
        //! @param positions A Vector3 list containing the positions to be added as a new element.
        //! @param color The color for the new element.
        //! @param loop Flag determining if the element is a loop (default is false).
        //!
        public Transform addElement(ref Vector3[] positions, Color color, bool loop = false)
        {
            GameObject gizmoElement = GameObject.Instantiate(m_GizmoElementPrefab, m_root.transform);
            LineRenderer lineRenderer = gizmoElement.GetComponent<LineRenderer>();

            lineRenderer.positionCount = positions.Length;
            lineRenderer.loop = loop;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.SetPositions(positions);
            m_GizmoElements.Add(gizmoElement);

            return gizmoElement.transform;
        }

        //!
        //! Removes the given element from the gismos element list.
        //!
        public void removeElement(GameObject element)
        {
            m_GizmoElements.Remove(element);
            Object.Destroy(element);
        }

        //!
        //! Sets the color of all elements of the gizmo.
        //!
        public void setColor(object sender, Color color)
        {
            foreach (GameObject gameObject in m_GizmoElements)
            {
                LineRenderer lineRenderer = gameObject.GetComponent<LineRenderer>();
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
            }
        }

    }
}
