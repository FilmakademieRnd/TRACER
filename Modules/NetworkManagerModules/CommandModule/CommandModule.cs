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
using NetMQ;
using NetMQ.Sockets;
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
            manager.requestCommandServer += connectAndStart;
            //SceneManager sceneManager = core.getManager<SceneManager>();
            //sceneManager.sceneReady += connectAndStart;
            //connectAndStart(this, EventArgs.Empty);
        }

        //!
        //! Destructor, cleaning up event registrations. 
        //!
        public override void Dispose()
        {
            base.Dispose();
            SceneManager sceneManager = core.getManager<SceneManager>();

            //if (sceneManager != null)
            //    sceneManager.sceneReady -= connectAndStart;

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

            core.syncEvent += queuePingMessage;
            manager.sendServerCommand += queueCommandMessage;
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
                m_commandRequest = new byte[3 + command.Length];
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
                TaskCompletionSource<List<byte[]>> tcs = manager.m_commandBufferWritten.Dequeue();
                tcs.TrySetResult(responses.ConvertAll(x => x.ToArray()));
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
        protected override void run()
        {
            m_isRunning = true;
            AsyncIO.ForceDotNet.Force();
            RequestSocket requester = new RequestSocket();
            m_socket = requester;
            List<byte[]> responses = new List<byte[]>();

            requester.Connect("tcp://" + m_ip + ":" + m_port);
            Helpers.Log("Command Module connected: " + "tcp://" + m_ip + ":" + m_port);
            while (m_isRunning)
            {
                m_mre.WaitOne();
                if (m_commandRequest != null)
                {
                    lock (m_lock)
                    {
                        try 
                        {
                            if (requester.HasOut)
                                requester.TrySendFrame(m_commandRequest);
                            else
                                Helpers.Log("Command responses not send, no DataHub reachable!", Helpers.logMsgType.WARNING);

                            if (!requester.TryReceiveMultipartBytes(TimeSpan.FromSeconds(1.0), ref responses))
                            {
                                //Helpers.Log("Command responses reply not received, no DataHub reachable!", Helpers.logMsgType.WARNING);
                                m_mre.Reset();

                                continue;
                            }
                        } catch { requester.Dispose(); }
                        if (responses.Count > 0)
                        {
                            byte[] header = responses[0];
                            if (header[0] != manager.cID)
                            {

                                switch ((DataHubMessageType)header[2])
                                {
                                    case DataHubMessageType.PING:
                                        decodePongMessage(header);
                                        break;
                                    default:
                                        decodeReplyMessage(responses);
                                        break;
                                }
                                m_commandRequest = null;
                            }
                            responses.Clear();
                        }
                    }
                }
                // reset to stop the thread after one loop is done
                m_mre.Reset();
                Thread.Yield();
            }
            m_thredEnded.TrySetResult(true);
        }
    }
}
