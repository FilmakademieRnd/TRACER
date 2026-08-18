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

//! @file "NetMQTransport.cs"
//! @brief implementation of the network transport based on NetMQ
//! @version 0
//! @date 13.08.2026

using System;
using System.Collections.Generic;
using NetMQ;
using NetMQ.Sockets;

namespace tracer
{
    //!
    //! Class implementing the network transport used on all platforms except WebGL.
    //! This assembly is excluded from WebGL builds, so that neither NetMQ nor
    //! System.Net.Sockets is linked there. Desktop and Mobile keep the same NetMQ
    //! socket types and the same transceiver threads as before.
    //!
    public class NetMQTransport : ITracerTransport
    {
        public string name { get => "NetMQ (tcp)"; }
        public bool supportsThreads { get => true; }
        public bool supportsBind { get => true; }
        public string scheme { get => "tcp"; }

        public NetMQTransport()
        {
            // Required before any NetMQ socket is created on Mono/IL2CPP. Previously
            // called at the top of every module's run(); doing it once here covers all
            // of them and keeps the modules transport agnostic.
            AsyncIO.ForceDotNet.Force();
        }

        public ITracerSocket CreateSocket(TracerSocketType type)
        {
            switch (type)
            {
                case TracerSocketType.Subscriber:
                    return new NetMQTracerSocket(new SubscriberSocket());

                case TracerSocketType.Publisher:
                {
                    PublisherSocket publisher = new PublisherSocket();
                    // Preserved from UpdateSenderModule: never block shutdown on
                    // undelivered updates, and keep the pending-connection queue short.
                    publisher.Options.Linger = TimeSpan.FromMilliseconds(0);
                    publisher.Options.Backlog = 10;
                    return new NetMQTracerSocket(publisher);
                }

                case TracerSocketType.Request:    return new NetMQTracerSocket(new RequestSocket());
                case TracerSocketType.Response:   return new NetMQTracerSocket(new ResponseSocket());
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "unknown socket type");
            }
        }

        //!
        //! Release the global NetMQ context.
        //! Called when the TRACER core is destroyed.
        //!
        public void cleanup()
        {
            NetMQConfig.Cleanup(false);
        }
    }

    //!
    //! Class wrapping a NetMQSocket as an ITracerSocket.
    //!
    public class NetMQTracerSocket : ITracerSocket
    {
        private NetMQSocket m_socket;

        public NetMQTracerSocket(NetMQSocket socket)
        {
            m_socket = socket;
        }

        //!
        //! The wrapped NetMQ socket, for the cases that need NetMQ specific access.
        //!
        public NetMQSocket socket { get => m_socket; }

        public void Connect(string address)
        {
            m_socket.Connect(address);
        }

        public void Bind(string address)
        {
            m_socket.Bind(address);
        }

        public void SubscribeToAnyTopic()
        {
            if (m_socket is SubscriberSocket subscriber)
                subscriber.SubscribeToAnyTopic();
            else
                throw new InvalidOperationException("SubscribeToAnyTopic on a non subscriber socket");
        }

        public bool hasOut
        {
            get => m_socket.HasOut;
        }

        public void Send(byte[] frame)
        {
            m_socket.SendFrame(frame);
        }

        public bool TrySend(byte[] frame)
        {
            return m_socket.TrySendFrame(frame);
        }

        public void SendMultipart(List<byte[]> frames)
        {
            if (frames == null || frames.Count == 0)
                return;

            for (int i = 0; i < frames.Count - 1; i++)
                m_socket.SendMoreFrame(frames[i]);

            m_socket.SendFrame(frames[frames.Count - 1]);
        }

        public bool TryReceive(ref List<byte[]> frames, int timeoutMilliseconds)
        {
            return m_socket.TryReceiveMultipartBytes(
                TimeSpan.FromMilliseconds(timeoutMilliseconds), ref frames);
        }

        public void Dispose()
        {
            if (m_socket == null)
                return;

            m_socket.Dispose();
            m_socket = null;
        }
    }
}
