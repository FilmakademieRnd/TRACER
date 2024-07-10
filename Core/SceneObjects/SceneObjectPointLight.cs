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

//! @file "SceneObjectPointLight.cs"
//! @brief implementation SceneObjectPointLight as a specialisation of the light object.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 01.03.2022

using UnityEngine;

namespace tracer
{
    //!
    //! Implementation of the TRACER point light object as a specialisation of the light object
    //!
    public class SceneObjectPointLight : SceneObjectLight
    {
        //!
        //! the range of the light
        //!
        public Parameter<float> range;

        //!
        //! Factory to create a new SceneObject and do it's initialisation.
        //! Use this function instead GameObject.AddComponen<>!
        //!
        //! @param gameObject The gameObject the new SceneObject will be attached to.
        //! @sceneID The scene ID for the new SceneObject.
        //!
        public static new SceneObjectPointLight Attach(GameObject gameObject, byte sceneID = 254)
        {
            SceneObjectPointLight obj = gameObject.AddComponent<SceneObjectPointLight>();
            obj.Init(sceneID);

            return obj;
        }

        // Start is called before the first frame update
        public override void Awake()
        {
            base.Awake();

            _light = this.GetComponent<Light>();
            if (_light)
            {
                range = new Parameter<float>(_light.range, "range", this);
                range.hasChanged += updateRange;
            }
            else
                Helpers.Log("no light component found!");
        }


        //!
        //! Function called, when Unity emit it's OnDestroy event.
        //!
        public override void OnDestroy()
        {
            base.OnDestroy();
            range.hasChanged -= updateRange;
        }

        //!
        //! Update the light range of the GameObject.
        //! @param   sender     Object calling the update function
        //! @param   a          new range value
        //!
        private void updateRange(object sender, float a)
        {
            _light.range = a;
            emitHasChanged((AbstractParameter)sender);
        }

        // Update is called once per frame
        public override void Update()
        {
            base.Update();
#if UNITY_EDITOR
            updateSceneObjectLightParameters();
#endif
        }

        //!
        //! updates the Unity light component specific parameters and informs all connected TRACER parameters about the change
        //!
        private void updateSceneObjectLightParameters()
        {
            if (_light.range != range.value)
                range.value = _light.range;
        }
    }
}
