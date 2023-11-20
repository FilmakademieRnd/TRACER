/*
TRACER FOUNDATION - 
Toolset for Realtime Animation, Collaboration & Extended Reality
tracer.research.animationsinstitut.de
https://github.com/FilmakademieRnd/TRACER

Copyright (c) 2023 Filmakademie Baden-Wuerttemberg, Animationsinstitut R&D Lab

TRACER is a development by Filmakademie Baden-Wuerttemberg, Animationsinstitut
R&D Labs in the scope of the EU funded project MAX-R (101070072) and funding on
the own behalf of Filmakademie Baden-Wuerttemberg.  Former EU projects Dreamspace
(610005) and SAUCE (780470) have inspired the TRACER development.

This program is distributed in the hope that it will be useful, but WITHOUT
ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
FOR A PARTICULAR PURPOSE. See the MIT License for more details.
You should have received a copy of the MIT License along with this program; 
if not go to https://opensource.org/licenses/MIT
*/

//! @file "UpdateSenderModule.cs"
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
        //! Array of received command responses.
        //!
        private byte[] m_commandResponse = null;

        //!
        //! Constructor
        //!
        //! @param  name  The  name of the module.
        //! @param core A reference to the TRACER core.
        //!
        public CommandModule(string name, Manager manager) : base(name, manager)
        {
        }

        //!
        //! Function for custom initialisation.
        //! 
        //! @param sender The TRACER core.
        //! @param e The pssed event arguments.
        //! 
        protected override void Init(object sender, EventArgs e)
        {
            m_pingTimes = new Queue<byte>(new byte[] { 0, 0, 0, 0, 0 });

            SceneManager sceneManager = core.getManager<SceneManager>();
            sceneManager.sceneReady += connectAndStart;
        }

        //!
        //! Destructor, cleaning up event registrations. 
        //!
        public override void Dispose()
        {
            base.Dispose();
            SceneManager sceneManager = core.getManager<SceneManager>();

            if (sceneManager != null)
                sceneManager.sceneReady -= connectAndStart;

            core.syncEvent -= queuePingMessage;
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

            SceneManager sceneManager = core.getManager<SceneManager>();

            core.syncEvent += queuePingMessage;
            manager.sendServerCommand += queueCommandMessage;
        }


        //!
        //! Function that creates a command message for sending.
        //!
        //! @param sender The TRACER core.
        //! @param time The clients global time.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void queueCommandMessage(object sender, byte[] command)
        {
            m_commandRequest = new byte[3 + command.Length];

            // header
            m_commandRequest[0] = manager.cID;
            m_commandRequest[1] = core.time;
            m_commandRequest[2] = (byte)MessageType.DATAHUB;
            command.CopyTo(m_commandRequest.AsSpan().Slice(3));

            m_pingStartTime = core.time;

            m_mre.Set();
        }


        //!
        //! Function that creates a ping message for sending.
        //!
        //! @param sender The TRACER core.
        //! @param time The clients global time.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void queuePingMessage(object sender, byte time)
        {
            // if (m_commandRequest == null)
            {
                m_commandRequest = new byte[3];

                m_pingStartTime = time;

                lock (m_commandRequest)
                {
                    // header
                    m_commandRequest[0] = manager.cID;
                    m_commandRequest[1] = time;
                    m_commandRequest[2] = (byte)MessageType.PING;
                }

                m_mre.Set();
            }
        }

        //! 
        //! Function that decodes a sync message and set the clients global time.
        //!
        //! @param message The message to be decoded.
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
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void decodeDatahubMessage(byte[] message)
        {
            // ...
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

            requester.Connect("tcp://" + m_ip + ":" + m_port);
            Helpers.Log("Command Module connected: " + "tcp://" + m_ip + ":" + m_port);
            while (m_isRunning)
            {
                m_mre.WaitOne();
                if (m_commandRequest != null)
                {
                    lock (m_commandRequest)
                    {
                        requester.SendFrame(m_commandRequest);
                        try { m_commandResponse = requester.ReceiveFrameBytes(); } catch { }
                        if (m_commandResponse != null)
                        {
                            if (m_commandResponse[0] != manager.cID)
                            {
                                switch ((MessageType)m_commandResponse[2])
                                {
                                    case MessageType.PING:
                                        decodePongMessage(m_commandResponse);
                                        break;
                                    case MessageType.DATAHUB:
                                        decodeDatahubMessage(m_commandResponse);
                                        break;
                                    default:
                                        break;
                                }
                                m_commandRequest = null;
                            }
                        }
                    }
                }
                // reset to stop the thread after one loop is done
                m_mre.Reset();

                Thread.Yield();
            }
        }
    }
}
