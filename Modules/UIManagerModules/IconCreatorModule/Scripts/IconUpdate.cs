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

//! @file "IconUpdate.cs"
//! @brief Implementation of the TRACER IconUpdate component, updating a icons properties.
//! @author Simon Spielmann
//! @version 0
//! @date 03.03.2022

using UnityEngine;
using UnityEngine.UIElements;

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
        private Vector3 m_iconScale = Vector3.one;

        private SpriteRenderer m_renderer;

        //!
        //! A reference to the _parent Scene Object.
        //! TODO: make readonly from outside
        //!
        public SceneObject m_parentObject;

        //!
        //! The lock image for lights and camera, since we cannot show an outline on these
        //!
        public GameObject m_lockImage;

        private Transform ourTr;
        private Transform mainCamTr;
        
        //!
        //! Start is called before the first frame update
        //!
        public void Init(UIManager uiManager, SceneObject _parentObject){
            m_parentObject = _parentObject;

            uiManager.settings.uiScale.hasChanged += UpdateUIScale;
            m_iconScale = Vector3.one * uiManager.settings.uiScale.value;
            transform.right = Camera.main.transform.right;

            ourTr = transform;
            mainCamTr = Camera.main.transform;

            m_renderer = GetComponent<SpriteRenderer>();

            CreateLockIcon();
        }

        private void CreateLockIcon(){
            if(m_parentObject.GetComponent<Camera>() || m_parentObject.GetComponent<Light>()){
                m_lockImage = new GameObject("Lock Viz");
                SpriteRenderer sr = m_lockImage.AddComponent<SpriteRenderer>();
                sr.sprite = Resources.Load<Sprite>("Images/SceneObjectLocked");
                sr.material = GetComponent<SpriteRenderer>().material;
                m_lockImage.transform.parent = ourTr;
                m_lockImage.transform.localPosition = new Vector3(ourTr.localScale.x/2f,-ourTr.localScale.y/2f,-0.1f);
                m_lockImage.transform.localScale = Vector3.one * 0.5f;
                HideLock();
            }
        }

        private void ShowLock(){
            if(m_lockImage) m_lockImage.SetActive(true);
        }
        private void HideLock(){
            if(m_lockImage) m_lockImage.SetActive(false);
        }

        //!
        //! Function coupled to user UI scale changes to update the icon scale
        //!
        private void UpdateUIScale(object sender, float e){
            m_iconScale = Vector3.one * e;
        }

        //!
        //! Update is called once per frame
        //!
        void Update()
        {
            if (m_renderer.isVisible)
            {
                float depth = Vector3.Dot(mainCamTr.position - ourTr.position, mainCamTr.forward);

                ourTr.position = m_parentObject.transform.position;
                ourTr.rotation = mainCamTr.rotation;
                ourTr.localScale = m_iconScale * Mathf.Abs(depth * 0.1f);

                if (!m_lockImage)
                    return;

                //TODO: only necessary to check, if icon is visible by any camera!
                if (m_parentObject._lock){
                    if (!m_lockImage.activeSelf)
                        ShowLock();
                }else{
                    if (m_lockImage.activeSelf)
                        HideLock();
                }
            }
        }
    }
}
