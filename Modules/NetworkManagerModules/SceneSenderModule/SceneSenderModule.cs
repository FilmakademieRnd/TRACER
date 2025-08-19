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

//! @file "SceneSenderModule.cs"
//! @brief Implementation of the scene sender module, listening to scene requests and sending scene data. 
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 11.03.2022

using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;
using NetMQ;
using NetMQ.Sockets;

namespace tracer
{
    //!
    //! Class implementing the scene sender module, listening to scene requests and sending scene data.
    //!
    public class SceneSenderModule : NetworkManagerModule
    {
        //!
        //! The menu for the network configuration.
        //!
        private MenuTree m_menu;
        
        //!
        //! A reference to the scene manager.
        //!
        private SceneManager m_sceneManager;

        //!
        //! Preloaded scene data split up into several packages for header, nodes, objects,
        //! characters, textures and materials.
        //!
        private Dictionary<string, byte[]> m_responses;

        //!
        //! Constructor
        //!
        //! @param  name  The  name of the module.
        //! @param _core A reference to the TRACER _core.
        //!
        public SceneSenderModule(string name, Manager manager) : base(name, manager)
        {
#if !UNITY_EDITOR
            if (!core.isServer)
                load = false;
#endif
        }

        //! 
        //!  Function called when an Unity Awake() m_callback is triggered
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Init(object sender, EventArgs e)
        {
            Parameter<Action> button = new Parameter<Action>(Connect, "Start");

            m_menu = new MenuTree()
               .Begin(MenuItem.IType.VSPLIT)
                    .Begin(MenuItem.IType.HSPLIT)
                        .Add("IP Address")
                        .Add(manager.settings.ipAddress)
                    .End()
                    .Begin(MenuItem.IType.HSPLIT)
                        .Add(button)
                    .End()
              .End();

            m_menu.caption = "Network Settings";
            m_menu.iconResourceLocation = "Images/button_network";
            core.getManager<UIManager>().addMenu(m_menu);

            m_sceneManager = core.getManager<SceneManager>();

            manager.requestSceneSend += SendScene;
            manager.stopSceneSend += StopSendScene;
        }

        //! 
        //! Function called when an Unity Start() m_callback is triggered
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Start(object sender, EventArgs e)
        {
            //_core.getManager<UIManager>().showMenu(m_menu);
        }

        //!
        //! Action called from the start Button, initializing the scene sender.
        //!
        private void SendScene(object sender, EventArgs e)
        {
            sendScene(manager.settings.ipAddress.value, "5555");
        }

        private void StopSendScene(object sender, EventArgs e)
        {
            stop();
            //m_isRunning = false;
        }

        //!
        //! Action called from the start Button, initializing the scene sender.
        //!
        private void Connect()
        {
            Helpers.Log(manager.settings.ipAddress.value);

            core.getManager<UIManager>().hideMenu();

            m_sceneManager.emitParseScene(true);

            sendScene(manager.settings.ipAddress.value, "5555");
        }

        //!
        //! Function, sending messages containing the scene data as reponces to the requested package (executed in separate thread).
        //!
        protected override void run()
        {
            m_isRunning = true;
            AsyncIO.ForceDotNet.Force();
            var dataSender = new ResponseSocket();
            m_socket = dataSender;

            dataSender.Bind("tcp://" + m_ip + ":" + m_port);
            Debug.Log("Enter while.. ");

            string message;

            while (m_isRunning)
            {
                dataSender.TryReceiveFrameString(out message);       // TryReceiveFrameString returns null if no message has been received!
                if (message != null)
                {
                    try
                    {
                        if (m_responses.ContainsKey(message))
                        {
                            dataSender.SendFrame(m_responses[message]);
                            Helpers.Log(message + " send bytes: " + m_responses[message].Length);
                        }
                        else
                            dataSender.SendFrame(new byte[0]);
                    }
                    catch { }

                }
                Thread.Sleep(100);
            }

            m_responses.Clear();
            m_sceneManager.sceneDataHandler.clearSceneByteData();
            
            m_thredEnded.TrySetResult(true);
        }

        //!
        //! Function to start the scene sender module.
        //! @param ip The IP address to be used from the sender.
        //! @param port The port number to be used from the sender.
        //!
        public void sendScene(string ip, string port)
        {
            m_responses = new Dictionary<string, byte[]>();
            SceneManager.SceneDataHandler dataHandler = m_sceneManager.sceneDataHandler;

            m_responses.Add("header", dataHandler.headerByteDataRef);
            m_responses.Add("nodes", dataHandler.nodesByteDataRef);
            m_responses.Add("parameterobjects", dataHandler.parameterObjectsByteDataRef);
            m_responses.Add("objects", dataHandler.objectsByteDataRef);
            m_responses.Add("characters", dataHandler.characterByteDataRef);
            m_responses.Add("textures", dataHandler.texturesByteDataRef);
            m_responses.Add("materials", dataHandler.materialsByteDataRef);

            start(ip, port);
        }
    }
}
