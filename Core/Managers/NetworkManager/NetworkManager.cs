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

//! @file "NetworkManager.cs"
//! @brief Implementation of the network manager and netMQ sender/receiver.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 13.10.2021

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using NetMQ;

namespace tracer
{
    //!
    //! Class implementing the network manager and netMQ sender/receiver.
    //!
    public class NetworkManager : Manager
    {
        [Serializable]
        public class NetworkManagerSettings : Settings
        {
            [ShowInMenu]
            // to store a parameters value into the settings files.
            public Parameter<string> ipAddress;
        }

        //!
        //! number of threads
        //!
        public static int threadCount = 0;

        //!
        //! The average ping rtt from the client to the server.
        //!
        public int pingRTT = 0;

        //!
        //! number of disposed network worker threads
        //!
        private int m_disposeCount = 0;

        //!
        //! Event that is invoket to send an server command.
        //!
        public event EventHandler<byte[]> sendServerCommand;

        //!
        //! Event that is invoket to request a scene server.
        //!
        public event EventHandler<EventArgs> requestSceneSend;

        //!
        //! Event that is invoket to stop a scene server.
        //!
        public event EventHandler<EventArgs> stopSceneSend;

        //!
        //! Event that is invoket when a client has left the network session.
        //!
        public event EventHandler<byte> clientLost;
        
        //!
        //! Event that is invoket when a new client enters the network session.
        //!
        public event EventHandler<byte> clientRegistered;

        //!
        //! Event that is invoket when a new scene object has been added.
        //!
        public event EventHandler<SceneObject> sceneObjectAdded;

        //!
        //! Event that is invoket when a new scene object has been removed.
        //!
        public event EventHandler<SceneObject> sceneObjectRemoved;
        
        //!
        //! Event that is invoket when the app is startet with a qr code and an ip address.
        //!
        public event EventHandler<string> connectUsingQrCode;

        //!
        //! Cast for accessing the settings variable with the correct type.
        //!
        public NetworkManagerSettings settings { get => (NetworkManagerSettings)_settings; }

        //!
        //! A list containung the ID of all registered Tracer clients acting as server.
        //!
        private List<byte> m_serverList;
        
        //!
        //! Constructor initializing member variables.
        //!
        //! @param  moduleType  type of modules to be loaded by this manager
        //! @param tracerCore A reference to the TRACER _core.
        //!
        public NetworkManager(Type moduleType, Core tracerCore) : base(moduleType, tracerCore)
        {
            settings.ipAddress = new Parameter<string>("127.0.0.1", "ipAddress");

            // initialize the server list with the given server ID
            m_serverList = new List<byte>() { byte.Parse(settings.ipAddress.value.ToString().Split('.')[3]) };

            //reads the network name of the device
            var hostName = Dns.GetHostName();
            var host = Dns.GetHostEntry(hostName);

            //Take last ip adress of local network (which is local wlan ip address)
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    if (core.useRandomCID)
                    {
                        // prevent equal cIDs if server and client running on the same machine
                        m_cID = (byte)UnityEngine.Random.Range(2, 250);
                    }
                    else
                        m_cID = byte.Parse(ip.ToString().Split('.')[3]);
                }
            }
        }

        //! 
        //! Cleanup function called before Unity destroys the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Cleanup(object sender, EventArgs e)
        {
            base.Cleanup(sender, e);
        }

        //!
        //! Clean up the NetMQ COntext
        //!
        public void NetMQCleanup()
        {
            m_disposeCount++;
            if (m_disposeCount == threadCount)
            {
                try
                {
                    NetMQConfig.Cleanup(false);
                }
                catch { }
                finally
                {
                    Helpers.Log("netMQ cleaned up.");
                }
            }
        }

        //!
        //! Function to add a scene object to the network sync.
        //!
        //! @param sceneObject The scene object to be added to the network sync.
        //!
        public void AddSceneObject(SceneObject sceneObject)
        {
            sceneObjectAdded?.Invoke(this, sceneObject);
        }

        //!
        //! Function to remove a scene object to the network sync.
        //!
        //! @param sceneObject The scene object to be removed from the network sync.
        //!
        public void RemoveSceneObject(SceneObject sceneObject)
        {
            sceneObjectRemoved?.Invoke(this, sceneObject);
        }

        //!
        //! The ID if the client (based on the last digit of IP address)
        //!
        private byte m_cID = 254;
        public byte cID
        {
            get => m_cID;
        }

        //!
        //! Function to invoke client connection status updates.
        //!
        //! @param connectionStatus Wether a client has been connected or disconnected.
        //! @param clientID The ID of the client that has been connected or disconnected.
        //!
        public void ClientConnectionUpdate(bool connectionStatus, byte clientID, bool isServer)
        {
            if (connectionStatus)
            {
                clientRegistered?.Invoke(this, clientID);
                if (isServer)
                    m_serverList.Add(clientID);
            }
            else
            {
                clientLost?.Invoke(this, clientID);
                m_serverList.Remove(clientID);
            }
            
            UnityEngine.Debug.Log("ClientConnectionUpdate ID: " + clientID + " Status: " + connectionStatus.ToString());
        }

        public void SendServerCommand(byte[] command)
        {
            sendServerCommand?.Invoke(this, command);
        }

        public void RequestSceneSend()
        {
            requestSceneSend?.Invoke(this, EventArgs.Empty);
        }

        public void StopSceneSend()
        {
            stopSceneSend?.Invoke(this, EventArgs.Empty);
        }

        public void ConnectUsingQrCode(string ip)
        {
            connectUsingQrCode?.Invoke(this, ip);
        }
    }
}
