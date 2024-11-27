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

//! @file "SelectionOutlineModule.cs"
//! @brief Implementation of the TRACER SelectionOutlineModule, adding a outline material to a selected scene object.
//! @author Simon Spielmann
//! @version 0
//! @date 29.03.2022

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace tracer
{
    public class SelectionOutlineModule : UIManagerModule
    {
        //!
        //! The outline material to be added to the selectet object.
        //!
        private Material _outlineMaterial;

        //!
        //! The outline material to be added to a locked object.
        //!
        private Material _outlineLockMaterial;

        //!
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public SelectionOutlineModule(string name, Manager manager) : base(name, manager)
        {
        }

        //! 
        //! Function called when Unity initializes the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Init(object sender, EventArgs e)
        {
            Shader outlineShader = Resources.Load<Shader>("Shader/SelectionOutlineShader");
            _outlineMaterial = new Material(outlineShader);
            _outlineLockMaterial = new Material(outlineShader);
            _outlineLockMaterial.SetColor("_OutlineColor", Color.red);

            manager.selectionAdded += HighlightSelection;
            manager.selectionRemoved += DisableHighlightSelection;

            manager.highlightLocked += HighlightLocked;
            manager.unhighlightLocked += DisableHighlightLocked;
        }

        //!
        //! Function that is called when the UIManager signals an selection.
        //! Will add the outline material to all renderes of the given scene object.
        //!
        //! @param sender A reference to the UIManager.
        //! @param eventArgs Event Arguments containing the Scene Object and the highlight color.
        //!
        private void HighlightLocked(object sender, SceneObject sceneObject)
        {
            if ((sceneObject is SceneObjectCamera) || sceneObject is SceneObjectLight){
                //show 3d ui lock
                
                return;
            }

            Renderer[] renderers = sceneObject.GetComponentsInChildren<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                List<Material> materials = renderer.sharedMaterials.ToList();
                materials.Add(_outlineLockMaterial);
                renderer.materials = materials.ToArray();
            }
        }

        //!
        //! Function that is called when the UIManager signals an selection.
        //! Will add the outline material to all renderes of the given scene object.
        //!
        //! @param sender A reference to the UIManager.
        //! @param sceneObject The selected sceneObject.
        //!
        private void HighlightSelection(object sender, SceneObject sceneObject)
        {
            if ((sceneObject is SceneObjectCamera) || sceneObject is SceneObjectLight)
                return;

            Renderer[] renderers = sceneObject.GetComponentsInChildren<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                List<Material> materials = renderer.sharedMaterials.ToList();
                materials.Add(_outlineMaterial);
                renderer.materials = materials.ToArray();
            }
        }

        //!
        //! Function that is called when the UIManager signals an selection removed.
        //! Will remove the outline material on all renderes of the given scene object.
        //!
        //! @param sender A reference to the UIManager.
        //! @param sceneObject The selected sceneObject.
        //!
        private void DisableHighlightSelection(object sender, SceneObject sceneObject)
        {
            if (sceneObject)
            {
                if ((sceneObject is SceneObjectCamera) || sceneObject is SceneObjectLight)
                    return;

                Renderer[] renderers = sceneObject.GetComponentsInChildren<Renderer>();

                foreach (Renderer renderer in renderers)
                {
                    List<Material> materials = renderer.sharedMaterials.ToList();
                    materials.Remove(_outlineMaterial);

                    renderer.materials = materials.ToArray();
                }
            }
        }

        //!
        //! Function that is called when the UIManager signals an selection removed.
        //! Will remove the outline material on all renderes of the given scene object.
        //!
        //! @param sender A reference to the UIManager.
        //! @param sceneObject The selected sceneObject.
        //!
        private void DisableHighlightLocked(object sender, SceneObject sceneObject)
        {
            if ((sceneObject is SceneObjectCamera) || sceneObject is SceneObjectLight)
                return;

            Renderer[] renderers = sceneObject.GetComponentsInChildren<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                List<Material> materials = renderer.sharedMaterials.ToList();
                materials.Remove(_outlineLockMaterial);

                renderer.materials = materials.ToArray();
            }
        }
    }
}