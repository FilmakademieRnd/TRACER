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

//! @file "SceneManagerModule.cs"
//! @brief base implementation for scene manager modules
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 28.10.2021

using NetMQ;
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

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
            SYNC, RESENDUPDATE, // sync
            UNDOREDOADD, RESETOBJECT, // undo redo
            DATAHUB, // DataHub
            RPC // RPC
        }

        //!
        //! Enumeration defining DataHub message types.
        //!
        public enum DataHubMessageType
        {
            CONNECTIONSTATUS, ID, PING,
            SENDSCENE, REQUESTSCENE, SCENERECEIVED, FILEINFO,
            UNKNOWN = 255
        }

        //!
        //! Object for handling thread locking.
        //!
        protected readonly object m_lock = new object();

        //!
        //! ID address of the network interface to be used.
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
        //! Flag to deterine wether the workers inner loop has been left.
        //!
        protected TaskCompletionSource<bool> m_thredEnded;

        //!
        //! The Thread used for receiving or sending messages.
        //!
        private Thread m_transceiverThread;
        
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
        //! @param _core A reference to the TRACER _core.
        //!
        public NetworkManagerModule(string name, Manager manager) : base(name, manager)
        {
            m_mre = new ManualResetEvent(false);
            m_thredEnded = new TaskCompletionSource<bool>();
        }

        //!
        //! Destructor, cleaning up event registrations. 
        //!
        public override void Dispose() 
        {
            base.Dispose();
            stopThread();
        }

        //!
        //! Function to stop all tranceiver threads (called when TRACER _core will be destroyed).
        //!
        private void stopThread()
        {
            stop();
        }

        //!
        //! Function to start a new thread.
        //!
        //! @param ip ID address of the network interface.
        //! @param port Port number to be used.
        //!
        protected virtual void start(string ip, string port)
        {
            if (m_isRunning)
               stop();

            m_ip = ip;
            m_port = port;

            ThreadStart transeiver = new ThreadStart(run);
            m_transceiverThread = new Thread(transeiver);
            m_transceiverThread.Start();
            NetworkManager.threadCount++;
        }

        //!
        //! Stop the tranceiver.
        //!
        public void stop()
        {
            m_isRunning = false;
            m_mre.Set();
            
            if (m_socket != null)
            {
                while (m_thredEnded.Task.Result != true)
                    Thread.Yield();

                //m_socket.Disconnect("tcp://" + m_ip + ":" + m_port);
                m_socket.Dispose();
                m_socket.Close();
                Helpers.Log(this.name + " disposed.");
                m_socket = null;
            }

            if (m_transceiverThread != null)
            {
                m_transceiverThread.Abort();
                m_transceiverThread.Join();
                NetworkManager.threadCount--;
            }
        }
    }
}
