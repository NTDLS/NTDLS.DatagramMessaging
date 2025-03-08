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
        /// //The thread that receives data for this connection.
        /// </summary>
        public Thread Thread { get; private set; }

        /// <summary>
        /// Creates a new DmContext instance.
        /// </summary>
        public DmContext(DatagramMessenger messenger, UdpClient client, Thread thread)
        {
            Messenger = messenger;
            Client = client;
            Thread = thread;
        }
    }
}
