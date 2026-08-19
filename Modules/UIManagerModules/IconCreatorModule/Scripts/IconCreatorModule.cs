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

//! @file "IconCreatorModule.cs"
//! @brief Implementation of the IconCreatorModule, creating icons for scene objects without geometry.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @author Thomas Krüger
//! @version 1
//! @date 16.07.2026
//! @note implementing height-over-ground display

using System;
using System.Collections.Generic;
using UnityEngine;

namespace tracer{

    public class IconCreatorModule : UIManagerModule{

        //!
        //! Flag that defines whether icons are shown or not.
        //!
        private bool m_showIcons = true;
        //!
        //! The list containing all light and camera sceneobjects.
        //!
        private List<SceneObject> m_lightAndCamSceneObjects;
        //!
        //! The root scene object containing all icons.
        //!
        private GameObject m_IconRoot;
        //!
        //! Prefab for the icon.
        //!
        private GameObject m_Icon;
        //!
        //! Sprite for the light icon.
        //!
        private Sprite m_lightSprite;
        //!
        //! Sprite for the light icon.
        //!
        private Sprite m_sunSprite;
        //!
        //! Sprite for the camera icon.
        //!
        private Sprite m_cameraSprite;

        //!
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public IconCreatorModule(string name, Manager manager) : base(name, manager){}

        //!
        //! Init Function
        //!
        protected override void Init(object sender, EventArgs e){
            m_lightAndCamSceneObjects = new List<SceneObject>();
            // [REVIEWV]
            // should become SphereCollider and put onto other layer than default ?!
            m_Icon = Resources.Load("Prefabs/Icon") as GameObject;
            m_lightSprite = Resources.Load<Sprite>("Images/LightIcon");
            m_sunSprite = Resources.Load<Sprite>("Images/button_sun");
            m_cameraSprite = Resources.Load<Sprite>("Images/CameraIcon");

            m_IconRoot = new GameObject("Icons");

            MenuButton hideIconButton = new MenuButton("", toggleIcons, new List<UIManager.Roles>() {UIManager.Roles.LIGHTING, UIManager.Roles.SET, UIManager.Roles.DOP });
            hideIconButton.setIcon("Images/button_hideIcons");
            manager.addButton(hideIconButton);

            SceneManager sceneManager = core.getManager<SceneManager>();
            sceneManager.sceneCreated += recreateIcons;
            sceneManager.sceneUpdated += recreateIcons;
            sceneManager.sceneReset += disposeIcons;

            manager.settings.roles.hasChanged += recreateIcons;

            core.getManager<UIManager>().selectionChanged += SelectionHasChanged;
        }

        //! 
        //! Function called before Unity destroys the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        public override void Dispose(){
            base.Dispose();

            SceneManager sceneManager = core.getManager<SceneManager>();
            sceneManager.sceneCreated -= recreateIcons;
            sceneManager.sceneUpdated -= recreateIcons;
            sceneManager.sceneReset -= disposeIcons;

            manager.settings.roles.hasChanged -= recreateIcons;

            core.getManager<UIManager>().selectionChanged -= SelectionHasChanged;
        }

        private void recreateIcons(object sender, int selectedIndex){
            disposeIcons(this, EventArgs.Empty);
            createIcons(core.getManager<SceneManager>(), EventArgs.Empty);
        }

        private void recreateIcons(object sender, EventArgs e)
        {
            disposeIcons(this, EventArgs.Empty);
            createIcons(core.getManager<SceneManager>(), EventArgs.Empty);
        }

        //!
        //! Function that toggles whether icons are shown or not.
        //!
        private void toggleIcons(){
            if (m_showIcons){
                m_showIcons = false;
                disposeIcons(null, EventArgs.Empty);
            }else{
                m_showIcons = true;
                createIcons(core.getManager<SceneManager>(), EventArgs.Empty);
            }
        }

        //!
        //! Function that parses the given list of scene objects to create and
        //! add icons depending on it's type as child objects.
        //!
        private void createIcons(object sender, EventArgs e){
            if (!m_showIcons)
                return;

            SceneManager sceneManager = core.getManager<SceneManager>();

            foreach (SceneObject sceneObject in sceneManager.getAllSceneObjects()){
                GameObject icon = null;
                //SpriteRenderer renderer = null;
                switch (sceneObject){
                    case SceneObjectLight:
                        if (manager.activeRole == UIManager.Roles.EXPERT ||
                            manager.activeRole == UIManager.Roles.LIGHTING ||
                            manager.activeRole == UIManager.Roles.DOP ||
                            manager.activeRole == UIManager.Roles.SET)
                        {
                            
                            Parameter<Color> colorParameter = sceneObject.getParameter<Color>("color");
                            icon = GameObject.Instantiate(m_Icon, m_IconRoot.transform);
                            IconUpdate iconUpdate = icon.GetComponent<IconUpdate>();
                    
                            switch (sceneObject){
                                case SceneObjectSunLight:
                                    iconUpdate.Init(manager, sceneObject, m_sunSprite, colorParameter, true);
                                    break;
                                default:
                                    iconUpdate.Init(manager, sceneObject, m_lightSprite, colorParameter);
                                    break;
                            }
                            
                            
                            colorParameter.hasChanged += updateIconColor;
                            m_lightAndCamSceneObjects.Add(sceneObject);
                        }
                        break;
                    case SceneObjectCamera:
                        if (manager.activeRole == UIManager.Roles.EXPERT ||
                            manager.activeRole == UIManager.Roles.DOP)
                        {
                            icon = GameObject.Instantiate(m_Icon, m_IconRoot.transform);
                            icon.GetComponent<IconUpdate>().Init(manager, sceneObject, m_cameraSprite);
                            
                            //add to other SceneObjectTypes to show as well
                            icon.AddComponent<HeightOverGround>().Initialize(sceneObject.transform, manager);

                            m_lightAndCamSceneObjects.Add(sceneObject);
                        }
                        break;
                }
            }
        }

        //!
        //! Function for updating the color of an icon.
        //!
        //! @param sender The connected parameter holding the color value.
        //! @param color The color value the icon's color will be set to.
        //!
        private void updateIconColor(object sender, Color color)
        {
            SceneObject sceneObject = (SceneObject) ((AbstractParameter)sender)._parent;
            sceneObject._icon.GetComponent<SpriteRenderer>().color = color;
        }

        //!
        //! Function for disposing and cleanup of all created gizmos.
        //!
        private void disposeIcons(object sender, EventArgs e){
            foreach(SceneObject sceneObject in m_lightAndCamSceneObjects){
                if (sceneObject.GetType().BaseType == typeof(SceneObjectLight)){
                    sceneObject.getParameter<Color>("color").hasChanged -= updateIconColor;
                }
                sceneObject.GetComponent<HeightOverGround>()?.DestroyViz();
                
                UnityEngine.Object.Destroy(sceneObject._icon);
            }
        }
    
        //!
        //! Called every time a scene object has been selected. Could change IconUpdate, Could show HeightOverGround
        //!
        //! @param sender The UI manager.
        //! @param sceneObjects a list of the currently selected objects.
        //!
        private void SelectionHasChanged(object sender, List<SceneObject> selectedSOs){
            //Debug.Log("Icon Creator, SelectionHasChanged: "+selectedSOs.Count);
            //Debug.Log("we have m_lightAndCamSceneObjects: "+m_lightAndCamSceneObjects.Count);
            //do the below via dict for performance reasons
            foreach(SceneObject lightOrCamSO in m_lightAndCamSceneObjects) {
                if (!selectedSOs.Contains(lightOrCamSO)) {
                    //Debug.Log("Hide HOG? At "+lightOrCamSO.gameObject.name);
                    if (lightOrCamSO._icon != null)
                        lightOrCamSO._icon.GetComponent<HeightOverGround>()?.HideViz();
                }
            }

            foreach(SceneObject selectedSO in selectedSOs) {
                if (m_lightAndCamSceneObjects.Contains(selectedSO)) {
                    //Debug.Log("Show HOG? At "+selectedSO.gameObject.name);
                    if (selectedSO._icon != null)
                        selectedSO._icon.GetComponent<HeightOverGround>()?.ShowViz(true);
                }
            
            }
        }

    }
    
}
