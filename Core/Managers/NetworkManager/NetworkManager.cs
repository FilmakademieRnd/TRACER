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

using NetMQ;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

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
            public Parameter<string> vID;
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
        //! Event that is invoket to send an server command.
        //!
        public event EventHandler<byte[]> sendServerCommand;

        //!
        //! Event that is invoket to start/restart the command server.
        //!
        public event EventHandler<EventArgs> requestCommandServer;

        //!
        //! Event that is invoket to request a scene receiver.
        //!
        public event EventHandler<EventArgs> requestSceneReceive;

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
        //! Watchdog used to create command buffer request/reply asynchrony.
        //! I also transfers the command reply as a byte array list.
        //!
        public TaskCompletionSource<List<byte[]>> m_commandBufferWritten { get; private set; } = null;

        //!
        //! Constructor initializing member variables.
        //!
        //! @param  moduleType  type of modules to be loaded by this manager
        //! @param tracerCore A reference to the TRACER _core.
        //!
        public NetworkManager(Type moduleType, Core tracerCore) : base(moduleType, tracerCore)
        {
            m_commandBufferWritten = new TaskCompletionSource<List<byte[]>>();
            settings.ipAddress = new Parameter<string>("127.0.0.1", "ipAddress");
            settings.vID = new Parameter<string>("000000", "vID");
        }

        //! 
        //! Virtual function called when Unity initializes the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Init(object sender, EventArgs e)
        {
            base.Init(sender, e);

            settings.ipAddress.hasChanged += startCommantServer;

            startCommantServer(this, "");
        }

        private void startCommantServer(object o, string ip)
        {
            requestCommandServer?.Invoke(this, EventArgs.Empty);
            determineClientID(o, ip);
        }

        private byte[] createVID()
        {
            int size = 6;
            byte[] data = new byte[size];
            
            if (settings.vID.value == "000000" || settings.vID.value == "")
            {
                string newVID = "";

                for (int i = 0; i < size; i++)
                {
                    data[i] = (byte) UnityEngine.Random.Range(0, 255);
                    newVID += (char) data[i];
                }
                
                settings.vID.value = newVID;
            }
            else
            {
                for (int i = 0; i < size; i++)
                {
                    data[i] = (byte) settings.vID.value[i];
                }
            }
            
            return data;
        }

        private async void determineClientID(object o, string startIP)
        {
            // initialize the server list with the given server ID
            string ipString = settings.ipAddress.value.ToString();
            string[] ips = ipString.Split('.');

            m_serverList = new List<byte>() { byte.Parse(ipString.Split('.')[3]) };

            if (core.useRandomCID)
            {
                // prevent equal cIDs if server and client running on the same machine
                m_cID = (byte)UnityEngine.Random.Range(2, 250);
            }

            else
            {
                m_cID = 254;
                //reads the network name of the device
                //var hostName = Dns.GetHostName();
                //var host = Dns.GetHostEntry(hostName);

#if UNITY_IOS || UNITY_ANDROID
                byte[] mac = createVID();
                Helpers.Log("Requesting ID for MAC: " + BitConverter.ToString(mac));

                List<byte[]> responses = await SendServerCommand(
                                   new byte[] { (byte)NetworkManagerModule.DataHubMessageType.ID, mac[0], mac[1], mac[2], mac[3], mac[4], mac[5] },
                                   2f);
                
                //fallback if no correct response or 255(ip aready taken)
                if (responses.Count > 0 && responses[0][0] != 255)
                {
                    m_cID = responses[0][0];
                    Helpers.Log("Got ID from DataHub. vID is: " + m_cID, Helpers.logMsgType.NONE);
                }
#else

                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if ((ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) && ni.OperationalStatus == OperationalStatus.Up)
                    {
                        foreach (var uni in ni.GetIPProperties().UnicastAddresses)
                        {
                            IPAddress ipAddress = uni.Address;

                            if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                byte[] mac = ni.GetPhysicalAddress().GetAddressBytes();
                                Helpers.Log("Requesting ID for IP: "+ ipAddress.ToString() + " at MAC: " + ni.GetPhysicalAddress().ToString());
                                List<byte[]> responses = await SendServerCommand(
                                    new byte[] { (byte)NetworkManagerModule.DataHubMessageType.ID, mac[0], mac[1], mac[2], mac[3], mac[4], mac[5] },
                                    2f);

                                //fallback if no correct response or 255(ip aready taken)
                                if (responses.Count > 0 && responses[0][0] != 255)
                                {
                                    m_cID = responses[0][0];
                                    Helpers.Log("Got ID from DataHub. vID is: " + m_cID, Helpers.logMsgType.NONE);
                                    core.StartSync();
                                    return;
                                }
                            }
                        }
                    }
                }
#endif
            }
            Helpers.Log("Set vID to: " + m_cID);
            
            if (core != null)
                core.StartSync();

        }


        //! 
        //! Cleanup function called before Unity destroys the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        public override void Cleanup()
        {
            base.Cleanup();
            NetMQCleanup();
        }

        //!
        //! Clean up the NetMQ COntext
        //!
        public void NetMQCleanup()
        {
            if (threadCount == 0)
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
            else Helpers.Log("netMQ cleanup error! Thread count is: " + threadCount, Helpers.logMsgType.ERROR);
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

        //! 
        //! Function to send commands to DataHub.
        //! 
        //! @param command The command as a byte array to be send to DataHub.
        //! @return 
        public async Task<List<byte[]>> SendServerCommand(byte[] command, float timeout = 5)
        {
            // enqueue new TCS to for handling message response tasks
            m_commandBufferWritten = new TaskCompletionSource<List<byte[]>>(TaskContinuationOptions.RunContinuationsAsynchronously);
            
            // send command
            sendServerCommand?.Invoke(this, command);

            // wait up to 'timeout' sconds for reply 
            Task t = await Task.WhenAny(m_commandBufferWritten.Task, Task.Delay(TimeSpan.FromSeconds(timeout)) );

            if (t.GetType() == typeof(Task<List<byte[]>>))
            {
                List<byte[]> result = m_commandBufferWritten.Task.Result;
                return result.GetRange(1, result.Count-1);
            }
            else
            {
                Helpers.Log("DataHub timed out, command not send.", Helpers.logMsgType.WARNING);
                return new List<byte[]>();
            }
        }

        //!
        //! Function to request a new scene receive at DataHub.
        //!
        public void RequestSceneReceive()
        {
            requestSceneReceive?.Invoke(this, EventArgs.Empty);
        }

        //!
        //! Function to request a scene from DataHub.
        //!
        public void RequestSceneSend()
        {
            requestSceneSend?.Invoke(this, EventArgs.Empty);
        }

        //!
        //! Function for requesting a scene via QR code.
        //!
        public void ConnectUsingQrCode(string ip)
        {
            connectUsingQrCode?.Invoke(this, ip);
            settings.ipAddress.setValue(ip);
        }
    }
}
