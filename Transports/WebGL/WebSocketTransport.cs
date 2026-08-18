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

//! @file "WebSocketTransport.cs"
//! @brief implementation of the network transport based on jszmq
//! @version 0
//! @date 13.08.2026

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace tracer
{
    //!
    //! Class implementing the network transport used on WebGL.
    //! A browser cannot open the raw TCP socket that the tcp:// transport of ZeroMQ
    //! requires, therefore the WebSocket transport of ZeroMQ (ZWS 2.0) is used. The
    //! DataHub has to bind ws:// endpoints in addition to its tcp:// ones, which a
    //! single ZeroMQ socket supports, so that native clients stay unaffected.
    //! The sockets themselves are held in JavaScript (Plugins/TracerZmq.jslib),
    //! because WebGL provides neither System.Net.Sockets nor usable threading. All
    //! code here runs on the main thread, called with every global tick.
    //!
    public class WebSocketTransport : ITracerTransport
    {
        public string name { get => "jszmq (ws)"; }

        //! WebGL is single threaded, all modules are served from the main thread.
        public bool supportsThreads { get => false; }

        //! A browser cannot listen for inbound connections.
        public bool supportsBind { get => false; }

        public string scheme { get => "ws"; }

        public ITracerSocket CreateSocket(TracerSocketType type)
        {
            return new WebSocketTracerSocket(type);
        }

        public void cleanup()
        {
            // Nothing global to release; each socket closes itself on Dispose.
        }
    }

    //!
    //! Class wrapping a jszmq socket, addressed by an integer handle that is held
    //! on the JavaScript side.
    //!
    public class WebSocketTracerSocket : ITracerSocket
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int TracerZmq_Create(int type);
        [DllImport("__Internal")] private static extern int TracerZmq_Connect(int handle, string address);
        [DllImport("__Internal")] private static extern int TracerZmq_Subscribe(int handle);
        [DllImport("__Internal")] private static extern int TracerZmq_HasOut(int handle);
        [DllImport("__Internal")] private static extern int TracerZmq_AddFrame(int handle, byte[] data, int length);
        [DllImport("__Internal")] private static extern int TracerZmq_Flush(int handle);
        [DllImport("__Internal")] private static extern int TracerZmq_PeekFrameCount(int handle);
        [DllImport("__Internal")] private static extern int TracerZmq_PeekFrameSize(int handle, int index);
        [DllImport("__Internal")] private static extern int TracerZmq_CopyFrame(int handle, int index, byte[] buffer, int maxLength);
        [DllImport("__Internal")] private static extern int TracerZmq_PopMessage(int handle);
        [DllImport("__Internal")] private static extern void TracerZmq_Close(int handle);
#else
        // Stubs so the assembly still compiles when opened in the Editor, where the
        // WebGL platform is selected but scripts run against the Editor runtime.
        private static int TracerZmq_Create(int type) => 0;
        private static int TracerZmq_Connect(int handle, string address) => 0;
        private static int TracerZmq_Subscribe(int handle) => 0;
        private static int TracerZmq_HasOut(int handle) => 0;
        private static int TracerZmq_AddFrame(int handle, byte[] data, int length) => 0;
        private static int TracerZmq_Flush(int handle) => 0;
        private static int TracerZmq_PeekFrameCount(int handle) => -1;
        private static int TracerZmq_PeekFrameSize(int handle, int index) => -1;
        private static int TracerZmq_CopyFrame(int handle, int index, byte[] buffer, int maxLength) => -1;
        private static int TracerZmq_PopMessage(int handle) => 0;
        private static void TracerZmq_Close(int handle) { }
#endif

        private int m_handle;
        private readonly TracerSocketType m_type;

        public WebSocketTracerSocket(TracerSocketType type)
        {
            m_type = type;

            if (type == TracerSocketType.Response)
                throw new NotSupportedException(
                    "A browser cannot bind a Response socket. SceneServerModule must not " +
                    "be loaded in WebGL builds.");

            m_handle = TracerZmq_Create(socketTypeId(type));

            if (m_handle == 0)
                throw new InvalidOperationException(
                    "Could not create a jszmq socket. Check that the vendored jszmq bundle " +
                    "(Plugins/jszmq.jspre) is included in the build.");
        }

        private static int socketTypeId(TracerSocketType type)
        {
            switch (type)
            {
                case TracerSocketType.Subscriber: return 0;
                case TracerSocketType.Publisher:  return 1;
                case TracerSocketType.Request:    return 2;
                case TracerSocketType.Response:   return 3;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, "unknown socket type");
            }
        }

        public void Connect(string address)
        {
            if (TracerZmq_Connect(m_handle, address) == 0)
                Helpers.Log("Could not connect to " + address, Helpers.logMsgType.ERROR);
        }

        public void Bind(string address)
        {
            throw new NotSupportedException("A browser cannot listen for inbound connections.");
        }

        public void SubscribeToAnyTopic()
        {
            TracerZmq_Subscribe(m_handle);
        }

        public bool hasOut
        {
            get => TracerZmq_HasOut(m_handle) != 0;
        }

        public void Send(byte[] frame)
        {
            TracerZmq_AddFrame(m_handle, frame, frame.Length);
            TracerZmq_Flush(m_handle);
        }

        public bool TrySend(byte[] frame)
        {
            if (!hasOut)
                return false;

            TracerZmq_AddFrame(m_handle, frame, frame.Length);
            return TracerZmq_Flush(m_handle) != 0;
        }

        public void SendMultipart(List<byte[]> frames)
        {
            if (frames == null || frames.Count == 0)
                return;

            for (int i = 0; i < frames.Count; i++)
                TracerZmq_AddFrame(m_handle, frames[i], frames[i].Length);

            TracerZmq_Flush(m_handle);
        }

        //!
        //! Receive the oldest queued message.
        //! The timeout is ignored, because nothing may block the current frame, so
        //! this never waits. Modules pass 0 on WebGL anyway.
        //!
        //! @param frames Filled with the received frames, untouched if nothing arrived.
        //! @param timeoutMilliseconds Ignored on this transport.
        //! @return True if a message was received.
        //!
        public bool TryReceive(ref List<byte[]> frames, int timeoutMilliseconds)
        {
            int frameCount = TracerZmq_PeekFrameCount(m_handle);
            if (frameCount < 0)
                return false;

            if (frames == null)
                frames = new List<byte[]>(frameCount);
            else
                frames.Clear();

            for (int i = 0; i < frameCount; i++)
            {
                int size = TracerZmq_PeekFrameSize(m_handle, i);
                if (size < 0)
                {
                    TracerZmq_PopMessage(m_handle);
                    return false;
                }

                byte[] frame = new byte[size];
                if (size > 0 && TracerZmq_CopyFrame(m_handle, i, frame, size) < 0)
                {
                    TracerZmq_PopMessage(m_handle);
                    return false;
                }

                frames.Add(frame);
            }

            TracerZmq_PopMessage(m_handle);
            return true;
        }

        public void Dispose()
        {
            if (m_handle == 0)
                return;

            TracerZmq_Close(m_handle);
            m_handle = 0;
        }
    }
}
