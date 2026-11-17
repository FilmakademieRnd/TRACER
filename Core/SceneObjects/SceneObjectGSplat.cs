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

//! @file "SceneObjectGSplat.cs"
//! @brief Implementation of the SceneObjectGSplat as a specialisation of SceneObject.
//! @author Simon Spielmann
//! @version 0
//! @date 11.11.2026

using UnityEngine;

namespace tracer
{
    //!
    //! Implementation of the TRACER camera object as a specialisation of the SceneObject
    //!
    public class SceneObjectGSplat : SceneObject
    {
        //!
        //! 
        //!
        public Parameter<float> scaleFactor;

        //!
        //! 
        //!
        public Parameter<float> brightness;

        private GsplatRenderer m_renderer;

        //!
        //! Factory to create a new SceneObject and do it's initialisation.
        //! Use this function instead GameObject.AddComponen<>!
        //!
        //! @param gameObject The gameObject the new SceneObject will be attached to.
        //! @sceneID The scene ID for the new SceneObject.
        //!
        public static new SceneObjectGSplat Attach(GameObject gameObject, byte sceneID)
        {
            SceneObjectGSplat obj = gameObject.AddComponent<SceneObjectGSplat>();
            obj.Init(sceneID);

            return obj;
        }

        // Start is called before the first frame update
        public override void Awake()
        {
            base.Awake();
            m_renderer = GetComponent<GsplatRenderer>();

            scaleFactor = new Parameter<float>(m_renderer.m_ScaleFactor, "scaleFactor", this, true, UIManager.Roles.EXPERT);
            scaleFactor.hasChanged += updateScaleFactor;
            brightness = new Parameter<float>(m_renderer.m_Brightness, "brightness", this, true, UIManager.Roles.EXPERT);
            brightness.hasChanged += updateBrightness;
        }

        //!
        //! Function called, when Unity emit it's OnDestroy event.
        //!
        public override void OnDestroy()
        {
            base.OnDestroy();
            scaleFactor.hasChanged -= updateScaleFactor;
            brightness.hasChanged -= updateBrightness;
        }

        // Update is called once per frame
        public override void Update()
        {
            base.Update();
        }

        //!
        //! 
        //! @param   sender     Object calling the update function
        //! @param   a          new fov value
        //!
        private void updateScaleFactor(object sender, float a)
        {
            m_renderer.m_ScaleFactor = a;
        }

        //!
        //! 
        //! @param   sender     Object calling the update function
        //! @param   a          new aspect ratio value
        //!
        private void updateBrightness(object sender, float a)
        {
           m_renderer.m_Brightness = a;
        }

    }
}