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

//! @file "CommandModule.cs"
//! @brief Implementation of the update sender module, sending parameter updates to clients.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 28.10.2021

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;
using System.Threading;
using UnityEngine;
using System.Linq;
using System.Threading.Tasks;

namespace tracer
{
    //!
    //! Class implementing the command module, sending  and receiving commands.
    //!
    public class CommandModule : NetworkManagerModule
    {
        //!
        //! Start time for messuring the ping round trip time.
        //!
        private byte m_pingStartTime = 0;

        //!
        //! A Queue containung the last 5 ping RTT's;
        //!
        private Queue<byte> m_pingTimes = null;
        
        //!
        //! Array of command requests to be send.
        //!
        private byte[] m_commandRequest = null;

        //!
        //! Constructor
        //!
        //! @param  name  The  name of the module.
        //! @param _core A reference to the TRACER _core.
        //!
        public CommandModule(string name, Manager manager) : base(name, manager)
        {
        }

        //!
        //! Function for custom initialisation.
        //! 
        //! @param sender The TRACER _core.
        //! @param e The pssed event arguments.
        //! 
        protected override void Init(object sender, EventArgs e)
        {
            m_pingTimes = new Queue<byte>(new byte[] { 0, 0, 0, 0, 0 });
            core.syncEvent += queuePingMessage;
            manager.sendServerCommand += queueCommandMessage;
            manager.requestCommandServer += connectAndStart;
        }

        //!
        //! Destructor, cleaning up event registrations. 
        //!
        public override void Dispose()
        {
            base.Dispose();
            SceneManager sceneManager = core.getManager<SceneManager>();

            core.syncEvent -= queuePingMessage;
            manager.sendServerCommand -= queueCommandMessage;
            manager.requestCommandServer -= connectAndStart;
        }

        //!
        //! Function that connects the scene object change events for parameter queuing.
        //!
        //! @param sender The SceneManager.
        //! @param e The pssed event arguments.
        //!
        private void connectAndStart(object sender, EventArgs e)
        {
            start(manager.settings.ipAddress.value, "5558");
        }

        //!
        //! Function that creates a command responses for sending.
        //!
        //! @param sender The TRACER core.
        //! @param time The clients global time.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void queueCommandMessage(object sender, byte[] command)
        {
            lock (m_lock)
            {
                m_commandRequest = new byte[2 + command.Length];
                // header
                m_commandRequest[0] = manager.cID;
                m_commandRequest[1] = core.time;
                command.CopyTo(m_commandRequest.AsSpan().Slice(2));
            }

            m_mre.Set();
        }

        //!
        //! Function that creates a ping responses for sending.
        //!
        //! @param sender The TRACER _core.
        //! @param time The clients global time.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void queuePingMessage(object sender, byte time)
        {
            if (m_commandRequest == null)
            {
                m_commandRequest = new byte[4];

                m_pingStartTime = time;

                lock (m_lock)
                {
                    // header
                    m_commandRequest[0] = manager.cID;
                    m_commandRequest[1] = time;
                    m_commandRequest[2] = (byte)DataHubMessageType.PING;
                    m_commandRequest[3] = Convert.ToByte(core.isServer);
                }

            }
            m_mre.Set();
        }

        //! 
        //! Function that decodes a sync responses and set the clients global time.
        //!
        //! @param responses The responses to be decoded.
        //! 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void decodePongMessage(byte[] message)
        {
            byte rtt = (byte)Helpers.DeltaTime(core.time, m_pingStartTime, core.timesteps);
            int pingCount = m_pingTimes.Count;
            int rttSum = 0;

            if (pingCount > 4)
                m_pingTimes.Dequeue();

            m_pingTimes.Enqueue(rtt);

            byte[] rtts = m_pingTimes.ToArray();
            byte rttMax = 0;
            for (int i = 0; i < pingCount; i++)
            {
                byte curr = rtts[i];
                if (rttMax < curr) rttMax = curr;
                rttSum += curr;
            }

            lock (manager)
            {
                manager.pingRTT = Mathf.RoundToInt((rttSum - rttMax) / (float)(pingCount - 1));
                //manager.pingRTT = rtt;
            }

            //Debug.Log("Pong received! RTT: " + rtt);
        }

        //! 
        //! Function that decodes file info response message list.
        //! The responses are formated as a list of bytes.
        //!
        //! @param responses The responses to be decoded a alist of bytes.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void decodeReplyMessage(List<byte[]> responses)
        {
            lock (m_lock)
            {
                    manager.m_commandBufferWritten.TrySetResult(responses.ConvertAll(x => x.ToArray()));
            }

            // just for debugging...
            //switch ((DataHubMessageType) responses[0][2])
            //{
            //    case DataHubMessageType.FILEINFO:
            //        for (int i = 1; i < responses.Count; i++)
            //            Helpers.Log(System.Text.Encoding.UTF8.GetString(responses[i]));
            //        break;
            //    default:
            //        break;
            //}
        }

        //!
        //! Function, sending control messages and parameter update messages (executed in separate thread).
        //! Thread execution is locked after every loop and unlocked by sendParameterMessages every global tick.
        //!
        //!
        //! Buffer for the responses received by a single transceive call, reused to
        //! avoid an allocation per message.
        //!
        private List<byte[]> m_responses = new List<byte[]>();

        //!
        //! Function creating and connecting the request socket of the command channel.
        //!
        protected override void createSocket()
        {
            m_socket = TracerTransport.current.CreateSocket(TracerSocketType.Request);

            string address = TracerTransport.endpoint(m_ip, m_port);
            m_socket.Connect(address);
            Helpers.Log("Command Module connected: " + address);
        }

        //!
        //! Function, sending the queued command request and receiving its reply.
        //! Request and reply sockets are strictly alternating, therefore only one
        //! request is outstanding at a time.
        //!
        protected override void transceive()
        {
            // on a thread this waits until the next global tick releases it,
            // otherwise transceive is already called once per tick and waiting
            // would block the frame forever
            if (threaded)
                m_mre.WaitOne();

            if (m_commandRequest != null)
            {
                lock (m_lock)
                {
                    try
                    {
                        if (m_socket.hasOut)
                            m_socket.TrySend(m_commandRequest);
                        //else
                            //Helpers.Log("Command responses not send, no DataHub reachable!", Helpers.logMsgType.WARNING);

                        if (!m_socket.TryReceive(ref m_responses, receiveTimeout))
                        {
                            //Helpers.Log("Command responses reply not received, no DataHub reachable!", Helpers.logMsgType.WARNING);
                            if (threaded)
                                m_mre.Reset();

                            return;
                        }
                    }
                    catch { m_socket.Dispose(); }

                    if (m_responses.Count > 0)
                    {
                        byte[] header = m_responses[0];
                        if (header[0] != manager.cID)
                        {
                            switch ((DataHubMessageType)header[2])
                            {
                                case DataHubMessageType.PING:
                                    decodePongMessage(header);
                                    break;
                                default:
                                    decodeReplyMessage(m_responses);
                                    break;
                            }
                            m_commandRequest = null;
                        }
                        m_responses.Clear();
                    }
                }
            }

            // reset to stop the thread after one loop is done
            if (threaded)
                m_mre.Reset();
        }
    }
}
