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

//! @file "UpdateReceiverModule.cs"
//! @brief Implementation of the update receiver module, listening to parameter updates from clients
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 19.06.2024

using NetMQ;
using NetMQ.Sockets;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace tracer
{
    //!
    //! Class implementing the scene sender module, listening to scene requests and sending scene data.
    //!
    public class UpdateReceiverModule : NetworkManagerModule
    {
        private readonly struct BufferedMessage
        {
            public readonly NetMQFrame Frame; // Das gemietete Array aus dem ArrayPool
            public readonly int Length;   // Die tatsächliche Datengröße

            public BufferedMessage(NetMQFrame frame, int length)
            {
                Frame = frame;
                Length = length;
            }
        }

        //!
        //! Buffer for storing incoming message by time (array of lists of bytes).
        //!
        private readonly List<BufferedMessage>[] m_messageBuffer = new List<BufferedMessage>[256];

        //!
        //! Event emitted when parameter change should be added to undo/redo history
        //!
        public event EventHandler<AbstractParameter> receivedHistoryUpdate;

        //!
        //! A referece to TRACER's scene manager.
        //!
        private SceneManager m_sceneManager;

        //!
        //! Object for handling thread locking.
        //!
        private readonly object _lock = new object();

        //!
        //! Constructor
        //!
        //! @param  name  The  name of the module.
        //! @param _core A reference to the TRACER _core.
        //!
        public UpdateReceiverModule(string name, Manager manager) : base(name, manager)
        {
        }

        //!
        //! Cleaning up event registrations. 
        //!
        public override void Dispose()
        {
            base.Dispose();
            core.timeEvent -= consumeMessages;
            m_sceneManager.sceneReady -= connectAndStart;
            manager.settings.ipAddress.hasChanged -= reconnect;
        }

        //!
        //! Function for custom initialisation.
        //! 
        //! @param sender The TRACER _core.
        //! @param e The pssed event arguments.
        //! 
        protected override void Init(object sender, EventArgs e)
        {
            // initialize message buffer
            for (int i = 0; i < 256; i++)
            {
                m_messageBuffer[i] = new List<BufferedMessage>(capacity: 32); // Startkapazität nach Bedarf
            }

            m_sceneManager = core.getManager<SceneManager>();
            m_sceneManager.sceneReady += connectAndStart;
            manager.settings.ipAddress.hasChanged += reconnect;
        }

        //!
        //! Function that connects the reciver to the DataHub and starts the receive loop.
        //!
        //! @param sender The scene manager.
        //! @param e The pssed event arguments.
        //!
        private void connectAndStart(object sender, EventArgs e)
        {
            startUpdateReceiver(manager.settings.ipAddress.value, "5556");

            core.timeEvent += consumeMessages;
        }

        //!
        //! Function that reconnects the reciver to the DataHub and restarts the receive loop.
        //!
        //! @param sender The network manager.
        //! @param e The pssed ip address.
        //!
        private void reconnect(object sender, string ip)
        {
            startUpdateReceiver(ip, "5556");
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
            using var receiver = new SubscriberSocket();
            m_socket = receiver;
            receiver.SubscribeToAnyTopic();
            string connectionString = $"tcp://{m_ip}:{m_port}";
            receiver.Connect(connectionString);
            Helpers.Log($"Update receiver connected: {connectionString}");

            NetMQMessage multipartMessage = new NetMQMessage();
            
            while (m_isRunning)
            {
                try
                {
                    if (receiver.TryReceiveMultipartMessage(System.TimeSpan.FromSeconds(1), ref multipartMessage))
                    {
                        lock (_lock)
                        {
                            for (int i = 0; i < multipartMessage.FrameCount; i++)
                            {
                                NetMQFrame frame = multipartMessage[i];
                                byte[] buffer = frame.Buffer;
                                if (buffer != null && buffer[0] != manager.cID)
                                {
                                    switch ((MessageType)buffer[2])
                                    {
                                        case MessageType.LOCK:
                                            decodeLockMessage(buffer);
                                            break;
                                        case MessageType.SYNC:
                                            decodeSyncMessage(buffer);
                                            break;
                                        case MessageType.RESETOBJECT:
                                            decodeResetMessage(buffer);
                                            break;
                                        case MessageType.UNDOREDOADD:
                                            decodeUndoRedoMessage(buffer);
                                            break;
                                        case MessageType.DATAHUB:
                                            decodeDataHubMessage(buffer);
                                            break;
                                        case MessageType.RPC:
                                        case MessageType.PARAMETERUPDATE:
                                            //int time = (message[1] + (Mathf.RoundToInt((float)manager.pingRTT * 0.5f))) % core.timesteps;
                                            int messageSize = frame.MessageSize;
                                            int timeSlot = buffer[1]; // buffer[1] corresponds to the message time byte
                                            // Rent an array from the pool to safely isolate the streaming data
                                            byte[] rentedArray = ArrayPool<byte>.Shared.Rent(messageSize);
                                            frame.Buffer.AsSpan(0, messageSize).CopyTo(rentedArray);
                                            // Wrap the rented array into a NetMQFrame and push it onto the ringbuffer slot
                                            var pooledFrame = new NetMQFrame(rentedArray);
                                            m_messageBuffer[timeSlot].Add(new BufferedMessage(pooledFrame, messageSize));
                                            break;
                                        default:
                                            break;
                                    }

                                }
                            }
                        }
                        // Clear the message container for reuse in the next network cycle
                        multipartMessage.Clear();
                    }
                }
                catch (Exception e) { Helpers.Log(e.Message, Helpers.logMsgType.WARNING); }
                Thread.Yield();
            }
            m_thredEnded.TrySetResult(true);
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

            if (deltaTime > 10 || deltaTime > 3 && runtime < 8)
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
               
                if(sceneObject.playedByTimeline)
                {   //if we are animating the object, lock it!
                    sceneObject.lockObject(true);
                    Debug.Log("instantly lock unlocked object we received because its playing!");
                }
                
            }
            // delay unlock message
            else
            {
                int bufferTime = (((message[1] + core.settings.framerate / 4) + core.timesteps) % core.timesteps);
                
                // A standard LOCK message has a fixed size of 7 bytes (Header: 3 + SceneID: 1 + ObjectID: 2 + State: 1)
                const int lockMessageSize = 7;

                // Rent a safe, isolated array from the pool
                byte[] rentedArray = ArrayPool<byte>.Shared.Rent(lockMessageSize);

                // Copy the exact 7 bytes of the lock message into the rented array using Span
                message.AsSpan(0, lockMessageSize).CopyTo(rentedArray);

                // Wrap into a NetMQFrame and store it inside the ringbuffer as a BufferedMessage struct
                var pooledFrame = new NetMQFrame(rentedArray);
                m_messageBuffer[bufferTime].Add(new BufferedMessage(pooledFrame, lockMessageSize));
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

            switch ((DataHubMessageType)message[3])
            {
                case DataHubMessageType.CONNECTIONSTATUS:
                    bool status = BitConverter.ToBoolean(message, 4);
                    byte cID = message[5];
                    bool isServer = BitConverter.ToBoolean(message, 6);
                    if (cID != manager.cID)
                        manager.ClientConnectionUpdate(status, cID, isServer);
                    break;
            }
        }

        //!
        //! Function that triggers the parameter updates (called once a global time tick).
        //! It also decodes all parameter message and update the corresponding parameters. 
        //!
        private void consumeMessages(object o, EventArgs e)
        {
            // Define the buffer size by defining the time offset in the ringbuffer
            // % time steps to take ring (0 to _core.timesteps) into account
            // Set to 1/10 second
            int bufferTime = (((core.time - core.settings.framerate / 6) + core.timesteps) % core.timesteps);

            lock (_lock)
            {
                // Caching variables for the ParameterObject
                byte oldSceneID = 0;
                short oldParameterObjectID = 0;
                bool paraObjectNotFound = true;
                ParameterObject parameterObject = null;

                List<BufferedMessage> timeSlotBuffer = m_messageBuffer[bufferTime];

                for (int i = 0; i < timeSlotBuffer.Count; i++)
                {
                    BufferedMessage bufferedMsg = timeSlotBuffer[i];

                    ReadOnlySpan<byte> message = new ReadOnlySpan<byte>(bufferedMsg.Frame.Buffer, 0, bufferedMsg.Length);

                    try
                    {
                        if ((MessageType)message[2] == MessageType.LOCK)
                        {
                            byte sceneID = message[3];
                            short parameterObjectID = MemoryMarshal.Read<short>(message.Slice(4));
                            bool lockState = MemoryMarshal.Read<bool>(message.Slice(6));

                            SceneObject sceneObject = m_sceneManager.getSceneObject(sceneID, parameterObjectID);
                            sceneObject._lock = lockState;

                            if (sceneObject.playedByTimeline)
                            {
                                // If we are animating the object, lock it
                                sceneObject.lockObject(true);
                            }
                        }
                        else
                        {
                            parameterObject = null;
                            paraObjectNotFound = true;
                            int start = 3;

                            while (start < message.Length)
                            {
                                byte sceneID = message[start];
                                short parameterObjectID = MemoryMarshal.Read<short>(message.Slice(start + 1));
                                short parameterID = MemoryMarshal.Read<short>(message.Slice(start + 3));
                                int length = MemoryMarshal.Read<int>(message.Slice(start + 6));

                                if (paraObjectNotFound ||
                                    sceneID != oldSceneID ||
                                    parameterObjectID != oldParameterObjectID)
                                {
                                    parameterObject = core.getParameterObject(sceneID, parameterObjectID);
                                }

                                if (parameterObject != null)
                                {
                                    paraObjectNotFound = false;
                                    AbstractParameter parameter = parameterObject.parameterList[parameterID];

                                    // Check update if animation is incoming and change parameter type if required.
                                    // 10 is the size of the parameter fixed field.
                                    if (parameter._isAnimated)
                                    {
                                        if (length == 10 + parameter.defaultDataSize())
                                            parameter.reset();
                                    }
                                    else
                                    {
                                        if (length > 10 + parameter.dataSize())
                                            parameter.InitAnimation();
                                    }

                                    parameter.deSerialize(message.Slice(start + 10));
                                }
                                else
                                {
                                    paraObjectNotFound = true;
                                }

                                start += length;
                                oldSceneID = sceneID;
                                oldParameterObjectID = parameterObjectID;
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(bufferedMsg.Frame.Buffer, clearArray: false);
                    }
                }
                // Clear the slot list. The internal list capacity is preserved.
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
