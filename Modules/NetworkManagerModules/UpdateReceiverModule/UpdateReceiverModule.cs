﻿/*
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

//! @file "UpdateReceiverModule.cs"
//! @brief Implementation of the update receiver module, listening to parameter updates from clients
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 19.06.2024

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;
using UnityEngine;
using System.Runtime.InteropServices;

namespace tracer
{
    //!
    //! Class implementing the scene sender module, listening to scene requests and sending scene data.
    //!
    public class UpdateReceiverModule : NetworkManagerModule
    {
        //!
        //! Buffer for storing incoming message by time (array of lists of bytes).
        //!
        private List<byte[]>[] m_messageBuffer;

        //!
        //! Event emitted when parameter change should be added to undo/redo history
        //!
        public event EventHandler<AbstractParameter> receivedHistoryUpdate;

        //!
        //! A referece to TRACER's scene manager.
        //!
        private SceneManager m_sceneManager;

        //!
        //! Constructor
        //!
        //! @param  name  The  name of the module.
        //! @param core A reference to the TRACER core.
        //!
        public UpdateReceiverModule(string name, Manager manager) : base(name, manager)
        {
        }

        //!
        //! Cleaning up event registrations. 
        //!
        protected override void Cleanup(object sender, EventArgs e)
        {
            base.Cleanup(sender, e);
            core.timeEvent -= consumeMessages;
            m_sceneManager.sceneReady -= connectAndStart;
        }

        //!
        //! Function for custom initialisation.
        //! 
        //! @param sender The TRACER core.
        //! @param e The pssed event arguments.
        //! 
        protected override void Init(object sender, EventArgs e)
        {
            // initialize message buffer
            m_messageBuffer = new List<byte[]>[core.timesteps];
            for (int i = 0; i < core.timesteps; i++)
                m_messageBuffer[i] = new List<byte[]>(64);

            m_sceneManager = core.getManager<SceneManager>();
            m_sceneManager.sceneReady += connectAndStart;
        }

        //!
        //! Function that connects the scene object change events for parameter queuing.
        //!
        //! @param sender The emitting scene object.
        //! @param e The pssed event arguments.
        //!
        private void connectAndStart(object sender, EventArgs e)
        {
            startUpdateReceiver(manager.settings.ipAddress.value, "5556");

            core.timeEvent += consumeMessages;
        }

        //!
        //! Function, waiting for incoming message (executed in separate thread).
        //! Control message are executed immediately, parameter update message are buffered
        //! and executed later to obtain synchronicity.
        //!
        protected override void run()
        {
            m_isRunning = true;
            AsyncIO.ForceDotNet.Force();
            var receiver = new SubscriberSocket();
            m_socket = receiver;
            receiver.SubscribeToAnyTopic();
            receiver.Connect("tcp://" + m_ip + ":" + m_port);
            Helpers.Log("Update receiver connected: " + "tcp://" + m_ip + ":" + m_port);
            byte[] message = null;
            while (m_isRunning)
            {
                try
                {
                    if (receiver.TryReceiveFrameBytes(System.TimeSpan.FromSeconds(1), out message))
                    {
                        if (message != null)
                            if (message[0] != manager.cID)
                            {
                                switch ((MessageType)message[2])
                                {
                                    case MessageType.LOCK:
                                        decodeLockMessage(message);
                                        break;
                                    case MessageType.SYNC:
                                        decodeSyncMessage(message);
                                        break;
                                    case MessageType.RESETOBJECT:
                                        decodeResetMessage(message);
                                        break;
                                    case MessageType.UNDOREDOADD:
                                        decodeUndoRedoMessage(message);
                                        break;
                                    case MessageType.DATAHUB:
                                        decodeDataHubMessage(message);
                                        break;
                                    case MessageType.RPC:
                                    case MessageType.PARAMETERUPDATE:
                                        // make shure that producer and consumer exclude eachother
                                        lock (m_messageBuffer)
                                        {
                                            // message[1] is time
                                            //int time = (message[1] + manager.pingRTT) % core.timesteps;
                                            //m_messageBuffer[time].Add(message);
                                            m_messageBuffer[message[1]].Add(message);
                                        }
                                        break;
                                    default:
                                        break;
                                }
                            }
                    }
                }
                catch { }
                Thread.Yield();
            }
        }

        //! 
        //! Function that decodes a sync message and set the clients global time.
        //!
        //! @param message The message to be decoded.
        //! 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void decodeSyncMessage(byte[] message)
        {
            int runtime = Mathf.FloorToInt(manager.pingRTT * 0.5f);  // *0.5 to convert rtt to one way
            int coreTime = core.time;
            int syncTime = message[1] + runtime;
            int deltaTime = Helpers.DeltaTime(core.time, message[1], core.timesteps);

            if (deltaTime > 10 ||
                 deltaTime > 3 && runtime < 8)
            {
                core.time = (byte)(Mathf.RoundToInt(syncTime) % core.timesteps);
                // UnityEngine.Debug.Log("Core time updated to: " + coreTime);
            }

            //UnityEngine.Debug.Log("Time delta: " + deltaTime);
        }

        //! 
        //! Function that decodes a lock message and lock or unlock the corresponding scene object.
        //!
        //! @param message The message to be decoded.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void decodeLockMessage(byte[] message)
        {
            bool lockState = BitConverter.ToBoolean(message, 6);

            if (lockState)
            {
                byte sceneID = message[3];
                short sceneObjectID = BitConverter.ToInt16(message, 4);

                SceneObject sceneObject = m_sceneManager.getSceneObject(sceneID, sceneObjectID);
                sceneObject._lock = lockState;
            }
            // delay unlock message
            else
            {
                int bufferTime = (((message[1] + core.settings.framerate / 4) + core.timesteps) % core.timesteps);
                m_messageBuffer[bufferTime].Add(message);
            }
        }

        private void decodeUndoRedoMessage(byte[] message)
        {
            byte sceneID = message[3];
            short sceneObjectID = BitConverter.ToInt16(message, 4);
            short parameterID = BitConverter.ToInt16(message, 6);

            ParameterObject sceneObject = core.getParameterObject(sceneID, sceneObjectID);

            receivedHistoryUpdate?.Invoke(this, sceneObject.parameterList[parameterID]);
        }

        private void decodeResetMessage(byte[] message)
        {
            byte sceneID = message[3];
            short sceneObjectID = BitConverter.ToInt16(message, 4);
            SceneObject sceneObject = m_sceneManager.getSceneObject(sceneID, sceneObjectID);

            foreach (AbstractParameter p in sceneObject.parameterList)
                p.reset();
            m_sceneManager.getModule<UndoRedoModule>().vanishHistory(sceneObject);
        }

        private void decodeDataHubMessage(byte[] message)
        {
            byte dhType = message[3];
            bool status = BitConverter.ToBoolean(message, 4);
            byte cID = message[5];

            // dhType 0 = client connection status update
            if (dhType == 0 &&
                cID != manager.cID)
                manager.ClientConnectionUpdate(status, cID);
        }

        //!
        //! Function that triggers the parameter updates (called once a global time tick).
        //! It also decodes all parameter message and update the corresponding parameters. 
        //!
        private void consumeMessages(object o, EventArgs e)
        {
            // define the buffer size by defining the time offset in the ringbuffer
            // % time steps to take ring (0 to core.timesteps) into account
            // set to 1/10 second
            int bufferTime = (((core.time - core.settings.framerate / 6) + core.timesteps) % core.timesteps);
            lock (m_messageBuffer)
            {
                // caching the ParameterObject
                byte oldSceneID = 0;
                short oldParameterObjectID = 0;
                ParameterObject parameterObject = null;
                List<byte[]> timeSlotBuffer = m_messageBuffer[bufferTime];

                for (int i = 0; i < timeSlotBuffer.Count; i++)
                {
                    ReadOnlySpan<byte> message = timeSlotBuffer[i];

                    if ((MessageType)message[2] == MessageType.LOCK)
                    {
                        byte sceneID = message[3];
                        short parameterObjectID = MemoryMarshal.Read<short>(message.Slice(4));
                        bool lockState = MemoryMarshal.Read<bool>(message.Slice(6));

                        SceneObject sceneObject = m_sceneManager.getSceneObject(sceneID, parameterObjectID);
                        sceneObject._lock = lockState;
                    }
                    else
                    {
                        int start = 3;
                        while (start < message.Length)
                        {
                            byte sceneID = message[start];
                            short parameterObjectID = MemoryMarshal.Read<short>(message.Slice(start + 1));
                            short parameterID = MemoryMarshal.Read<short>(message.Slice(start + 3));
                            int length = message[start + 6];

                            if (sceneID != oldSceneID ||
                                parameterObjectID != oldParameterObjectID)
                                parameterObject = core.getParameterObject(sceneID, parameterObjectID);

                            if (parameterObject != null)
                            {
                                AbstractParameter parameter = parameterObject.parameterList[parameterID];
                                // check update if animation is incoming and change parameter type if required 
                                // 7 is the size of the parameter fixed fields
                                if (!parameter.isAnimated && length > 7 + parameter.dataSize())
                                {
                                    parameter = parameter.getAnimationParameter();
                                }
                                parameter.deSerialize(message.Slice(start + 7));
                            }

                            start += length;
                            oldSceneID = sceneID;
                            oldParameterObjectID = parameterObjectID;
                        }
                    }
                }

                timeSlotBuffer.Clear();
            }
        }

        //!
        //! Function to start the scene sender module.
        //! @param ip The IP address to be used from the sender.
        //! @param port The port number to be used from the sender.
        //!
        void startUpdateReceiver(string ip, string port)
        {
            start(ip, port);
        }
    }
}
