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

//! @file "_core.cs"
//! @brief TRACER _core implementation. Central class for TRACER initalization. Manages all Tracer Managers and their modules.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 01.03.2022

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using System.Diagnostics;
using static tracer.AbstractParameter;

namespace tracer
{
    //!
    //! Central class for TRACER initalization.
    //! Manages all Tracer Managers and their modules.
    //!
    public class Core : CoreInterface
    {
        //!
        //! Class containing the cores settings.
        //! This is persistent data saved and loaded to/from disk.
        //!
        public class coreSettings : Settings
        {
            //!
            //! Default screen size (unused)
            //!
            public Vector2Int screenSize = new Vector2Int(1280, 720);
            //! 
            //! VSync on every frame.
            //!
            public int vSyncCount = 1;
            //! 
            //! Global frame rate in t per second.
            //! 
            public int framerate = 60;
        }
        //!
        //! The _core settings.
        //!
        private Settings _settings;
        //!
        //! Getter and setter for the _core settings.
        //!
        public coreSettings settings
        {
            get { return (coreSettings)_settings; }
            set { _settings = value; }
        }
        //!
        //! Flag determining wether the Tracer instance acts as a server or client.
        //!
        public bool isServer = false;
        //!
        //! The current local time stores as value between 0 and 255.
        //!
        private byte m_time = 0;
        //!
        //! The current local time stores as value between 0 and 255.
        //!
        public byte time
        {
            set => m_time = value;
            get => m_time;
        }
        //!
        //! The base for the number of time steps the System uses.
        //!
        private static int s_timestepsBase = 128;
        //!
        //! The max value for the local time (multiples of framerate).
        //!
        private byte m_timesteps;
        //!
        //! The max value for the local time.
        //!
        public byte timesteps
        {
            get => m_timesteps;
        }

        public string deepLink;
        //!
        //! The global dictionary of parameter objects.
        //! The structure is Dictionary<client/scene ID, Dictionary<ParameterObject ID, ParameterObject>>
        //!
        private Dictionary<byte, Dictionary<short, ParameterObject>> m_parameterObjectList;
        //!
        //! The current orientation of the device;
        //!
        private DeviceOrientation m_orientation;
        //!
        //! Getter for the parameter object list.
        //!
        //! @return A reference to the parameter object list.
        //!
        public ref Dictionary<byte, Dictionary<short, ParameterObject>> parameterObjectList
        {
            get => ref m_parameterObjectList;
        }
        //!
        //! Event invoked when an Unity Update() callback is triggered.
        //!
        public event EventHandler updateEvent;
        //!
        //! Event invoked when an Unity Awake() callback is triggered.
        //!
        public event EventHandler awakeEvent;
        //!
        //! Event invoked after the awakeEvent is triggered.
        //!
        public event EventHandler lateAwakeEvent;
        //!
        //! Event invoked when an Unity Start() callback is triggered.
        //!
        public event EventHandler startEvent;
        //!
        //! Event invoked when an Unity OnDestroy() callback is triggered.
        //!
        public event EventHandler destroyEvent;
        //!
        //! Event invoked when Tracer's global timer ticks.
        //!
        public event EventHandler timeEvent;
        //!
        //! Event invoked when the device orientation has changed
        //!
        public event EventHandler<float> orientationChangedEvent;
        //!
        //! Event invoked every second.
        //!
        public event EventHandler<byte> syncEvent;

        //public event  EventHandler<string> QrCodeReady;
        [SerializeField] public bool useRandomCID = false;

        //!
        //! Unity's Awake callback, used for Initialization of all Managers and modules.
        //!
        void Awake()
        {
            // enable/disable logging
#if UNITY_EDITOR
            UnityEngine.Debug.unityLogger.logEnabled = true;
#else
            Helpers.Log("Warning, Unity Logging has been disabled, look at Core.cs!", Helpers.logMsgType.WARNING);
            UnityEngine.Debug.unityLogger.logEnabled = false;
#endif

            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                deepLink = Application.absoluteURL;
            }

            _settings = new coreSettings();
            m_timesteps = (byte)((s_timestepsBase / settings.framerate) * settings.framerate);
            m_parameterObjectList = new Dictionary<byte, Dictionary<short, ParameterObject>>();

            // Create network manager
            NetworkManager networkManager = new NetworkManager(typeof(NetworkManagerModule), this);
            m_managerList.Add(typeof(NetworkManager), networkManager);

            //Create scene manager
            SceneManager sceneManager = new SceneManager(typeof(SceneManagerModule), this);
            m_managerList.Add(typeof(SceneManager), sceneManager);

            //Create UI manager
            UIManager uiManager = new UIManager(typeof(UIManagerModule), this);
            m_managerList.Add(typeof(UIManager), uiManager);

            //Create Input manager
            InputManager inputManager = new InputManager(typeof(InputManagerModule), this);
            m_managerList.Add(typeof(InputManager), inputManager);

            //Create Animation manager
            AnimationManager animationManager = new AnimationManager(typeof(AnimationManagerModule), this);
            m_managerList.Add(typeof(AnimationManager), animationManager);

            LoadSettings();

            settings.screenSize.x = Screen.currentResolution.width;
            settings.screenSize.y = Screen.currentResolution.height;

            awakeEvent?.Invoke(this, new EventArgs());
            lateAwakeEvent?.Invoke(this, new EventArgs());
        }

        //!
        //! Unity's Start callback, used for Late initialization.
        //!
        void Start()
        {
            // Sync framerate to monitors refresh rate
            QualitySettings.vSyncCount = settings.vSyncCount;
            Application.targetFrameRate = settings.framerate;

            m_orientation = Input.deviceOrientation;

            InvokeRepeating("checkDeviceOrientation", 0f, 1f);
            InvokeRepeating("pingDataHub", 0f, 1f);
            InvokeRepeating("updateTime", 0f, Mathf.FloorToInt(1000f / settings.framerate) / 1000f); // computation to match the ms int scala of an QtTimer used in SyncServer

            startEvent?.Invoke(this, new EventArgs());
        }

        //!
        //! Unity's OnDestroy callback, used to invoke a destroy event to inform TRACER modules.
        //!
        private void OnDestroy()
        {
            SaveSettings();
            destroyEvent?.Invoke(this, new EventArgs());
        }

        //!
        //! Unity's Update callback, used to invoke a update event to inform TRACER modules.
        //!
        private void Update()
        {
            QualitySettings.vSyncCount = 1;
            updateEvent?.Invoke(this, EventArgs.Empty);
        }

        private void checkDeviceOrientation()
        {
            if (Input.deviceOrientation != m_orientation)
            {
                orientationChangedEvent.Invoke(this, 0f);
                /// [DEACTIVATED BACAUSE WE DONT USE PORTAIT MODE] ///
                //Debug.Log("ORIENTATION CHANGED TO: " + Input.deviceOrientation);
                //Camera mainCamera = Camera.main;
                //if ((Input.deviceOrientation == DeviceOrientation.Portrait &&
                //     (m_orientation == DeviceOrientation.LandscapeLeft ||
                //      m_orientation == DeviceOrientation.LandscapeRight))
                //      ||
                //     ((Input.deviceOrientation == DeviceOrientation.LandscapeLeft ||
                //      Input.deviceOrientation == DeviceOrientation.LandscapeRight) &&
                //     m_orientation == DeviceOrientation.Portrait))
                //{
                //    mainCamera.aspect = 1f / mainCamera.aspect;
                //}
                m_orientation = Input.deviceOrientation;
            }
        }

        //!
        //! Function for increasing and resetting the time variable.
        //!
        private void updateTime()
        {
            timeEvent?.Invoke(this, EventArgs.Empty);
            m_time = (m_time > (m_timesteps - 2) ? (byte)0 : m_time += 1);
        }

        //!
        //! Function that triggers the Tracer to Datahub ping messages.
        //!
        private void pingDataHub()
        {
            syncEvent?.Invoke(this, (byte)m_time);
        }

        //!
        //! Function to save the modules- and _core settins to disk.
        //!
        private void SaveSettings()
        {
            foreach (Manager manager in getManagers())
                if (manager._settings != null)
                    Save(Application.persistentDataPath, manager._settings);

            Save(Application.persistentDataPath, _settings);
        }

        //!
        //! Function to load the modules- and _core settins from disk.
        //!
        private void LoadSettings()
        {
            foreach (Manager manager in getManagers())
                if (manager._settings != null)
                    Load(Application.persistentDataPath, ref manager._settings);

            Load(Application.persistentDataPath, ref _settings);
        }

        //!
        //! Function to serialize settings and write it to disk.
        //!
        internal void Save(string path, Settings settings)
        {
            string filepath = Path.Combine(path, settings.GetType().ToString() + ".cfg");
            File.WriteAllText(filepath, JsonUtility.ToJson(settings));
            Helpers.Log("Settings saved to: " + filepath);
        }

        //!
        //! Function to read settings from disk and deserialze it to a Settings class.
        //!
        internal void Load(string path, ref Settings settings)
        {
            string filepath = Path.Combine(path, settings.GetType() + ".cfg");
            if (File.Exists(filepath))
                settings = (Settings)JsonUtility.FromJson(File.ReadAllText(filepath), settings.GetType());
        }

        //!
        //! Function for adding parameter objects to the prameter object list.
        //!
        //! @parameterObject The parameter object to be added to the parameter object list.
        //!
        internal void addParameterObject(ParameterObject parameterObject)
        {
            byte sceneID = parameterObject._sceneID;
            short poID = parameterObject._id;
            Dictionary<short, ParameterObject> sceneObjects;

            // check scene
            if (!m_parameterObjectList.TryGetValue(sceneID, out sceneObjects))
            {
                sceneObjects = new Dictionary<short, ParameterObject>();
                m_parameterObjectList.Add(sceneID, sceneObjects);
            }

            // check ParameterObject
            if (!sceneObjects.TryAdd(poID, parameterObject))
                Helpers.Log("Parameter object List in scene ID: " + sceneID.ToString() + " already contains the Parameter Object.", Helpers.logMsgType.WARNING);
        }

        internal void removeParameterObject(ParameterObject parameterObject)
        {
            byte sceneID = parameterObject._sceneID;
            short poID = parameterObject._id;
            Dictionary<short, ParameterObject> sceneObjects;

            // check scene
            if (!m_parameterObjectList.TryGetValue(sceneID, out sceneObjects))
            {
                Helpers.Log("Deletion of parameterObject (Scene: " + sceneID + ") not possible, object cannot be found in Dictionary!", Helpers.logMsgType.WARNING);
            }
            // check ParameterObject
            else if (!sceneObjects.Remove(poID))
                Helpers.Log("Deletion of parameterObject (ID: " + poID + ") not possible, object cannot be found in Dictionary!", Helpers.logMsgType.WARNING);
        }

        //!
        //! Function that returns a parameter object based in the given scene and object ID.
        //!
        //! @param poID The ID of the parameter object to be returned.
        //! @param sceneID The ID of the scene containing the parameter object to be returned.
        //! @return The corresponding parameter object to the gevien IDs.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ParameterObject getParameterObject(byte sceneID, short poID)
        {
            if (poID < 1 || sceneID < 0)
                return null;
            else
            {
                Dictionary<short, ParameterObject> sceneObjects;
                if (m_parameterObjectList.TryGetValue(sceneID, out sceneObjects))
                    return sceneObjects[poID];
                else
                    return null;
            }
        }

        //!
        //! Function that returns a list containing all parameter objects.
        //!
        //! @return The list containing all parameter objects.
        //!
        public List<ParameterObject> getAllParameterObjects()
        {
            List<ParameterObject> returnvalue = new List<ParameterObject>();

            foreach (Dictionary<short, ParameterObject> dict in m_parameterObjectList.Values)
            {
                foreach (ParameterObject parameterObject in dict.Values)
                {
                    returnvalue.Add(parameterObject);
                }
            }
            return returnvalue;
        }
    }
}