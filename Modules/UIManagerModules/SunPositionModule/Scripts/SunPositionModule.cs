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
using System.Collections;
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
        private Coroutine m_timeAnimationCoroutine;
        private Coroutine m_locationAnimationCoroutine;


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

                ClassParameter<Action> timeButton = new ClassParameter<Action>(pickCurrentTime, "Current Time");
                ClassParameter<Action> locationButton = new ClassParameter<Action>(pickCurrentLocation, "Current Location");
                ClassParameter<Action> removeSunButton = new ClassParameter<Action>(removeSun, "Remove Sun");

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

        private System.Collections.IEnumerator Delay(float sec, System.Action action) { yield return new WaitForSeconds(sec); action?.Invoke(); }

        //!
        //! Function to copy the current local time to the parameters.
        //!
        private void pickCurrentTime()
        {
            manager.SendCommand(new InputManager.StartGPSCommand());
            DateTime dateTime = DateTime.Now;

            if (m_timeAnimationCoroutine != null)
            {
                core.StopCoroutine(m_timeAnimationCoroutine);
            }
           
            m_timeAnimationCoroutine = core.StartCoroutine(AnimateTimeValues(
                dateTime.Month,
                dateTime.Day,
                dateTime.Hour,
                dateTime.Minute,
                0.8f
            ));
            core.StartCoroutine(Delay(0.9f, () => UpdateMenu()));

            UpdateMenu();
        }

        private IEnumerator AnimateTimeValues(int targetMonth, int targetDay, int targetHour, int targetMinute, float duration)
        {
            // Clamp initial and target date values to ensure valid DateTime construction
            int startMonth = Mathf.Clamp(m_month.value, 1, 12);
            int startDay = Mathf.Clamp(m_day.value, 1, DateTime.DaysInMonth(1900, startMonth));
            targetMonth = Mathf.Clamp(targetMonth, 1, 12);
            targetDay = Mathf.Clamp(targetDay, 1, DateTime.DaysInMonth(1900, targetMonth));

            DateTime startDate = new DateTime(1900, startMonth, startDay);
            DateTime targetDate = new DateTime(1900, targetMonth, targetDay);

            // Direct day difference within the same year (no wrap-around across Dec/Jan)
            int dayDiff = targetDate.DayOfYear - startDate.DayOfYear;

            // Convert time to total minutes since midnight for continuous interpolation
            float startTotalMinutes = m_hour.value * 60 + m_minute.value;
            float targetTotalMinutes = targetHour * 60 + targetMinute;

            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsedTime / duration));

                // 1. Add interpolated days within the year (automatically handles month lengths like 28/30/31)
                int dayOffset = Mathf.RoundToInt(dayDiff * t);
                DateTime currentDate = startDate.AddDays(dayOffset);

                // 2. Interpolate total minutes and split back into hours and minutes
                int currentTotalMinutes = Mathf.RoundToInt(Mathf.Lerp(startTotalMinutes, targetTotalMinutes, t));
                int currentHour = Mathf.Clamp(currentTotalMinutes / 60, 0, 23);
                int currentMinute = currentTotalMinutes % 60;

                // Only update parameters when their integer values actually change
                if (m_month.value != currentDate.Month) m_month.value = currentDate.Month;
                if (m_day.value != currentDate.Day) m_day.value = currentDate.Day;
                if (m_hour.value != currentHour) m_hour.value = currentHour;
                if (m_minute.value != currentMinute) m_minute.value = currentMinute;

                yield return null;
            }

            // Ensure exact target values are set at the end of the animation
            m_month.value = targetMonth;
            m_day.value = targetDay;
            m_hour.value = targetHour;
            m_minute.value = targetMinute;

            m_timeAnimationCoroutine = null;
        }

        private void UpdateMenu()
        {
            manager.hideMenu();
            manager.showMenu(m_sunMenu);
        }

        //!
        //! Function to copy the current local position to the parameters.
        //!
        private void pickCurrentLocation()
        {
            manager.SendCommand(new InputManager.StartGPSCommand());

            // Retrieve GPS coordinates from Unity's location service (replace with your TRACER GPS data source if applicable)
            int targetLongitude = Mathf.RoundToInt(Input.location.lastData.longitude);
            int targetLatitude = Mathf.RoundToInt(Input.location.lastData.latitude);

            // Stop any currently running location animation before starting a new one
            if (m_locationAnimationCoroutine != null)
            {
                core.StopCoroutine(m_locationAnimationCoroutine);
            }

            m_locationAnimationCoroutine = core.StartCoroutine(AnimateLocationValues(
                targetLongitude,
                targetLatitude,
                0.5f
            ));

            core.StartCoroutine(Delay(0.6f, () => UpdateMenu()));

            manager.hideMenu();
            manager.showMenu(m_sunMenu);
        }

        //!
        //! Coroutine that smoothly interpolates longitude and latitude parameters over a given duration.
        //!
        //! @param targetLongitude Target geographical longitude (-180 to 180)
        //! @param targetLatitude  Target geographical latitude (-90 to 90)
        //! @param duration        Duration of the animation in seconds
        //!
        private IEnumerator AnimateLocationValues(int targetLongitude, int targetLatitude, float duration)
        {
            // Clamp targets to valid geographical coordinate bounds
            targetLongitude = Mathf.Clamp(targetLongitude, -180, 180);
            targetLatitude = Mathf.Clamp(targetLatitude, -90, 90);

            float startLongitude = m_longitude.value;
            float startLatitude = m_latitude.value;

            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsedTime / duration));

                int currentLongitude = Mathf.RoundToInt(Mathf.Lerp(startLongitude, targetLongitude, t));
                int currentLatitude = Mathf.RoundToInt(Mathf.Lerp(startLatitude, targetLatitude, t));

                // Only update parameters when their integer values actually change
                if (m_longitude.value != currentLongitude) m_longitude.value = currentLongitude;
                if (m_latitude.value != currentLatitude) m_latitude.value = currentLatitude;

                yield return null;
            }

            // Ensure exact target values are set at the end of the animation
            m_longitude.value = targetLongitude;
            m_latitude.value = targetLatitude;

            m_locationAnimationCoroutine = null;
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
