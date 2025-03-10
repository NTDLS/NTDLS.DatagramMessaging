using NTDLS.DatagramMessaging.Framing;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Contains information about the endpoint and the client.
    /// </summary>
    public class DmContext
    {
        /// <summary>
        /// This is the RPC server or client instance.
        /// </summary>
        public DatagramMessenger Messenger { get; private set; }

        /// <summary>
        /// The UDP client associated with this peer.
        /// </summary>
        public UdpClient Client { get; private set; }

        /// <summary>
        /// The thread that receives data for this connection.
        /// </summary>
        public Thread Thread { get; private set; }

        /// <summary>
        /// The IP endpoint which can be used to reply to the message.
        /// </summary>
        public IPEndPoint Endpoint { get; private set; }

        /// <summary>
        /// Creates a new DmContext instance.
        /// </summary>
        public DmContext(DatagramMessenger messenger, UdpClient client, ref IPEndPoint endpoint, Thread thread)
        {
            Endpoint = endpoint;
            Messenger = messenger;
            Client = client;
            Thread = thread;
        }

        internal void SetEndpoint(IPEndPoint endpoint)
        {
            Endpoint = endpoint;
        }

        /// <summary>
        /// Sends a return serialized message to the remote endpoint via NAT.
        /// </summary>
        public void WriteReplyMessage(IDmNotification payload)
        {
            if (Client == null) throw new Exception("The UDP client has not been initialized.");
            Client.WriteNotificationFrame(Messenger, Endpoint, payload,
                Messenger.GetSerializationProvider(), Messenger.GetCompressionProvider(), Messenger.GetCryptographyProvider());
        }

        /// <summary>
        /// Sends a frame containing the given bytes to the remote endpoint via NAT.
        /// </summary>
        public void WriteReplyBytes(byte[] payload)
        {
            if (Client == null) throw new Exception("The UDP client has not been initialized.");
            Client.WriteBytesFrame(Messenger, Endpoint, payload,
                Messenger.GetSerializationProvider(), Messenger.GetCompressionProvider(), Messenger.GetCryptographyProvider());
        }
    }
}
