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

//! @file "SceneObjectPointLight.cs"
//! @brief implementation SceneObjectDirectionalLight as a specialisation of the light object.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 03.02.2022

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace tracer
{
    //!
    //! Implementation of the TRACER directional light object as a specialisation of the light object
    //!
    public class SceneObjectAreaLight : SceneObjectLight
    {
        //!
        //! Factory to create a new SceneObject and do it's initialisation.
        //! Use this function instead GameObject.AddComponen<>!
        //!
        //! @param gameObject The gameObject the new SceneObject will be attached to.
        //! @sceneID The scene ID for the new SceneObject.
        //!
        public static new SceneObjectAreaLight Attach(GameObject gameObject, byte sceneID)
        {
            SceneObjectAreaLight obj = gameObject.AddComponent<SceneObjectAreaLight>();
            obj.Init(sceneID);

            return obj;
        }

        // Start is called before the first frame update
        public override void Awake()
        {
            base.Awake();
        }

        // Update is called once per frame
        public override void Update()
        {
            base.Update();

        }
    }
}
