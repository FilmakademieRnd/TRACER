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

//! @file "SceneManager.cs"
//! @brief Scene Manager implementation.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 23.02.2021

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace tracer
{
    //!
    //! class managing all scene related aspects
    //!
    public partial class SceneManager : Manager
    {
        [Serializable]
        public class SceneManagerSettings : Settings
        {
            //!
            //! Do we load scene from dump file
            //!
            public bool loadSampleScene = true;

            //!
            //! Do we load textures
            //!
            public bool loadTextures = true;

            //!
            //! global scale of the scene
            //!
            public float sceneScale = 1f;

            //!
            //! The filepath for loading and storing a scene.
            //!
            public Parameter<string> sceneFilepath;
        }

        //!
        //! Cast for accessing the settings variable with the correct type.
        //!
        public SceneManagerSettings settings { get => (SceneManagerSettings)_settings; }

        //!
        //! The maximum extend of the scene
        //!
        public Vector3 sceneBoundsMax = Vector3.positiveInfinity;
        public Vector3 sceneBoundsMin = Vector3.negativeInfinity;
        public float maxExtend = 1f;

        //!
        //! Function that returns a list containing all scene objects.
        //!
        //! @return The list containing all scene objects.
        //!
        public List<SceneObject> getAllSceneObjects()
        {
            List<SceneObject> returnvalue = new List<SceneObject>();

            foreach (Dictionary<short, ParameterObject> dict in core.parameterObjectList.Values)
            {
                foreach (ParameterObject parameterObject in dict.Values)
                {
                    SceneObject sceneObject = parameterObject as SceneObject;
                    if (sceneObject)
                        returnvalue.Add((SceneObject) parameterObject);
                }
            }
            return returnvalue;
        }

        //!
        //! Function that returns a list containing all objects of a specific scene.
        //!
        //! @param The scne ID to define the scene to gather all scene objects from.
        //!
        //! @return The list containing all scene objects.
        //!
        public List<SceneObject> getAllSceneObjectsFromScene(byte sceneID)
        {
            List<SceneObject> returnvalue = new List<SceneObject>();

            foreach (ParameterObject parameterObject in core.parameterObjectList[sceneID].Values)
            {
                SceneObject sceneObject = parameterObject as SceneObject;
                if (sceneObject)
                    returnvalue.Add((SceneObject)parameterObject);
            }
            return returnvalue;
        }

        //!
        //! Function that returns a list containing all Dynamic Parameter Objects.
        //!
        //! @return The list containing all Dynamic Parameter Objects.
        //!
        public List<DynamicParameterObject> getAllDynamicParameterObjects()
        {
            List<DynamicParameterObject> returnvalue = new List<DynamicParameterObject>();

            foreach (Dictionary<short, ParameterObject> dict in core.parameterObjectList.Values)
            {
                foreach (ParameterObject parameterObject in dict.Values)
                {
                    DynamicParameterObject sceneObject = parameterObject as DynamicParameterObject;
                    if (sceneObject)
                        returnvalue.Add((DynamicParameterObject) parameterObject);
                }
            }
            return returnvalue;
        }

        //!
        //! Function that returns a list containing all animated scene objects.
        //!
        //! @return The list containing all animated scene objects.
        //!
        public List<SceneObject> getAllAnimatedSceneObjects()
        {
            List<SceneObject> returnvalue = new List<SceneObject>();

            foreach (Dictionary<short, ParameterObject> dict in core.parameterObjectList.Values)
            {
                foreach (ParameterObject parameterObject in dict.Values)
                {
                    SceneObject sceneObject = parameterObject as SceneObject;
                    if (sceneObject){
                        foreach(AbstractParameter apara in sceneObject.parameterList){
                            if(apara._isAnimated){
                                returnvalue.Add((SceneObject) parameterObject);
                                break;
                            }
                        }
                    }
                }
            }
            return returnvalue;
        }
        
        //!
        //! The list storing selectable Unity lights in scene.
        //!
        private List<SceneObjectLight> m_sceneLightList = new List<SceneObjectLight>();
        //!
        //! Setter and getter to List holding references to all editable TRACER sceneObjects.
        //!
        public List<SceneObjectLight> sceneLightList
        {
            get { return m_sceneLightList; }
            set { m_sceneLightList = value; }
        }

        //!
        //! The list storing Unity cameras in scene.
        //!
        private List<SceneObjectCamera> m_sceneCameraList = new List<SceneObjectCamera>();
        //!
        //! Setter and getter to List holding references to all editable TRACER sceneObjects.
        //!
        public List<SceneObjectCamera> sceneCameraList
        {
            get { return m_sceneCameraList; }
            set { m_sceneCameraList = value; }
        }
        
        //!
        //! The list storing scene object that are not lights or cameras.
        //!
        private List<SceneObject> m_simpleSceneObjectList = new List<SceneObject>();
        //!
        //! Setter and getter to List holding references to all scene objects that are not lights or cameras.
        //!
        public List<SceneObject> simpleSceneObjectList
        {
            get { return m_simpleSceneObjectList; }
            set { m_simpleSceneObjectList = value; }
        }

        //!
        //! A reference to the TRACER scene root.
        //!
        private GameObject m_scnRoot;

        //!
        //! The TRACER SceneDataHandler, handling all TRACER scene data relevant conversion.
        //!
        protected SceneDataHandler m_sceneDataHandler;

        //!
        //! Event emitted to start scene parsing.
        //!
        public event EventHandler<bool> parseScene;

        //!
        //! Event emitted when scene is prepared.
        //!
        public event EventHandler<EventArgs> sceneReady;

        //!
        //! Event emitted when a scene has been parsed.
        //!
        public event EventHandler<EventArgs> sceneParsed;

        //!
        //! Event emitted when a new scene has been received or loaded.
        //!
        public event EventHandler<bool> sceneNew;

        //!
        //! Event emitted when a scene has been created.
        //!
        public event EventHandler<EventArgs> sceneCreated;

        //!
        //! Event emitted when a scene has been reseted.
        //!
        public event EventHandler<EventArgs> sceneReset;

        //!
        //! Event emitted when a scene object has been locked.
        //!
        public event EventHandler<SceneObject> sceneObjectLocked;

        //!
        //! Event emitted when a scene object has been unlocked.
        //!
        public event EventHandler<SceneObject> sceneObjectUnlocked;

        public event EventHandler<string> loadDemoSceneUsingQr;

        public void InvokeQrLoadEvent(string loadScene)
        {
            loadDemoSceneUsingQr?.Invoke(this, loadScene);
        }

        //!
        //! Getter returning a reference to the TRACER scene root.
        //!
        //! @return A reference to the TRACER scene root.
        //!
        public ref GameObject scnRoot
        {
            get { return ref m_scnRoot; }
        }

        //!
        //! A reference to the TRACER SceneDataHandler.
        //!
        //! @return A reference to the TRACER SceneDataHandler.
        //!
        public ref SceneDataHandler sceneDataHandler
        {
            get { return ref m_sceneDataHandler; }
        }

        //!
        //! constructor
        //! @param  name    Name of the scene manager
        //! @param  moduleType  Type of module to add to this manager 
        //!
        public SceneManager(Type moduleType, Core tracerCore) : base(moduleType, tracerCore)
        {
            m_sceneDataHandler = new SceneDataHandler();
            settings.sceneFilepath = new Parameter<string>("VPETdefaultScene", "Filepath");

            // create scene _parent if not there
            scnRoot = GameObject.Find("Scene");
            if (scnRoot == null)
            {
                scnRoot = new GameObject("VPETScene");
            }
        }

        public void emitParseScene(bool emitSceneReady)
        {
            parseScene?.Invoke(this, emitSceneReady);
        }

        //!
        //! Function that emits the scene ready event. 
        //!
        public void emitSceneReady()
        {
            sceneReady?.Invoke(this, new EventArgs());
        }

        //!
        //! Function that emits the scene parsed event. 
        //!
        public void emitSceneParsed()
        {
            sceneParsed?.Invoke(this, new EventArgs());
        }

        //!
        //! Function that emits the scene created event. 
        //!
        public void emitSceneCreated()
        {
            sceneCreated?.Invoke(this, new EventArgs());
        }

        //!
        //! Function that emits the scene received or loaded event. 
        //!
        public void emitSceneNew(bool emitSceneReady)
        {
            sceneNew?.Invoke(this, emitSceneReady);
        }


        //!
        //! Function that returns a scne object based in the given ID.
        //!
        //! @param _id The ID of the scene object to be returned.
        //! @return The corresponding scene object to the gevien ID.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SceneObject getSceneObject(byte sceneID, short poID)
        {
            return (SceneObject) core.getParameterObject(sceneID, poID);
        }

        //!
        //! Function that deletes all Unity scene content and clears the TRACER scene object lists.
        //!
        public void ResetScene()
        {
            // remove all Unity GameObjects
            if (m_scnRoot != null)
            {
                for (int i=0; i<m_scnRoot.transform.childCount; i++)
                    GameObject.Destroy(m_scnRoot.transform.GetChild(i).gameObject);
            }

            // remove all Tracer SceneObjects
            List<SceneObject> sceneObjectList = getAllSceneObjects();
            foreach (SceneObject sceneObject in sceneObjectList)
                core.removeParameterObject(sceneObject);

            m_sceneCameraList.Clear();
            m_sceneLightList.Clear();
            m_simpleSceneObjectList.Clear();

            sceneReset?.Invoke(this, EventArgs.Empty);
        }

        public void ResetScene(byte sceneID)
        {
            // remove all Unity GameObjects
            if (m_scnRoot != null)
            {
                for (int i = 0; i < m_scnRoot.transform.childCount; i++)
                    GameObject.Destroy(m_scnRoot.transform.GetChild(i).gameObject);
            }

            // remove all Tracer SceneObjects
            List<SceneObject> sceneObjectList = getAllSceneObjectsFromScene(sceneID);
            foreach (SceneObject sceneObject in sceneObjectList)
            {
                GameObject go = sceneObject.gameObject;
                core.removeParameterObject(sceneObject);
                m_simpleSceneObjectList.Remove(sceneObject);
                if (sceneObject.GetType() == typeof(SceneObjectCamera))
                    m_sceneCameraList.Remove((SceneObjectCamera) sceneObject);
                else if (sceneObject.GetType() == typeof(SceneObjectLight))
                    m_sceneLightList.Remove((SceneObjectLight) sceneObject);
                if (go != null)
                    GameObject.Destroy(go);
                
            }

            sceneReset?.Invoke(this, EventArgs.Empty);
        }

        //!
        //! Function to lock a SceneObject.
        //!
        //! @param sceneObject The SceneObject to be locked.
        //!
        internal void LockSceneObject(SceneObject sceneObject)
        {
            sceneObjectLocked.Invoke(this, sceneObject);
        }

        //!
        //! Function to unlock a SceneObject.
        //!
        //! @param sceneObject The SceneObject to be unlocked.
        //!
        internal void UnlockSceneObject(SceneObject sceneObject)
        {
            sceneObjectUnlocked.Invoke(this, sceneObject);
        }

        //!
        //! Function that creates a screensupt based on the viw of the main camera.
        //!
        //! @param size The size for heigt and width in pixels for the screenshot.
        //! @return The JPG encoded screenshot as a byte array.
        //!
        public byte[] MakeScreenshotJPG(int size)
        {
            // get active camera
            Camera cam = Camera.main;

            // calculate the corresponding height based on the target size and the cameras aspect
            int height = Mathf.FloorToInt(size / cam.aspect);

            // create a squared size texture we can use to handle the data
            // and a temporary render texture with correct aspect to render the scene to
            Texture2D tex = new Texture2D(size, size);
            RenderTexture rt = RenderTexture.GetTemporary(size, height, 24);

            // fill texture with black pixels
            tex.SetPixels(Enumerable.Repeat(Color.black, size * size).ToArray());

            // render scene into the render target
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;

            // copy the rendered pixels to a texture (from GPU to CPU) and cleanup
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, size, height), 0, size / 2 - height / 2);
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return tex.EncodeToJPG(95);
        }
    }
}