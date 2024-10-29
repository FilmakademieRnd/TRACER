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
        //! The event that is triggerd, when the scene has been received.
        //!
        public event EventHandler m_sceneReceived;

        //!
        //! The menu for the network configuration.
        //!
        private MenuTree m_menu;

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
                         .Add("IP Address")
                         .Add(manager.settings.ipAddress)
                     .End()
                     .Begin(MenuItem.IType.HSPLIT)
                         .Add(button)
                     .End()
                .End();

            m_menu.iconResourceLocation = "Images/button_network";
            m_menu.caption = "Network Settings";
            UIManager uiManager = core.getManager<UIManager>();
            uiManager.addMenu(m_menu);

            // add elements to start menu;
            uiManager.startMenu
                .Begin(MenuItem.IType.HSPLIT)
                    .Add("IP Address")
                    .Add(manager.settings.ipAddress)
                .End()
                .Begin(MenuItem.IType.HSPLIT)
                     .Add(button)
                .End();

            //_core.getManager<UIManager>().showMenu(m_menu);
        }

        private void Connect()
        {
            Helpers.Log(manager.settings.ipAddress.value);

            core.getManager<UIManager>().hideMenu();

            receiveScene(manager.settings.ipAddress.value, "5555");
        }

        //!
        //! Function that overrides the default start function.
        //! Because of Unity's single threded design we have to 
        //! split the work within a coroutine.
        //!
        //! @param ip IP address of the network interface.
        //! @param port Port number to be used.
        //!
        protected override void start(string ip, string port)
        {
            m_ip = ip;
            m_port = port;

            NetworkManager.threadCount++;

            core.StartCoroutine(startReceive());
        }

        //!
        //! Coroutine that creates a new thread receiving the scene data
        //! and yielding to allow the main thread to update the statusDialog.
        //!
        private IEnumerator startReceive()
        {
            Dialog statusDialog = new Dialog("Receive Scene", "", Dialog.DTypes.BAR);
            UIManager uiManager = core.getManager<UIManager>();
            uiManager.showDialog(statusDialog);

            Thread receiverThread = new Thread(run);
            receiverThread.Start();

            while (receiverThread.IsAlive)
            {
                yield return null;
                statusDialog.progress += 3;
            }

            // emit sceneReceived signal to trigger scene cration in the sceneCreator module
            if (core.getManager<SceneManager>().sceneDataHandler.headerByteDataRef != null)
                m_sceneReceived?.Invoke(this, EventArgs.Empty);

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
            SceneManager.SceneDataHandler sceneDataHandler = sceneManager.sceneDataHandler;

            try
            {
                foreach (string request in m_requests)
                {
                    sceneReceiver.SendFrame(request);
                    switch (request)
                    {
                        case "header":
                            sceneDataHandler.headerByteData = sceneReceiver.ReceiveFrameBytes();
                            break;
                        case "nodes":
                            sceneDataHandler.nodesByteData = sceneReceiver.ReceiveFrameBytes();
                            break;
                        case "parameterobjects":
                            sceneDataHandler.parameterObjectsByteData = sceneReceiver.ReceiveFrameBytes();
                            break;
                        case "objects":
                            sceneDataHandler.objectsByteData = sceneReceiver.ReceiveFrameBytes();
                            break;
                        case "characters":
                            sceneDataHandler.characterByteData = sceneReceiver.ReceiveFrameBytes();
                            break;
                        case "textures":
                            sceneDataHandler.texturesByteData = sceneReceiver.ReceiveFrameBytes();
                            break;
                        case "materials":
                            sceneDataHandler.materialsByteData = sceneReceiver.ReceiveFrameBytes();
                            break;
                    }
                }
            }
            catch { }
        }


        //! 
        //! Function that triggers the scene receiving process.
        //! @param ip The IP address to the server.
        //! @param port The port the server uses to send out the scene data.
        //! 
        public void receiveScene(string ip, string port)
        {
            m_requests = new List<string>() { "header", "nodes", "parameterobjects", "objects", "characters", "textures", "materials",  };
            start(ip, port);
        }

        public void ReceiveSceneUsingQr(object o, string ip)
        {
            core.getManager<UIManager>().hideMenu();
            receiveScene(ip, "5555");
        }
    }

}
