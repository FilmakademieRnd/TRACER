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

//! @file "SceneManagerModule.cs"
//! @brief base implementation for scene manager modules
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 28.10.2021

using NetMQ;
using System;
using System.Diagnostics;
using System.Threading;

namespace tracer
{
    //!
    //! class for scene manager modules
    //!
    public abstract class NetworkManagerModule : Module
    {
        //!
        //! Enumeration defining TRACER message types.
        //!
        public enum MessageType
        {
            PARAMETERUPDATE, LOCK, // node
            SYNC, PING, RESENDUPDATE, // sync
            UNDOREDOADD, RESETOBJECT, // undo redo
            DATAHUB, // DataHub
            RPC // RPC
        }


        //!
        //! IP address of the network interface to be used.
        //!
        protected string m_ip;

        //!
        //! Port number to be used.
        //!
        protected string m_port;

        //!
        //! Flag specifing if the thread should stop running.
        //!
        protected bool m_isRunning;

        //!
        //! Action emitted when worker thread has been disposed
        //!
        protected Action m_disposed;

        //!
        //! The Thread used for receiving or sending messages.
        //!
        private Thread m_transeiverThread;
        
        //!
        //! Function, listening for messages and adds them to m_messageQueue (executed in separate thread).
        //!
        protected abstract void run();

        //!
        //! Reset event for stopping and resetting the run thread.
        //!
        protected ManualResetEvent m_mre;

        protected NetMQSocket m_socket;

        //!
        //! Ret the manager of this module.
        //!
        public NetworkManager manager
        {
            get => (NetworkManager) m_manager;
        }

        //!
        //! constructor
        //! @param  name  The  name of the module.
        //! @param core A reference to the TRACER core.
        //!
        public NetworkManagerModule(string name, Manager manager) : base(name, manager) 
        {
            m_mre = new ManualResetEvent(false);

            manager.cleanupEvent += stopThread;
            m_disposed += this.manager.NetMQCleanup;
        }

        //!
        //! Destructor, cleaning up event registrations. 
        //!
        public override void Dispose() 
        {
            base.Dispose();
            manager.cleanupEvent -= stopThread;
            //m_disposed -= manager.NetMQCleanup;
        }

        //!
        //! Function to stop all tranceiver threads (called when TRACER core will be destroyed).
        //!
        private void stopThread(object sender, EventArgs e)
        {
            stop();
        }

        //!
        //! Function to start a new thread.
        //!
        //! @param ip IP address of the network interface.
        //! @param port Port number to be used.
        //!
        protected virtual void start(string ip, string port)
        {
            stop();

            m_ip = ip;
            m_port = port;

            ThreadStart transeiver = new ThreadStart(run);
            m_transeiverThread = new Thread(transeiver);
            m_transeiverThread.Start();
            NetworkManager.threadCount++;
        }

        //!
        //! Stop the transeiver.
        //!
        public void stop()
        {
            m_isRunning = false;
            m_mre.Set();
            if (m_socket != null)
            {
                    m_socket.Disconnect("tcp://" + m_ip + ":" + m_port);
                    //m_socket.Unbind("tcp://" + m_ip + ":" + m_port);
                    m_socket.Close();
                    m_socket.Dispose();
                    Helpers.Log(this.name + " disposed.");
                    m_disposed?.Invoke();
            }
        }
    }
}
