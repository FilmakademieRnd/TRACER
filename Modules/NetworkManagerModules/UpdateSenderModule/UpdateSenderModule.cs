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
using UnityEngine;

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
        //! List of control messages, containing all tracer messages besides parameter updates.
        //!
        private NetMQMessage m_controlMessages;

        //!
        //! List of parameter messages, containing all tracer parameter updat messages.
        //!
        private NetMQMessage m_parameterMessages;

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
        //! @param sender The TRACER _core.
        //! @param e The pssed event arguments.
        //! 
        protected override void Init(object sender, EventArgs e)
        {
            m_modifiedParameters = new List<AbstractParameter>();
            m_controlMessages = new NetMQMessage(3);
            m_parameterMessages = new NetMQMessage(6);

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
            byte sceneId;
            short sceneObjectId;
            lock (sceneObject)
            {                      
                sceneId = sceneObject._sceneID;
                sceneObjectId = sceneObject._id;
            }

            lock (_lock)
            {
                byte[] message = new byte[7];
                // header
                message[0] = manager.cID;
                message[1] = core.time;
                message[2] = (byte)MessageType.LOCK;
                Helpers.copyArray(BitConverter.GetBytes(sceneId), 0, message, 3, 1);           // SceneID
                Helpers.copyArray(BitConverter.GetBytes(sceneObjectId), 0, message, 4, 2);     // SceneObjectID
                message[6] = Convert.ToByte(true);
                m_controlMessages.Append(message);
            }

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
            if(sceneObject.playedByTimeline)   //dont send unlock event if object is currently played via timeline
                return;

            byte sceneId;
            short sceneObjectId;
            lock (sceneObject)
            {                      //can still be altered from any access that is not using lock!
                sceneId = sceneObject._sceneID;
                sceneObjectId = sceneObject._id;
            }

            lock (_lock)
            {
                byte[] message = new byte[7];
                // header
                message[0] = manager.cID;
                message[1] = core.time;
                message[2] = (byte)MessageType.LOCK;
                Helpers.copyArray(BitConverter.GetBytes(sceneId), 0, message, 3, 1);           // SceneID
                Helpers.copyArray(BitConverter.GetBytes(sceneObjectId), 0, message, 4, 2);     // SceneObjectID
                message[6] = Convert.ToByte(false);
                m_controlMessages.Append(message);
            }

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

             lock (_lock)
            {
                int parameterSize = parameter.dataSize();
                byte[] message = new byte[9 + parameterSize];
                parameter.Serialize(new Span<byte>(message, 9, parameterSize)); // ParameterData;

                // header
                message[0] = manager.cID;
                message[1] = core.time;
                message[2] = (byte)MessageType.UNDOREDOADD;

                // parameter
                Helpers.copyArray(BitConverter.GetBytes(parameter._parent._sceneID), 0, message, 3, 1);  // SceneID
                Helpers.copyArray(BitConverter.GetBytes(parameter._parent._id), 0, message, 4, 2);  // SceneObjectID
                Helpers.copyArray(BitConverter.GetBytes(parameter._id), 0, message, 6, 2);  // ParameterID
                message[8] = (byte)parameter.tracerType;  // ParameterType
                m_controlMessages.Append(message);
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

            lock (_lock)
            {
                byte[] message = new byte[6]; // ParameterData;

                // header
                message[0] = manager.cID;
                message[1] = core.time;
                message[2] = (byte)MessageType.RESETOBJECT;

                // parameter
                Helpers.copyArray(BitConverter.GetBytes(sceneObject._sceneID), 0, message, 3, 1);  // SceneID
                Helpers.copyArray(BitConverter.GetBytes(sceneObject._id), 0, message, 4, 2);  // SceneObjectID
                m_controlMessages.Append(message);
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

            byte[] message = new byte[3 + parameter.dataSize() + 10];
            Span<byte> msgSpan = message;

            // header
            msgSpan[0] = manager.cID; // ClientID
            msgSpan[1] = core.time; // Time
            msgSpan[2] = (byte)MessageType.RPC; // MessageType

            int length = 10 + parameter.dataSize();
            Span<byte> newSpan = msgSpan.Slice(3, length);

            newSpan[0] = parameter._parent._sceneID;  // SceneID
            BitConverter.TryWriteBytes(newSpan.Slice(1, 2), parameter._parent._id);  // SceneObjectID
            BitConverter.TryWriteBytes(newSpan.Slice(3, 2), parameter._id);  // ParameterID
            newSpan[5] = (byte)parameter.tracerType;  // ParameterType
            //newSpan[6] = (byte)newSpan.Length;  // Parameter message length
            BitConverter.TryWriteBytes(newSpan.Slice(6, 4), newSpan.Length);  // Parameter message length
            parameter.Serialize(newSpan.Slice(10)); // Parameter data

            m_controlMessages.Append(message);
            //m_mre.Set();
        }

        //!
        //! Function collects all parameter modifications within one global time tick for sending.
        //!
        //! @param sender The scene object containing the modified parameter.
        //! @param parameter The modified parameter.
        //!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void queueModifiedParameter(object sender, AbstractParameter parameter)
        {
            lock (_lock)
            {
                if (parameter._isRPC)
                {
                    if (!parameter._networkLock)
                        queueRPCMessage(sender, parameter);
                    return;
                }

                int paramInList = m_modifiedParameters.FindIndex(p => p == parameter);
                if (parameter._networkLock)
                {
                    if (paramInList > -1)
                    {
                        m_modifiedParameters.RemoveAt(paramInList);
                        m_modifiedParametersDataSize -= parameter.dataSize();
                    }
                }
                else if (paramInList == -1)
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

            byte[] message = new byte[3 + m_modifiedParametersDataSize + 10 * m_modifiedParameters.Count];
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
                int length = 10 + parameter.dataSize();
                Span<byte> newSpan = msgSpan.Slice(start, length);

                newSpan[0] = parameter._parent._sceneID;  // SceneID
                BitConverter.TryWriteBytes(newSpan.Slice(1, 2), parameter._parent._id);  // SceneObjectID
                BitConverter.TryWriteBytes(newSpan.Slice(3, 2), parameter._id);  // ParameterID
                newSpan[5] = (byte)parameter.tracerType;  // ParameterType
                BitConverter.TryWriteBytes(newSpan.Slice(6, 4), newSpan.Length);  // Parameter message length
                parameter.Serialize(newSpan.Slice(10)); // Parameter data

                start += length;
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
            const int packageSize = 0;  // change these numer to > 0 (4) to enable framewise message bundling
            int i = 0;
            m_socket = sender;

            sender.Connect("tcp://" + m_ip + ":" + m_port);
            Helpers.Log("Update sender connected: " + "tcp://" + m_ip + ":" + m_port);
            while (m_isRunning)
            {
                m_mre.WaitOne();
                lock (_lock)
                {
                    // send controm messages
                    if (!m_controlMessages.IsEmpty)
                    {
                        Debug.Log("<color=blue>HALLO</color>");
                        try { sender.SendMultipartMessage(m_controlMessages); } catch (Exception e) { Debug.Log("<color=red> ERROR:controlMsg:SendFrame</color> " + e.ToString()); } // true not wait 
                        m_controlMessages.Clear();
                    }
                    // add parameter message to message buffer
                    if (m_modifiedParameters.Count > 0)
                    {
                        Debug.Log("<color=red>HALLO</color>");
                        m_parameterMessages.Append(createParameterMessage());
                        m_modifiedParameters.Clear();
                        m_modifiedParametersDataSize = 0;
                    }
                    // send message buffer if cout > packageSize or time is over
                    int frameCount = m_parameterMessages.FrameCount;
                    if (frameCount > packageSize || (i++ > packageSize && frameCount > 0))
                    {
                        Debug.Log("<color=yellow>HALLO</color>");
                        try { sender.SendMultipartMessage(m_parameterMessages); } catch (Exception e) { Debug.Log("<color=red> ERROR:modifiedParameter:SendFrame</color> " + e.ToString()); } // true not wait
                        m_parameterMessages.Clear();
                        i = 0;
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
        //! @param sender The TRACER _core.
        //! @param e Empty.
        //!
        private void sendParameterMessages(object sender, EventArgs e)
        {
            //if (m_modifiedParameters.Count > 0)
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
