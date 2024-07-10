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

//! @file "SceneStorageModule.cs"
//! @brief implementation of TRACER scene I/O
//! @author Simon Spielmann
//! @version 0
//! @date 16.08.2022

using UnityEngine;
using System;
using System.Collections;
using System.IO;
using UnityEngine.Networking;

namespace tracer
{
    //!
    //! Implementation if TRACER scene I/O
    //!
    public class SceneStorageModule : SceneManagerModule
    {
        public event EventHandler<EventArgs> sceneLoaded;
        private MenuTree m_menu;

        //!
        //! constructor
        //! @param name The name of this module.
        //! @param Manager The manager (SceneManager) of this module.
        //!
        public SceneStorageModule(string name, Manager manager) : base(name, manager) { }

        //!
        //! Init and setup of the module and it's UI.
        //!
        protected override void Start(object sender, EventArgs e)
        {
            manager.loadDemoSceneUsingQr += QrSceneLoad;
            base.Start(sender, e);

            Parameter<Action> loadButton = new Parameter<Action>(LoadScene, "Load");
            Parameter<Action> saveButton = new Parameter<Action>(SaveScene, "Save");
            Parameter<Action> loadDemoButton = new Parameter<Action>(LoadDemoScene, "Load Demo");

            m_menu = new MenuTree()
              .Begin(MenuItem.IType.VSPLIT)
                   .Begin(MenuItem.IType.HSPLIT)
                       .Add("Scene name: ")
                       .Add(manager.settings.sceneFilepath)
                   .End()
                   .Begin(MenuItem.IType.HSPLIT)
                       .Add(loadButton)
                       .Add(saveButton)
                   .End()
                   .Begin(MenuItem.IType.HSPLIT)
                       .Add(loadDemoButton)
                   .End()
             .End();

            m_menu.caption = "Load/Save";
            m_menu.iconResourceLocation = "Images/button_save";
            UIManager uiManager = core.getManager<UIManager>();
            uiManager.addMenu(m_menu);

            // add elements to start menu
            uiManager.startMenu
                .Begin(MenuItem.IType.HSPLIT)
                    .Add(loadDemoButton)
                .End();

            //uiManager.showMenu(m_menu);
        }

        private void QrSceneLoad(object sender,string sceneName)
        {
            if (sceneName.Equals("Demo"))
            {
                LoadDemoScene();
                return;
            }
            
            LoadHTTPScene(sceneName);
        }

        //!
        //! Function that determines the current scene filepath and calls the save function.
        //!
        private void SaveScene()
        {
            SaveScene(manager.settings.sceneFilepath.value);
            core.getManager<UIManager>().hideMenu();
        }

        //!
        //! Function that determines the current scene filepath and calls the load function.
        //!
        private void LoadScene()
        {
            LoadScene(manager.settings.sceneFilepath.value);
            core.getManager<UIManager>().hideMenu();
        }

        //!
        //! Function that parses the current scene, and stores it to the persistent data path under the given name.
        //!
        //! @param sceneName The name under which the scene files will be stored.
        //!
        public void SaveScene(string sceneName)
        {
            SceneParserModule sceneParserModule = manager.getModule<SceneParserModule>();
            if (sceneParserModule != null)
            {
                sceneParserModule.ParseScene(true, false, true, false);

                if (manager.sceneDataHandler.headerByteDataRef != null)
                    File.WriteAllBytes(Path.Combine(Application.persistentDataPath, sceneName + ".header"), manager.sceneDataHandler.headerByteDataRef);
                if (manager.sceneDataHandler.nodesByteDataRef != null)
                    File.WriteAllBytes(Path.Combine(Application.persistentDataPath, sceneName + ".nodes"), manager.sceneDataHandler.nodesByteDataRef);
                if (manager.sceneDataHandler.objectsByteDataRef != null)
                    File.WriteAllBytes(Path.Combine(Application.persistentDataPath, sceneName + ".objects"), manager.sceneDataHandler.objectsByteDataRef);
                if (manager.sceneDataHandler.characterByteDataRef != null)
                    File.WriteAllBytes(Path.Combine(Application.persistentDataPath, sceneName + ".characters"), manager.sceneDataHandler.characterByteDataRef);
                if (manager.sceneDataHandler.texturesByteDataRef != null)
                    File.WriteAllBytes(Path.Combine(Application.persistentDataPath, sceneName + ".textures"), manager.sceneDataHandler.texturesByteDataRef);
                if (manager.sceneDataHandler.materialsByteDataRef != null)
                    File.WriteAllBytes(Path.Combine(Application.persistentDataPath, sceneName + ".materials"), manager.sceneDataHandler.materialsByteDataRef);

                Helpers.Log("Scene saved to " + Application.persistentDataPath);

                manager.sceneDataHandler.clearSceneByteData();
            }
        }

        //!
        //! Function that loads and creates the scene stored with the given from the persistent data path.
        //!
        //! @param sceneName The name of the scene to be loaded.
        //!
        public void LoadScene(string sceneName)
        {
            if (manager.sceneDataHandler != null)
            {
                string filepath = Path.Combine(Application.persistentDataPath, sceneName + ".header");
                if (File.Exists(filepath))
                    manager.sceneDataHandler.headerByteData = File.ReadAllBytes(filepath);

                filepath = Path.Combine(Application.persistentDataPath, sceneName + ".nodes");
                if (File.Exists(filepath))
                    manager.sceneDataHandler.nodesByteData = File.ReadAllBytes(filepath);

                filepath = Path.Combine(Application.persistentDataPath, sceneName + ".objects");
                if (File.Exists(filepath))
                    manager.sceneDataHandler.objectsByteData = File.ReadAllBytes(filepath);

                filepath = Path.Combine(Application.persistentDataPath, sceneName + ".characters");
                if (File.Exists(filepath))
                    manager.sceneDataHandler.characterByteData = File.ReadAllBytes(filepath);

                filepath = Path.Combine(Application.persistentDataPath, sceneName + ".textures");
                if (File.Exists(filepath))
                    manager.sceneDataHandler.texturesByteData = File.ReadAllBytes(filepath);

                filepath = Path.Combine(Application.persistentDataPath, sceneName + ".materials");
                if (File.Exists(filepath))
                    manager.sceneDataHandler.materialsByteData = File.ReadAllBytes(filepath);

                sceneLoaded?.Invoke(this, EventArgs.Empty);
            }
        }

        //!
        //! Function that starts the coroutine to load and create the scene stored with the given from the persistent data path.
        //!
        //! @param sceneName The name of the scene to be loaded.
        //!
        public void LoadDemoScene()
        {
            if (manager.sceneDataHandler != null)
                core.StartCoroutine(LoadDemoCoroutine());
        }

        //!
        //! Coproutine that loads and creates the scene stored with the given from the persistent data path.
        //!
        private IEnumerator LoadDemoCoroutine()
        {
            Dialog statusDialog = new Dialog();
            UIManager UImanager = core.getManager<UIManager>();
            UImanager.showDialog(statusDialog);
            
            core.getManager<UIManager>().hideMenu();

            statusDialog.caption = "Load Header";
            yield return null;
            manager.sceneDataHandler.headerByteData = (Resources.Load("Storage/VPETDemoSceneHeader") as TextAsset).bytes;
            statusDialog.progress += 14;

            statusDialog.caption = "Load Scene Nodes";
            yield return null;
            manager.sceneDataHandler.nodesByteData = (Resources.Load("Storage/VPETDemoSceneNodes") as TextAsset).bytes;
            statusDialog.progress += 14;

            statusDialog.caption = "Load Scene Objects";
            yield return null;
            manager.sceneDataHandler.objectsByteData = (Resources.Load("Storage/VPETDemoSceneObjects") as TextAsset).bytes;
            statusDialog.progress += 14;

            statusDialog.caption = "Load Characters";
            yield return null;
            manager.sceneDataHandler.characterByteData = (Resources.Load("Storage/VPETDemoSceneCharacters") as TextAsset).bytes;
            statusDialog.progress += 14;

            statusDialog.caption = "Load Textures";
            yield return null;
            manager.sceneDataHandler.texturesByteData = (Resources.Load("Storage/VPETDemoSceneTextures") as TextAsset).bytes;
            statusDialog.progress += 14;

            statusDialog.caption = "Load Materials";
            yield return null;
            manager.sceneDataHandler.materialsByteData = (Resources.Load("Storage/VPETDemoSceneMaterials") as TextAsset).bytes;
            statusDialog.progress += 14;

            statusDialog.caption = "Build Scene";
            yield return null;
            sceneLoaded?.Invoke(this, EventArgs.Empty);
            statusDialog.progress += 14;

            UImanager.showDialog(null);   
        }
        
        //!
        //! Function that starts the coroutine to load and create the scene stored on an http space
        //!
        //! @param url URL to the scene (without package name endings)
        //!
        public void LoadHTTPScene(string url)
        {
            if (manager.sceneDataHandler != null)
                core.StartCoroutine(LoadHTTPCoroutine(url));
        }
        
        //!
        //! Coproutine that loads and creates the scene stored with the given from the persistent data path.
        //!
        private IEnumerator LoadHTTPCoroutine(string url = "")
        {
            Dialog statusDialog = new Dialog();
            UIManager UImanager = core.getManager<UIManager>();
            UImanager.showDialog(statusDialog);

            core.getManager<UIManager>().hideMenu();

            string[] urls = new string[6];
            urls[0] = "header";
            urls[1] = "nodes";
            urls[2] = "objects";
            urls[3] = "characters";
            urls[4] = "textures";
            urls[5] = "materials";

            for (int i = 0; i < urls.Length; i++)
            {
                statusDialog.caption = "Trying HTTP receive";
                using (UnityWebRequest webRequest = UnityWebRequest.Get(url + "." + urls[i]))
                {
                    // Send the request and wait for the response
                    yield return webRequest.SendWebRequest();

                    if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
                    {
                        statusDialog.caption = "Error: " + webRequest.error;
                    }
                    else
                    {
                        statusDialog.caption = "Loading package " + i;
                        yield return null;
                        switch (i)
                        {
                            case 0:
                                manager.sceneDataHandler.headerByteData = webRequest.downloadHandler.data;
                                break;
                            case 1:
                                manager.sceneDataHandler.nodesByteData = webRequest.downloadHandler.data;
                                break;
                            case 2:
                                manager.sceneDataHandler.objectsByteData = webRequest.downloadHandler.data;
                                break;
                            case 3:
                                manager.sceneDataHandler.characterByteData = webRequest.downloadHandler.data;
                                break;
                            case 4:
                                manager.sceneDataHandler.texturesByteData = webRequest.downloadHandler.data;
                                break;
                            case 5:
                                manager.sceneDataHandler.materialsByteData = webRequest.downloadHandler.data;
                                break;
                            default:
                                break;
                        }
                        statusDialog.progress += 14;
                    }
                }
            }
            sceneLoaded?.Invoke(this, EventArgs.Empty);
            UImanager.showDialog(null);
        }

    }

}
