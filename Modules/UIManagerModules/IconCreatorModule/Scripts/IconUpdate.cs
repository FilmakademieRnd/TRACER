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

//! @file "IconUpdate.cs"
//! @brief Implementation of the TRACER IconUpdate component, updating a icons properties.
//! @author Simon Spielmann
//! @version 0
//! @date 03.03.2022

using UnityEngine;

namespace tracer
{
    //!
    //! Implementation of the TRACER GizmoElementUpdate component, updating line based gizmo objects. 
    //!
    public class IconUpdate : MonoBehaviour
    {
        //!
        //! The calculated Depth between main camera and gizmo from last frame call.
        //!
        private Vector3 m_iconScale;

        //!
        //! A reference to the parent Scene Object.
        //!
        public SceneObject m_parentObject;
        
        //!
        //! Start is called before the first frame update
        //!
        void Start()
        {
            Core core = GameObject.Find("TRACER").GetComponent<Core>();
            m_iconScale = Vector3.one * core.getManager<UIManager>().settings.uiScale.value;
            transform.right = Camera.main.transform.right;
        }

        //!
        //! Update is called once per frame
        //!
        void Update()
        {
            Transform camera = Camera.main.transform;
            float depth = Vector3.Dot(camera.position - transform.position, camera.forward);

            transform.position = m_parentObject.transform.position;
            transform.rotation = camera.rotation;
            transform.localScale = m_iconScale * Mathf.Abs(depth * 0.1f);
        }
    }
}
