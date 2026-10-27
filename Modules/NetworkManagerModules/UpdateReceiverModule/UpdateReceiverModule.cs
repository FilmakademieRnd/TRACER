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
using System.Buffers.Binary;
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
            public readonly byte[] Buffer; 
            public readonly int Length;   

            public BufferedMessage(byte[] buffer, int length)
            {
                Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
                Length = length;
            }
        }

        //!
        //! Buffer for storing incoming message by time (array of lists of bytes).
        //!
        private readonly List<BufferedMessage>[] m_messageBuffer;

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
            m_messageBuffer = new List<BufferedMessage>[core.timesteps];
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
            for (int i = 0; i < core.timesteps; i++)
                m_messageBuffer[i] = new List<BufferedMessage>(capacity: 16);

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
                        for (int i = 0; i < multipartMessage.FrameCount; i++)
                        {
                            NetMQFrame frame = multipartMessage[i];
                            byte[] buffer = frame.Buffer;

                            if (buffer != null && buffer.Length > 2 && buffer[0] != manager.cID)
                            {
                                MessageType msgType = (MessageType)buffer[2];

                                // Execute decoding outside of the global lock to prevent blocking the network thread
                                switch (msgType)
                                {
                                    case MessageType.LOCK:
                                        lock (_lock) { decodeLockMessage(buffer); }
                                        break;
                                    case MessageType.SYNC:
                                        lock (_lock) { decodeSyncMessage(buffer); }
                                        break;
                                    case MessageType.RESETOBJECT:
                                        lock (_lock) { decodeResetMessage(buffer); }
                                        break;
                                    case MessageType.UNDOREDOADD:
                                        lock (_lock) { decodeUndoRedoMessage(buffer); }
                                        break;
                                    case MessageType.DATAHUB:
                                        lock (_lock) { decodeDataHubMessage(buffer); }
                                        break;
                                    case MessageType.RPC:
                                    case MessageType.PARAMETERUPDATE:
                                        int messageSize = frame.MessageSize;
                                        int timeSlot = buffer[1];

                                        // Rent from pool and copy the span immediately
                                        byte[] rentedArray = ArrayPool<byte>.Shared.Rent(messageSize);
                                        frame.Buffer.AsSpan(0, messageSize).CopyTo(rentedArray);

                                        lock (_lock)
                                        {
                                            m_messageBuffer[timeSlot].Add(new BufferedMessage(rentedArray, messageSize));
                                        }
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                        // Clear the message container for reuse in the next network cycle
                        multipartMessage.Clear();
                    }
                }
                catch (Exception e)
                {
                    Helpers.Log(e.Message, Helpers.logMsgType.WARNING);
                }

                // Yield execution to allow other threads to process without heavy context-switching overhead
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
            bool lockState = message[6] != 0;

            if (lockState)
            {
                byte sceneID = message[3];
                short sceneObjectID = BinaryPrimitives.ReadInt16LittleEndian(message.AsSpan(4, 2));

                SceneObject sceneObject = m_sceneManager.getSceneObject(sceneID, sceneObjectID);
                if (sceneObject != null)
                {
                    sceneObject._lock = lockState;

                    if (sceneObject.playedByTimeline)
                    {
                        // If we are animating the object, lock it!
                        sceneObject.lockObject(true);
                        Debug.Log("Instantly lock unlocked object we received because its playing!");
                    }
                }
            }
            // Delay unlock message
            else
            {
                // Add a 1/4 second delay (framerate / 4) into the future
                int bufferTime = (((message[1] + core.settings.framerate / 4) + core.timesteps) % core.timesteps);

                const int lockMessageSize = 7;
                byte[] rentedArray = ArrayPool<byte>.Shared.Rent(lockMessageSize);
                message.AsSpan(0, lockMessageSize).CopyTo(rentedArray);

                m_messageBuffer[bufferTime].Add(new BufferedMessage(rentedArray, lockMessageSize));
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
                byte oldSceneID = 0;
                short oldParameterObjectID = 0;
                bool paraObjectNotFound = true;
                ParameterObject parameterObject = null;

                List<BufferedMessage> timeSlotBuffer = m_messageBuffer[bufferTime];

                for (int i = 0; i < timeSlotBuffer.Count; i++)
                {
                    BufferedMessage bufferedMsg = timeSlotBuffer[i];

                    // Create a read-only view of the message buffer safely
                    ReadOnlySpan<byte> message = new ReadOnlySpan<byte>(bufferedMsg.Buffer, 0, bufferedMsg.Length);

                    try
                    {
                        // Basic boundary check to ensure header data exists
                        if (message.Length < 3) continue;

                        if ((MessageType)message[2] == MessageType.LOCK)
                        {
                            // Ensure the LOCK message has the required minimum length (7 bytes)
                            if (message.Length < 7) continue;

                            byte sceneID = message[3];
                            short parameterObjectID = MemoryMarshal.Read<short>(message.Slice(4, 2));
                            bool lockState = MemoryMarshal.Read<bool>(message.Slice(6, 1));

                            SceneObject sceneObject = m_sceneManager.getSceneObject(sceneID, parameterObjectID);
                            if (sceneObject != null)
                            {
                                sceneObject._lock = lockState;

                                if (sceneObject.playedByTimeline)
                                {
                                    sceneObject.lockObject(true);
                                }
                            }
                        }
                        else
                        {
                            parameterObject = null;
                            paraObjectNotFound = true;
                            int start = 3;

                            while (start + 10 <= message.Length)
                            {
                                byte sceneID = message[start];
                                short parameterObjectID = MemoryMarshal.Read<short>(message.Slice(start + 1, 2));
                                short parameterID = MemoryMarshal.Read<short>(message.Slice(start + 3, 2));
                                int length = MemoryMarshal.Read<int>(message.Slice(start + 6, 4));

                                // Prevent infinite loop if a corrupted message has length <= 0
                                if (length <= 0) break;

                                // Safeguard against corrupted data exceeding total buffer length
                                if (start + length > message.Length) break;

                                if (paraObjectNotFound ||
                                    sceneID != oldSceneID ||
                                    parameterObjectID != oldParameterObjectID)
                                {
                                    parameterObject = core.getParameterObject(sceneID, parameterObjectID);
                                }

                                if (parameterObject != null)
                                {
                                    paraObjectNotFound = false;

                                    if (parameterObject.parameterList != null && parameterID < parameterObject.parameterList.Count)
                                    {
                                        AbstractParameter parameter = parameterObject.parameterList[parameterID];

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

                                        // FIXED: Sliced with exact payload length to prevent reading into subsequent messages
                                        int payloadLength = length - 10;
                                        parameter.deSerialize(message.Slice(start + 10, payloadLength));
                                    }
                                }
                                else
                                    paraObjectNotFound = true;

                                start += length;
                                oldSceneID = sceneID;
                                oldParameterObjectID = parameterObjectID;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error parsing network message: {ex.Message}");
                    }
                    finally
                    {
                        // Recycle the byte array buffer back into the shared pool
                        ArrayPool<byte>.Shared.Return(bufferedMsg.Buffer, clearArray: false);
                    }
                }

                // Clear the slot list while keeping its allocated capacity for future updates
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
