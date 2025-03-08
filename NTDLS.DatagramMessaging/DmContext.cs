using System.Net.Sockets;
using System.Threading;

namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Contains information about the endpoint and the connection.
    /// </summary>
    public class DmContext
    {
        /// <summary>
        /// This is the RPC server or client instance.
        /// </summary>
        public DmMessenger Endpoint { get; set; }

        /// <summary>
        /// The UDP client associated with this peer.
        /// </summary>
        public UdpClient UdpClient { get; set; }

        /// <summary>
        /// //The thread that receives data for this connection.
        /// </summary>
        public Thread Thread { get; set; }

        /// <summary>
        /// Creates a new ReliableMessagingContext instance.
        /// </summary>
        public DmContext(DmMessenger endpoint, UdpClient udpClient, Thread thread)
        {
            Endpoint = endpoint;
            UdpClient = udpClient;
            Thread = thread;
        }
    }
}
