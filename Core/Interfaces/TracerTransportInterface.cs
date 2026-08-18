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

//! @file "TracerTransportInterface.cs"
//! @brief base implementation for network transports
//! @version 0
//! @date 13.08.2026

using System;
using System.Collections.Generic;

namespace tracer
{
    //!
    //! The ZeroMQ socket patterns TRACER uses.
    //!
    public enum TracerSocketType
    {
        Subscriber,   //!< receives broadcasts from the DataHub (port 5556)
        Publisher,    //!< sends updates to the DataHub (port 5557)
        Request,      //!< command channel and scene requests (ports 5558, 5555)
        Response      //!< serves scene requests (port 5555), never available in a browser
    }

    //!
    //! Interface for a single network socket, reduced to the operations TRACER uses.
    //! Receiving takes a timeout instead of blocking indefinitely, so that the same
    //! module code can run on a transceiver thread (timeout > 0) or on the main
    //! thread with every global tick (timeout 0).
    //!
    public interface ITracerSocket : IDisposable
    {
        //!
        //! Connect the socket to a remote endpoint.
        //!
        //! @param address The endpoint, e.g. "tcp://1.2.3.4:5556" or "ws://1.2.3.4:5566".
        //!
        void Connect(string address);

        //!
        //! Bind the socket to a local endpoint.
        //! Not supported by transports that cannot listen for inbound connections.
        //!
        //! @param address The endpoint to bind, e.g. "tcp://1.2.3.4:5555".
        //!
        void Bind(string address);

        //!
        //! Subscribe to all topics. Valid for subscriber sockets only.
        //! The DataHub does not use topic frames.
        //!
        void SubscribeToAnyTopic();

        //!
        //! Whether the socket currently accepts a send.
        //! False for a request socket while a reply is still outstanding, because
        //! request and reply sockets are strictly alternating.
        //!
        bool hasOut { get; }

        //!
        //! Send a single frame.
        //!
        //! @param frame The frame to be sent.
        //!
        void Send(byte[] frame);

        //!
        //! Send a single frame without throwing if the socket cannot send.
        //!
        //! @param frame The frame to be sent.
        //! @return True if the frame was sent.
        //!
        bool TrySend(byte[] frame);

        //!
        //! Send several frames as one multipart message.
        //!
        //! @param frames The frames to be sent.
        //!
        void SendMultipart(List<byte[]> frames);

        //!
        //! Try to receive one multipart message.
        //!
        //! @param frames Filled with the received frames, untouched if nothing arrived.
        //! @param timeoutMilliseconds The time to wait for a message. 0 does not block.
        //! @return True if a message was received.
        //!
        bool TryReceive(ref List<byte[]> frames, int timeoutMilliseconds);
    }

    //!
    //! Interface for a network transport, creating sockets and describing what the
    //! networking of the current platform is capable of.
    //!
    public interface ITracerTransport
    {
        //!
        //! The name of the transport, used for logging.
        //!
        string name { get; }

        //!
        //! Whether the transport can be run on a transceiver thread.
        //! False for WebGL, where all sockets are served from the main thread.
        //!
        bool supportsThreads { get; }

        //!
        //! Whether the transport can bind a local endpoint.
        //! False for WebGL, where a client cannot accept inbound connections.
        //!
        bool supportsBind { get; }

        //!
        //! The URL scheme of the transport, "tcp" or "ws".
        //!
        string scheme { get; }

        //!
        //! Create a new socket.
        //!
        //! @param type The socket pattern to be created.
        //! @return The created socket.
        //!
        ITracerSocket CreateSocket(TracerSocketType type);

        //!
        //! Release all global state held by the transport.
        //! Called when the TRACER core is destroyed, after all sockets are disposed.
        //!
        void cleanup();
    }

    //!
    //! Class providing the network transport compiled into this build.
    //! Exactly one implementation of ITracerTransport is expected to be present,
    //! the NetMQ one on Desktop and Mobile, the WebSocket one on WebGL. It is
    //! looked up by reflection instead of being referenced directly, so that the
    //! Core assembly stays free of any networking dependency and can be built for
    //! platforms that cannot link NetMQ.
    //!
    public static class TracerTransport
    {
        private static ITracerTransport m_current;

        //!
        //! The transport of this build.
        //!
        public static ITracerTransport current
        {
            get
            {
                if (m_current == null)
                    m_current = discover();
                return m_current;
            }
        }

        //!
        //! Set the transport explicitly instead of looking it up. Used for testing.
        //!
        //! @param transport The transport to be used.
        //!
        public static void setTransport(ITracerTransport transport)
        {
            m_current = transport;
        }

        //!
        //! Build an endpoint string using the scheme of the current transport.
        //!
        //! @param ip The IP address of the endpoint.
        //! @param port The port number of the endpoint.
        //! @return The endpoint, e.g. "tcp://1.2.3.4:5556".
        //!
        public static string endpoint(string ip, string port)
        {
            return current.scheme + "://" + ip + ":" + port;
        }

        private static ITracerTransport discover()
        {
            // Helpers.GetAllTypes matches on IsSubclassOf, which is never true for an
            // interface, so the scan is done here instead.
            Type target = typeof(ITracerTransport);

            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException e)
                {
                    // A partially loadable assembly still may hold the transport.
                    types = Array.FindAll(e.Types, t => t != null);
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (Type t in types)
                {
                    if (t.IsAbstract || t.IsInterface || !target.IsAssignableFrom(t))
                        continue;

                    try
                    {
                        ITracerTransport transport = (ITracerTransport)Activator.CreateInstance(t);
                        Helpers.Log("Network transport: " + transport.name);
                        return transport;
                    }
                    catch (Exception e)
                    {
                        Helpers.Log("Could not instantiate transport " + t.Name + ": " + e.Message,
                                    Helpers.logMsgType.WARNING);
                    }
                }
            }

            throw new InvalidOperationException(
                "No ITracerTransport implementation found in this build. Desktop and Mobile " +
                "builds need the NetMQ transport; WebGL builds need the WebSocket transport.");
        }
    }
}
