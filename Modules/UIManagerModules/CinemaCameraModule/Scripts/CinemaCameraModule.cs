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

//! @file "CinemaCameraModule.cs"
//! @brief Implementation of the Cinema Camera Module for professional camera and lens selection
//! @author Claude Code Assistant
//! @version 0
//! @date 29.09.2025

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace tracer
{
    public class CinemaCameraModule : UIManagerModule
    {
        private MenuButton m_cinemaCameraButton;
        private MenuTree m_cinemaCameraMenu;
        private SceneManager m_sceneManager;

        public CinemaCameraModule(string name, Manager manager) : base(name, manager)
        {
            load = false;
        }

        protected override void Start(object sender, EventArgs e)
        {
            base.Start(sender, e);

            m_sceneManager = core.getManager<SceneManager>();

            m_cinemaCameraButton = new MenuButton("Cinema Camera", showCinemaCameraMenu, new List<UIManager.Roles>() { UIManager.Roles.DOP });

            manager.addButton(m_cinemaCameraButton);

            createCinemaCameraMenu();
        }

        public override void Dispose()
        {
            base.Dispose();

            if (m_cinemaCameraButton != null)
            {
                manager.removeButton(m_cinemaCameraButton);
                m_cinemaCameraButton = null;
            }
        }

        private void showCinemaCameraMenu()
        {
            manager.showMenu(m_cinemaCameraMenu);
        }

        private void createCinemaCameraMenu()
        {
            m_cinemaCameraMenu = new MenuTree()
                .Begin(MenuItem.IType.VSPLIT)
                    // ARRI Cameras
                    .Begin(MenuItem.IType.HSPLIT)
                        .Add(new Parameter<Action>(() => ApplyCameraPreset("ARRI ALEXA 35", new Vector2(36.70f, 25.54f), 2.39f, 60f, 1.4f), "ALEXA 35"))
                        .Add(new Parameter<Action>(() => ApplyCameraPreset("ARRI ALEXA Mini LF", new Vector2(36.70f, 25.54f), 1.78f, 50f, 2.0f), "ALEXA Mini LF"))
                        .Add(new Parameter<Action>(() => ApplyCameraPreset("ARRI ALEXA 65", new Vector2(54.12f, 25.58f), 2.39f, 40f, 2.8f), "ALEXA 65"))
                    .End()

                    // RED Cameras
                    .Begin(MenuItem.IType.HSPLIT)
                        .Add(new Parameter<Action>(() => ApplyCameraPreset("RED V-RAPTOR XL", new Vector2(40.96f, 21.60f), 1.90f, 45f, 2.0f), "V-RAPTOR XL"))
                        .Add(new Parameter<Action>(() => ApplyCameraPreset("RED DSMC3 HELIUM", new Vector2(29.90f, 15.77f), 1.90f, 50f, 2.8f), "HELIUM"))
                    .End()

                    // Sony Cameras
                    .Begin(MenuItem.IType.HSPLIT)
                        .Add(new Parameter<Action>(() => ApplyCameraPreset("Sony FX9", new Vector2(35.70f, 18.80f), 1.90f, 55f, 2.8f), "Sony FX9"))
                        .Add(new Parameter<Action>(() => ApplyCameraPreset("Sony VENICE 2", new Vector2(36.20f, 24.10f), 1.50f, 50f, 2.0f), "VENICE 2"))
                    .End()

                    // Canon Cameras
                    .Begin(MenuItem.IType.HSPLIT)
                        .Add(new Parameter<Action>(() => ApplyCameraPreset("Canon C500 Mark II", new Vector2(36.00f, 24.00f), 1.78f, 50f, 2.8f), "C500 Mark II"))
                        .Add(new Parameter<Action>(() => ApplyCameraPreset("Canon C300 Mark III", new Vector2(26.20f, 13.80f), 1.90f, 55f, 2.0f), "C300 Mark III"))
                    .End()

                    // Prime Lenses
                    .Begin(MenuItem.IType.HSPLIT)
                        .Add(new Parameter<Action>(() => ApplyLensPreset(18f, 1.4f), "18mm"))
                        .Add(new Parameter<Action>(() => ApplyLensPreset(25f, 1.4f), "25mm"))
                        .Add(new Parameter<Action>(() => ApplyLensPreset(35f, 1.4f), "35mm"))
                        .Add(new Parameter<Action>(() => ApplyLensPreset(50f, 1.4f), "50mm"))
                        .Add(new Parameter<Action>(() => ApplyLensPreset(85f, 1.4f), "85mm"))
                    .End()

                    // Zoom Lenses
                    .Begin(MenuItem.IType.HSPLIT)
                        .Add(new Parameter<Action>(() => ApplyLensPreset(24f, 2.8f), "24-70 Wide"))
                        .Add(new Parameter<Action>(() => ApplyLensPreset(50f, 2.8f), "24-70 Mid"))
                        .Add(new Parameter<Action>(() => ApplyLensPreset(70f, 2.8f), "24-70 Tele"))
                    .End()

                    // Aspect Ratios
                    .Begin(MenuItem.IType.HSPLIT)
                        .Add(new Parameter<Action>(() => ApplyAspectRatio(1.33f), "4:3"))
                        .Add(new Parameter<Action>(() => ApplyAspectRatio(1.78f), "16:9"))
                        .Add(new Parameter<Action>(() => ApplyAspectRatio(1.85f), "1.85:1"))
                        .Add(new Parameter<Action>(() => ApplyAspectRatio(2.39f), "2.39:1"))
                    .End()
                .End();

            m_cinemaCameraMenu.caption = "Cinema Camera Presets";
            m_cinemaCameraMenu.scrollable = true;
        }

        private void ApplyCameraPreset(string cameraName, Vector2 sensorSize, float aspectRatio, float fov, float aperture)
        {
            // Use TRACER's dedicated camera list instead of filtering all scene objects
            List<SceneObjectCamera> cameras = m_sceneManager.sceneCameraList;

            if (cameras.Count == 0)
            {
                Helpers.Log("No cameras found in scene to apply preset to!", Helpers.logMsgType.WARNING);
                return;
            }

            foreach (SceneObjectCamera camera in cameras)
            {
                // Use TRACER parameter system - setValue triggers proper updates and network sync
                camera.sensorSize.setValue(sensorSize);
                camera.fov.setValue(fov);
                camera.aperture.setValue(aperture);
                camera.aspect.setValue(aspectRatio);
            }

            Helpers.Log($"Applied {cameraName} preset to {cameras.Count} camera(s)");
        }

        private void ApplyLensPreset(float focalLength, float aperture)
        {
            // Use TRACER's dedicated camera list
            List<SceneObjectCamera> cameras = m_sceneManager.sceneCameraList;

            if (cameras.Count == 0)
            {
                Helpers.Log("No cameras found in scene to apply lens preset to!", Helpers.logMsgType.WARNING);
                return;
            }

            foreach (SceneObjectCamera camera in cameras)
            {
                // Calculate FOV from focal length and sensor size
                float fov = 2.0f * Mathf.Atan(camera.sensorSize.value.y / (2.0f * focalLength)) * Mathf.Rad2Deg;
                camera.fov.setValue(fov);
                camera.aperture.setValue(aperture);
            }

            Helpers.Log($"Applied {focalLength}mm T{aperture} lens preset to {cameras.Count} camera(s)");
        }

        private void ApplyAspectRatio(float aspectRatio)
        {
            // Use TRACER's dedicated camera list
            List<SceneObjectCamera> cameras = m_sceneManager.sceneCameraList;

            if (cameras.Count == 0)
            {
                Helpers.Log("No cameras found in scene to apply aspect ratio to!", Helpers.logMsgType.WARNING);
                return;
            }

            foreach (SceneObjectCamera camera in cameras)
            {
                camera.aspect.setValue(aspectRatio);
            }

            Helpers.Log($"Applied {aspectRatio:F2}:1 aspect ratio to {cameras.Count} camera(s)");
        }
    }
}