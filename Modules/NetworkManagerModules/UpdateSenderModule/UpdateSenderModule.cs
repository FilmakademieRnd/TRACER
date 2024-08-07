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

//! @file "UpdateSenderModule.cs"
//! @brief Implementation of the update sender module, sending parameter updates to clients.
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

namespace tracer
{
    //!
    //! Class implementing the update sender module, sending parameter updates to clients.
    //!
    public class UpdateSenderModule : NetworkManagerModule
    {
        //!
        //! List of medified parameters for undo/redo handling.
        //!
        private List<AbstractParameter> m_modifiedParameters;

        //!
        //! The size of all currently modified parameters in byte;
        //!
        private int m_modifiedParametersDataSize = 0;

        //!
        //! Array of control messages, containing all tracer messages besides parameter updates.
        //!
        private byte[] m_controlMessage;

        //!
        //! Constructor
        //!
        //! @param  name  The  name of the module.
        //! @param core A reference to the TRACER core.
        //!
        public UpdateSenderModule(string name, Manager manager) : base(name, manager)
        {
        }

        //!
        //! Destructor, cleaning up event registrations. 
        //!
        public override void Dispose()
        {
            base.Dispose();
            SceneManager sceneManager = core.getManager<SceneManager>();
            UIManager uiManager = core.getManager<UIManager>();

            if (sceneManager != null && uiManager != null)
            {
                sceneManager.sceneReady -= connectAndStart;
                uiManager.selectionAdded -= lockSceneObject;
                uiManager.selectionRemoved -= unlockSceneObject;
            }

            foreach (SceneObject sceneObject in sceneManager.getAllSceneObjects())
            {
                sceneObject.hasChanged -= queueModifiedParameter;
            }

            core.timeEvent -= sendParameterMessages;
        }

        //!
        //! Function for custom initialisation.
        //! 
        //! @param sender The TRACER core.
        //! @param e The pssed event arguments.
        //! 
        protected override void Init(object sender, EventArgs e)
        {
            m_modifiedParameters = new List<AbstractParameter>();

            SceneManager sceneManager = core.getManager<SceneManager>();
            sceneManager.sceneReady += connectAndStart;
        }

        //!
        //! Function that connects the scene object change events for parameter queuing.
        //!
        //! @param sender The SceneManager.
        //! @param e The pssed event arguments.
        //!
        private void connectAndStart(object sender, EventArgs e)
        {
            startUpdateSender(manager.settings.ipAddress.value, "5557");

            UIManager uiManager = core.getManager<UIManager>();
            uiManager.selectionAdded += lockSceneObject;
            uiManager.selectionRemoved += unlockSceneObject;

            SceneManager sceneManager = core.getManager<SceneManager>();
            sceneManager.sceneObjectLocked += lockSceneObject;
            sceneManager.sceneObjectUnlocked += unlockSceneObject;

            foreach (SceneObject sceneObject in ((SceneManager)sender).getAllSceneObjects())
            {
                sceneObject.hasChanged += queueModifiedParameter;
            }
            
            foreach (DynamicParameterObject dynamicParameterObject in ((SceneManager)sender).getAllDynamicParameterObjects())
            {
                dynamicParameterObject.hasChanged += queueModifiedParameter;
            }

            manager.sceneObjectAdded += AddSceneObject;
            manager.sceneObjectRemoved += RemoveSceneObject;

            core.timeEvent += sendParameterMessages;
        }

        //!
        //! Function to add a scene object to the network sync.
        //!
        //! @param sender The network manager.
        //! @param sceneObject The scene object to be added to the network sync.
        //!
        private void AddSceneObject(object sender, SceneObject sceneObject)
        {
            sceneObject.hasChanged += queueModifiedParameter;
        }

        //!
        //! Function to remove a scene object from the network sync.
        //!
        //! @param sender The network manager.
        //! @param sceneObject The scene object to be added to the network sync.
        //!
        private void RemoveSceneObject(object sender, SceneObject sceneObject)
        {
            sceneObject.hasChanged -= queueModifiedParameter;
        }

        //!
        //! Function that creates and sends a lock message after a selectionAdd event invokes.
        //!
        //! @param sender The UI manager.
        //! @param sceneObject The selected scene object.
        //!
        private void lockSceneObject(object sender, SceneObject sceneObject)
        {
            m_controlMessage = new byte[7];

            // header
            m_controlMessage[0] = manager.cID;
            m_controlMessage[1] = core.time;
            m_controlMessage[2] = (byte)MessageType.LOCK;
            Helpers.copyArray(BitConverter.GetBytes(sceneObject.sceneID), 0, m_controlMessage, 3, 1);  // ScenetID
            Helpers.copyArray(BitConverter.GetBytes(sceneObject.id), 0, m_controlMessage, 4, 2);  // SceneObjectID
            m_controlMessage[6] = Convert.ToByte(true);

            m_mre.Set();
        }

        //!
        //! Function that creates and sends a (un)lock message after a selectionRemove event invokes.
        //!
        //! @param sender The UI manager.
        //! @param sceneObject The deselected scene object.
        //!
        private void unlockSceneObject(object sender, SceneObject sceneObject)
        {
            m_controlMessage = new byte[7];

            // header
            m_controlMessage[0] = manager.cID;
            m_controlMessage[1] = core.time;
            m_controlMessage[2] = (byte)MessageType.LOCK;
            Helpers.copyArray(BitConverter.GetBytes(sceneObject.sceneID), 0, m_controlMessage, 3, 1);  // SceneID
            Helpers.copyArray(BitConverter.GetBytes(sceneObject.id), 0, m_controlMessage, 4, 2);  // SceneObjectID
            m_controlMessage[6] = Convert.ToByte(false);

            m_mre.Set();
        }


        //!
        //! Function that creates a undo redo message.
        //!
        //! @param parameter The modified parameter the message will be based on.
        //! @param sender The spinner UI element.
        //!
        public void queueUndoRedoMessage(object sender, AbstractParameter parameter)
        {
            // Message structure: Header, Parameter (optional)
            // Header: ClientID, Time, MessageType
            // Parameter: SceneID, ParameterObjectID, ParameterID, ParameterType, ParameterData

            lock (parameter)
            {
                int parameterSize = parameter.dataSize();
                m_controlMessage = new byte[9 + parameterSize];
                parameter.Serialize(new Span<byte>(m_controlMessage, 9, parameterSize)); // ParameterData;

                // header
                m_controlMessage[0] = manager.cID;
                m_controlMessage[1] = core.time;
                m_controlMessage[2] = (byte)MessageType.UNDOREDOADD;

                // parameter
                Helpers.copyArray(BitConverter.GetBytes(parameter.parent.sceneID), 0, m_controlMessage, 3, 1);  // SceneID
                Helpers.copyArray(BitConverter.GetBytes(parameter.parent.id), 0, m_controlMessage, 4, 2);  // SceneObjectID
                Helpers.copyArray(BitConverter.GetBytes(parameter.id), 0, m_controlMessage, 6, 2);  // ParameterID
                m_controlMessage[8] = (byte)parameter.tracerType;  // ParameterType
            }

            m_mre.Set();
        }

        //!
        //! Function that creates a reset message.
        //!
        //! @param parameter The modified parameter the message will be based on.
        //!
        public void queueResetMessage(SceneObject sceneObject)
        {
            // Message structure: Header, Parameter (optional)
            // Header: ClientID, Time, MessageType
            // Parameter: SceneID, ParameterObjectID, ParameterID, ParameterType, ParameterData

            lock (sceneObject)
            {
                m_controlMessage = new byte[6]; // ParameterData;

                // header
                m_controlMessage[0] = manager.cID;
                m_controlMessage[1] = core.time;
                m_controlMessage[2] = (byte)MessageType.RESETOBJECT;

                // parameter
                Helpers.copyArray(BitConverter.GetBytes(sceneObject.sceneID), 0, m_controlMessage, 3, 1);  // SceneID
                Helpers.copyArray(BitConverter.GetBytes(sceneObject.id), 0, m_controlMessage, 4, 2);  // SceneObjectID
            }

            m_mre.Set();
        }

        //!
        //! Function that creates a reset message.
        //!
        //! @param parameter The modified parameter the message will be based on.
        //!
        public void queueRPCMessage(object sender, AbstractParameter parameter)
        {
            // Message structure: Header, Parameter (optional)
            // Header: ClientID, Time, MessageType
            // Parameter: SceneID, ParameterObjectID, ParameterID, ParameterType, ParameterData

            lock (sender)
            {
                m_controlMessage = new byte[3 + parameter.dataSize() + 7];
                Span<byte> msgSpan = m_controlMessage;

                // header
                msgSpan[0] = manager.cID; // ClientID
                msgSpan[1] = core.time; // Time
                msgSpan[2] = (byte)MessageType.RPC; // MessageType

                int length = 7 + parameter.dataSize();
                Span<byte> newSpan = msgSpan.Slice(3, length);

                newSpan[0] = parameter.parent.sceneID;  // SceneID
                BitConverter.TryWriteBytes(newSpan.Slice(1, 2), parameter.parent.id);  // SceneObjectID
                BitConverter.TryWriteBytes(newSpan.Slice(3, 2), parameter.id);  // ParameterID
                newSpan[5] = (byte)parameter.tracerType;  // ParameterType
                newSpan[6] = (byte)newSpan.Length;  // Parameter message length
                parameter.Serialize(newSpan.Slice(7)); // Parameter data
            }
            m_mre.Set();
        }

        //!
        //! Function collects all parameter modifications within one global time tick for sending.
        //!
        //! @param sender The scene object containing the modified parameter.
        //! @param parameter The modified parameter.
        //!
        private void queueModifiedParameter(object sender, AbstractParameter parameter)
        {
            lock (m_modifiedParameters)
            {
                if (parameter.isRPC)
                {
                    if (!parameter.isNetworkLocked)
                        queueRPCMessage(sender, parameter);
                    return;
                }

                bool paramInList = m_modifiedParameters.Contains(parameter);
                if (parameter.isNetworkLocked)
                {
                    if (paramInList)
                    {
                        m_modifiedParameters.Remove(parameter);
                        m_modifiedParametersDataSize -= parameter.dataSize();
                    }
                }
                else if (!paramInList)
                {
                    m_modifiedParameters.Add(parameter);
                    m_modifiedParametersDataSize += parameter.dataSize();
                }
            }
        }

        //!
        //! Function that creates a parameter update message (byte[]) based on a abstract parameter and a time value.
        //!
        //! @param parameter The modified parameter the message will be based on.
        //! @param time The time for synchronization
        //! @param addToHistory should this update be added to undo/redo history
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte[] createParameterMessage()
        {
            // Message structure: Header, Parameter List (optional)
            // Header: ClientID, Time, MessageType
            // ParameterList: List<SceneObjectID, ParameterID, ParameterType, Parameter message length, ParameterData>

            byte[] message = new byte[3 + m_modifiedParametersDataSize + 7 * m_modifiedParameters.Count];
            Span<byte> msgSpan = new Span<byte>(message);

            // header
            msgSpan[0] = manager.cID; // ClientID
            msgSpan[1] = core.time; // Time
            msgSpan[2] = (byte)MessageType.PARAMETERUPDATE; // MessageType

            // list of parameters
            int start = 3;
            for (int i = 0; i < m_modifiedParameters.Count; i++)
            {
                AbstractParameter parameter = m_modifiedParameters[i];
                lock (parameter)
                {
                    int length = 7 + parameter.dataSize();
                    Span<byte> newSpan = msgSpan.Slice(start, length);

                    newSpan[0] = parameter.parent.sceneID;  // SceneID
                    BitConverter.TryWriteBytes(newSpan.Slice(1, 2), parameter.parent.id);  // SceneObjectID
                    BitConverter.TryWriteBytes(newSpan.Slice(3, 2), parameter.id);  // ParameterID
                    newSpan[5] = (byte)parameter.tracerType;  // ParameterType
                    newSpan[6] = (byte)newSpan.Length;  // Parameter message length
                    parameter.Serialize(newSpan.Slice(7)); // Parameter data

                    start += length;
                }
            }

            return message;
        }

        //!
        //! Function, sending control messages and parameter update messages (executed in separate thread).
        //! Thread execution is locked after every loop and unlocked by sendParameterMessages every global tick.
        //!
        protected override void run()
        {
            m_isRunning = true;
            AsyncIO.ForceDotNet.Force();
            var sender = new PublisherSocket();
            sender.Options.Linger = TimeSpan.FromMilliseconds(0);
            sender.Options.Backlog = 10;
            m_socket = sender;

            sender.Connect("tcp://" + m_ip + ":" + m_port);
            Helpers.Log("Update sender connected: " + "tcp://" + m_ip + ":" + m_port);
            while (m_isRunning)
            {
                m_mre.WaitOne();
                if (m_controlMessage != null)
                {
                    lock (m_controlMessage)
                    {
                        try { sender.SendFrame(m_controlMessage, false); } catch { } // true not wait 
                        m_controlMessage = null;
                    }
                }
                else if (m_modifiedParameters.Count > 0)
                {
                    lock (m_modifiedParameters)
                    {
                        try { sender.SendFrame(createParameterMessage(), false); } catch { } // true not wait
                        m_modifiedParameters.Clear();
                        m_modifiedParametersDataSize = 0;
                    }
                }
                // reset to stop the thread after one loop is done
                m_mre.Reset();

                Thread.Yield();
            }
        }

        //!
        //! Function that unlocks the sender thread once (called with every global tick event).
        //!
        //! @param sender The TRACER core.
        //! @param e Empty.
        //!
        private void sendParameterMessages(object sender, EventArgs e)
        {
            if (m_modifiedParameters.Count > 0)
                m_mre.Set();
        }

        //!
        //! Function to start the scene sender module.
        //!
        //! @param ip The IP address to be used from the sender.
        //! @param port The port number to be used from the sender.
        //!
        public void startUpdateSender(string ip, string port)
        {
            start(ip, port);
        }
    }
}
