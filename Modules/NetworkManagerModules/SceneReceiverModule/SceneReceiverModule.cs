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
//! @file "SceneReceiverModule.cs"
//! @brief Implementation of the scene receiver module, sending scene requests and receives scene data. 
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 25.06.2021

using System.Collections.Generic;
using System.Collections;
using System;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;
using UnityEngine;

namespace tracer
{
    //!
    //! The scene receiver module, sending scene requests and receives scene data.
    //!
    public class SceneReceiverModule : NetworkManagerModule
    {
        //!
        //! The list of request the reqester uses to request the packages.
        //!
        private List<string> m_requests;

        //!
        //! The menu for the network configuration.
        //!
        private MenuTree m_menu;

        //!
        //! The scene receive progress.
        //!
        private int m_loadProgress = 0;

        //!
        //! Constructor
        //!
        //! @param  name  The  name of the module.
        //! @param _core A reference to the TRACER _core.
        //!
        public SceneReceiverModule(string name, Manager manager) : base(name, manager)
        {
            if (core.isServer)
                load = false;
        }

        //! 
        //!  Function called when an Unity Awake() m_callback is triggered
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Init(object sender, EventArgs e)
        {

        }

        //! 
        //! Function called when an Unity Start() m_callback is triggered
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Start(object sender, EventArgs e)
        {
            manager.connectUsingQrCode += ReceiveSceneUsingQr;
            Parameter<Action> button = new Parameter<Action>(Connect, "Connect");

            List<AbstractParameter> parameterList1 = new List<AbstractParameter>
            {
                new Parameter<string>(null, "Server"),
                new Parameter<string>(null, "Device")
            };

            m_menu = new MenuTree()
                .Begin(MenuItem.IType.VSPLIT)
                    .Begin(MenuItem.IType.HSPLIT)
                         .Add("Scene Source")
                         .Add(new ListParameter(parameterList1, "Device"))
                     .End()
                     .Begin(MenuItem.IType.HSPLIT)
                         .Add("ID Address")
                         .Add(manager.settings.ipAddress)
                     .End()
                     .Begin(MenuItem.IType.HSPLIT)
                         .Add(button)
                     .End()
                .End();

            m_menu.iconResourceLocation = "Images/button_network";
            m_menu.caption = "Client Network Settings";
            UIManager uiManager = core.getManager<UIManager>();
            uiManager.addMenu(m_menu);

            // add elements to start menu;
            uiManager.startMenu
                .Begin(MenuItem.IType.HSPLIT)
                    .Add("ID Address")
                    .Add(manager.settings.ipAddress)
                .End()
                .Begin(MenuItem.IType.HSPLIT)
                     .Add(button)
                .End();

            //_core.getManager<UIManager>().showMenu(m_menu);

            manager.requestSceneReceive += Connect;
        }

        private void Connect(object sender, EventArgs e)
        {
            //receiveScene(manager.settings.ipAddress.value, "5555", false);
            Connect();
        }

        private void Connect()
        {
            Helpers.Log(manager.settings.ipAddress.value);

            core.getManager<UIManager>().hideMenu();

            receiveScene(manager.settings.ipAddress.value, "5555", true);
        }

        //!
        //! Function that overrides the default start function.
        //! Because of Unity's single threded design we have to 
        //! split the work within a coroutine.
        //!
        //! @param ip ID address of the network interface.
        //! @param port Port number to be used.
        //!
        protected void start(string ip, string port, bool emitSceneReady)
        {
            m_ip = ip;
            m_port = port;

            core.StartCoroutine(startReceive(emitSceneReady));
        }

        //!
        //! Coroutine that creates a new thread receiving the scene data
        //! and yielding to allow the main thread to update the statusDialog.
        //!
        private IEnumerator startReceive(bool emitSceneReady)
        {
            Dialog statusDialog = new Dialog("Receive Scene", "", Dialog.DTypes.BAR);
            statusDialog.destroyEvent = stop;
            UIManager uiManager = core.getManager<UIManager>();
            uiManager.showDialog(statusDialog);

            ThreadStart transeiver = new ThreadStart(run);
            m_transceiverThread = new Thread(transeiver);
            m_transceiverThread.Start();
            NetworkManager.threadCount++;

            while (m_transceiverThread.IsAlive)
            {
                statusDialog.progress = m_loadProgress;
                yield return new WaitForSeconds(0.5f);
            }

            m_transceiverThread = null;
            NetworkManager.threadCount--;
            stop();

            SceneManager sceneManager = core.getManager<SceneManager>();
            // emit sceneReceived signal to trigger scene cration in the sceneCreator module
            if (sceneManager.sceneDataHandler.headerByteDataRef != null)
            {
                sceneManager.emitSceneNew(emitSceneReady);
            }

            uiManager.showDialog(null);
        }

        //!
        //! Function, requesting scene packages and receiving package data (executed in separate thread).
        //! As soon as all requested packages are received, a signal is emited that triggers the scene cration.
        //!
        protected override void run()
        {
            AsyncIO.ForceDotNet.Force();
            RequestSocket sceneReceiver = new RequestSocket();
            m_socket = sceneReceiver;

            SceneManager sceneManager = core.getManager<SceneManager>();
            sceneReceiver.Connect("tcp://" + m_ip + ":" + m_port);
            Helpers.Log("Scene receiver started: " + "tcp://" + m_ip + ":" + m_port);
            SceneManager.SceneDataHandler sceneDataHandler = sceneManager.sceneDataHandler;

            // because no loop we set m_threadEnded already at the beginning
            m_thredEnded.TrySetResult(true);

            try
            {
                foreach (string request in m_requests)
                {
                    sceneReceiver.SendFrame(request);
                    switch (request)
                    {
                        case "header":
                            sceneDataHandler.headerByteData = sceneReceiver.ReceiveFrameBytes();
                            Debug.Log(request + " received with " + sceneDataHandler.headerByteData.Length + " bytes.");
                            m_loadProgress += 10;
                            break;
                        case "nodes":
                            sceneDataHandler.nodesByteData = sceneReceiver.ReceiveFrameBytes();
                            Debug.Log(request + " received with " + sceneDataHandler.nodesByteDataRef.Length + " bytes.");
                            m_loadProgress += 20;
                            break;
                        case "parameterobjects":
                            sceneDataHandler.parameterObjectsByteData = sceneReceiver.ReceiveFrameBytes();
                            Debug.Log(request + " received with " + sceneDataHandler.parameterObjectsByteDataRef.Length + " bytes.");
                            m_loadProgress += 10;
                            break;
                        case "objects":
                            sceneDataHandler.objectsByteData = sceneReceiver.ReceiveFrameBytes();
                            Debug.Log(request + " received with " + sceneDataHandler.objectsByteDataRef.Length + " bytes.");
                            m_loadProgress += 20;
                            break;
                        case "characters":
                            sceneDataHandler.characterByteData = sceneReceiver.ReceiveFrameBytes();
                            Debug.Log(request + " received with " + sceneDataHandler.characterByteDataRef.Length + " bytes.");
                            m_loadProgress += 10;
                            break;
                        case "textures":
                            sceneDataHandler.texturesByteData = sceneReceiver.ReceiveFrameBytes();
                            Debug.Log(request + " received with " + sceneDataHandler.texturesByteDataRef.Length + " bytes.");
                            m_loadProgress += 20;
                            break;
                        case "materials":
                            sceneDataHandler.materialsByteData = sceneReceiver.ReceiveFrameBytes();
                            Debug.Log(request + " received with " + sceneDataHandler.materialsByteDataRef.Length + " bytes.");

                            m_loadProgress += 10;
                            break;
                    }
                }
            }
            catch (Exception e) { Helpers.Log("SceneReceiver " + e.ToString(), Helpers.logMsgType.WARNING); }
        }


        //! 
        //! Function that triggers the scene receiving process.
        //! @param ip The ID address to the server.
        //! @param port The port the server uses to send out the scene data.
        //! 
        public void receiveScene(string ip, string port, bool emitSceneReady)
        {
            m_requests = new List<string>() { "header", "nodes", "parameterobjects", "objects", "characters", "textures", "materials"  };
            start(ip, port, emitSceneReady);
        }

        public void ReceiveSceneUsingQr(object o, string ip)
        {
            core.getManager<UIManager>().hideMenu();
            receiveScene(ip, "5555", true);
        }
    }

}
