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

//! @file "SunPositionModule.cs"
//! @brief Implementation of the SunPositionModule, creating icons for scene objects without geometry.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 29.03.2022

using System;
using System.Collections.Generic;
using UnityEngine;

namespace tracer
{

    public class SunPositionModule : UIManagerModule
    {
        //!
        //! The gegraphical longitude to calculate the sun position.
        //!
        private Parameter<int> m_longitude;
        //!
        //! The gegraphical latitude to calculate the sun position.
        //!
        private Parameter<int> m_latitude;
        //!
        //! The the minunte of the time used to calculate the sun position.
        //!
        private Parameter<int> m_minute;
        //!
        //! The the minunte of the time used to calculate the sun position.
        //!
        private Parameter<int> m_hour;
        //!
        //! The the minunte of the time used to calculate the sun position.
        //!
        private Parameter<int> m_day;
        //!
        //! The the minunte of the time used to calculate the sun position.
        //!
        private Parameter<int> m_month;
        //!
        //! The menu handling the sun positioning parameters.
        //!
        private MenuTree m_sunMenu;
        //!
        //! The directional light simulating the sun.
        //!
        private GameObject m_sun = null;
        private SceneObjectSunLight m_sunLight = null;
        

        //!
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public SunPositionModule(string name, Manager manager) : base(name, manager)
        {

        }

        //!
        //! Init Function
        //!
        protected override void Init(object sender, EventArgs e)
        {
            base.Init(sender, e);

            MenuButton toggleMenuButton = new MenuButton("", toggleMenu, new List<UIManager.Roles>() { UIManager.Roles.LIGHTING, UIManager.Roles.SET, UIManager.Roles.DOP });
            toggleMenuButton.setIcon("Images/button_sun");
            manager.addButton(toggleMenuButton);

            //MenuButton fpsButton = new MenuButton("FPS", FPS, new List<UIManager.Roles>() { UIManager.Roles.LIGHTING, UIManager.Roles.SET, UIManager.Roles.DOP });
            //manager.addButton(fpsButton);
        }

        void FPS()
        {
            core.speedUpFPS();
        }

        //!
        //! Function to remove the sun directional light and the attached SceneObject.
        //!
        private void removeSun()
        {
            if (m_sun != null)
            {
                if (m_sunLight != null)
                {
                    core.removeParameterObject(m_sunLight);
                }
                UnityEngine.Object.Destroy(m_sun);
                core.getManager<SceneManager>().emitSceneUpdated();
            }
        }

        //!
        //! Function to create a new directional light with attached SceneObject to be controlled via TRACER.
        //!
        private void createSun()
        {
            if (m_sun == null)
            {
                SceneManager sceneManager = core.getManager<SceneManager>();

                m_sun = new GameObject("VPET_Sun");
                m_sun.transform.SetParent(findLightRoot());

                Light lightComponent = m_sun.AddComponent<Light>();
                lightComponent.type = LightType.Directional;
                lightComponent.color = Color.white;
                lightComponent.intensity = 1;
                lightComponent.shadows = LightShadows.Soft;
                lightComponent.shadowStrength = 0.8f;
                lightComponent.shadowBias = 0f;
                lightComponent.shadowNormalBias = 1f;

                m_sunLight = SceneObjectSunLight.Attach(m_sun, 255);

                m_longitude = m_sunLight.getParameter<int>("Longitude");
                m_latitude = m_sunLight.getParameter<int>("Latitude");
                m_minute = m_sunLight.getParameter<int>("Minute");
                m_hour = m_sunLight.getParameter<int>("Hour");
                m_day = m_sunLight.getParameter<int>("Day");
                m_month = m_sunLight.getParameter<int>("Month");

                Parameter<Action> timeButton = new Parameter<Action>(pickCurrentTime, "Current Time");
                Parameter<Action> locationButton = new Parameter<Action>(pickCurrentLocation, "Current Location");
                Parameter<Action> removeSunButton = new Parameter<Action>(removeSun, "Remove Sun");

                m_sunMenu = new MenuTree()
                 .Begin(MenuItem.IType.VSPLIT)
                      // === Time ===
                      .Add("Time")
                      .Begin(MenuItem.IType.HSPLIT)
                          .Add("Month:")
                          .Add(m_month)
                          .Add("Day:")
                          .Add(m_day)
                          .Add("Hour:")
                          .Add(m_hour)
                          .Add("Minute:")
                          .Add(m_minute)
                      .End()

                      // === Location ===
                      .Add("Location")
                      .Begin(MenuItem.IType.HSPLIT)
                          .Add("Longitude:")
                          .Add(m_longitude)
                          .Add("Latitude:")
                          .Add(m_latitude)
                      .End()

                      // === Buttons ===
                      .Begin(MenuItem.IType.HSPLIT)
                          .Add(timeButton)
                          .Add(locationButton)
                          .Add(removeSunButton)
                      .End()
                .End();

                core.getManager<SceneManager>().emitSceneUpdated();
            }
        }

        //!
        //! Function to find a proper parent for the new sun light.
        //!
        //! @return The patent transform the new sun light will be attached to.
        //!
        private Transform findLightRoot()
        {
            Transform lightRoot = null; 
            GameObject root = core.getManager<SceneManager>().scnRoot;
            if (root != null)
            {
                lightRoot = root.transform.Find("Light");
                if (lightRoot == null)
                    lightRoot = root.transform;
            }

            return lightRoot;
        }

        //!
        //! Function to copy the current local time to the parameters.
        //!
        private void pickCurrentTime()
        {
            DateTime dateTime = DateTime.Now;
            m_month.value = dateTime.Month;
            m_day.value = dateTime.Day;
            m_hour.value = dateTime.Hour;
            m_minute.value = dateTime.Minute;

            manager.hideMenu();
            manager.showMenu(m_sunMenu);
        }

        //!
        //! Function to copy the current local position to the parameters.
        //!
        private void pickCurrentLocation()
        {
            // ...
        }

        //!
        //! Function that toggles whether sun positioning menu is visible or not.
        //!
        private void toggleMenu()
        {
            if (m_sun == null)
                createSun();
            
            if (!m_sunMenu.visible)
            {
                manager.hideMenu();
                manager.showMenu(m_sunMenu);
            }
            else
                manager.hideMenu();
        }
    }
}
