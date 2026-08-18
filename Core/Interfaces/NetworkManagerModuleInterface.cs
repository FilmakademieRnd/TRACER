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

//! @file "NetworkManagerModuleInterface.cs"
//! @brief base implementation for network manager modules
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 28.10.2021

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            SENDSCENE, REQUESTSCENE, FILEINFO,
            UNKNOWN = 255
        }

        //!
        //! Object for handling thread locking.
        //!
        protected readonly object m_lock = new object();

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
        //! Flag to deterine wether the workers inner loop has been left.
        //!
        protected TaskCompletionSource<bool> m_thredEnded;

        //!
        //! The Thread used for receiving or sending messages.
        //! Null if the transport has no threads, in which case transceive is called
        //! from the main thread with every global tick instead.
        //!
        protected Thread m_transceiverThread;

        //!
        //! Reset event for stopping and resetting the run thread.
        //!
        protected ManualResetEvent m_mre;

        //!
        //! The socket, created by the transport this build was compiled with.
        //!
        protected ITracerSocket m_socket;

        //!
        //! Whether this build runs the transceiver on its own thread.
        //! False for WebGL, which is single threaded.
        //!
        protected static bool threaded
        {
            get => TracerTransport.current.supportsThreads;
        }

        //!
        //! The time in milliseconds a receive is allowed to block.
        //! Threaded builds wait on the socket, single threaded builds must return
        //! immediately so that the current frame can continue.
        //!
        protected static int receiveTimeout
        {
            get => threaded ? 1000 : 0;
        }

        //!
        //! Function creating and connecting the module's socket.
        //! Called once before the first call to transceive.
        //!
        protected virtual void createSocket() { }

        //!
        //! Function performing a single, non blocking send and receive step.
        //! Called repeatedly by run on the transceiver thread, or once per global
        //! tick on the main thread if the transport has no threads.
        //!
        protected virtual void transceive() { }

        //!
        //! Function, sending and receiving messages until the module is stopped
        //! (executed in a separate thread).
        //! Modules that perform a one time sequence instead of a loop, like the
        //! scene receiver, override this function completely.
        //!
        protected virtual void run()
        {
            m_isRunning = true;
            createSocket();

            while (m_isRunning)
            {
                transceive();
                Thread.Yield();
            }

            m_thredEnded.TrySetResult(true);
        }

        //!
        //! Function receiving a single frame, discarding any further frames of the
        //! same message. Used by the scene modules, that exchange one frame at a time.
        //!
        //! @param timeoutMilliseconds The time to wait for a frame. 0 does not block.
        //! @return The received frame, or null if no frame arrived in time.
        //!
        protected byte[] receiveFrame(int timeoutMilliseconds)
        {
            if (m_socket == null)
                return null;

            List<byte[]> frames = new List<byte[]>();
            if (m_socket.TryReceive(ref frames, timeoutMilliseconds) && frames.Count > 0)
                return frames[0];

            return null;
        }

        //!
        //! Function calling transceive with every global tick, used if the transport
        //! has no threads.
        //!
        //! @param sender The TRACER core.
        //! @param args Empty.
        //!
        private void transceiveOnTick(object sender, EventArgs args)
        {
            if (!m_isRunning)
                return;

            try
            {
                transceive();
            }
            catch (Exception e)
            {
                Helpers.Log(name + " transceive failed: " + e.Message, Helpers.logMsgType.WARNING);
            }
        }

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
        //! Function to start the tranceiver, on a new thread if the transport
        //! supports threads, otherwise on the main thread.
        //!
        //! @param ip IP address of the network interface.
        //! @param port Port number to be used.
        //!
        protected virtual void start(string ip, string port)
        {
            if (m_isRunning)
               stop();

            m_ip = ip;
            m_port = port;

            if (threaded)
            {
                ThreadStart transeiver = new ThreadStart(run);
                m_transceiverThread = new Thread(transeiver);
                m_transceiverThread.Start();
                NetworkManager.threadCount++;
            }
            else
            {
                m_isRunning = true;
                createSocket();
                core.timeEvent += transceiveOnTick;
            }
        }

        //!
        //! Stop the tranceiver.
        //!
        public void stop()
        {
            bool wasRunning = m_isRunning;
            m_isRunning = false;
            m_mre.Set();

            if (!threaded)
            {
                if (wasRunning)
                    core.timeEvent -= transceiveOnTick;

                if (m_socket != null)
                {
                    m_socket.Dispose();
                    Helpers.Log(this.name + " disposed.");
                    m_socket = null;
                }
                return;
            }

            if (m_socket != null)
            {
                // waiting on a task would never return without a second thread,
                // so this branch is only reached if the transport has threads
                while (m_thredEnded.Task.Result != true)
                    Thread.Yield();

                //m_socket.Disconnect("tcp://" + m_ip + ":" + m_port);
                m_socket.Dispose();
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
