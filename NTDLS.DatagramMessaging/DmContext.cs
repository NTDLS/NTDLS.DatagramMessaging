using System.Net.Sockets;
using System.Threading;

namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Contains information about the endpoint and the connection.
    /// </summary>
    public class DmContext
    {
        private IDmSerializationProvider? _serializationProvider = null;
        private IDmCompressionProvider? _compressionProvider = null;
        private IDmCryptographyProvider? _cryptographyProvider = null;

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
        /// <param name="endpoint"></param>
        /// <param name="udpClient"></param>
        /// <param name="thread"></param>
        public DmContext(DmMessenger endpoint, UdpClient udpClient, Thread thread)
        {
            Endpoint = endpoint;
            UdpClient = udpClient;
            Thread = thread;
        }

        #region IDmSerializationProvider.

        /// <summary>
        /// Sets the custom serialization provider that this client should use when sending/receiving data. Can be cleared by passing null or calling ClearSerializationProvider().
        /// </summary>
        public void SetSerializationProvider(IDmSerializationProvider? provider)
            => _serializationProvider = provider;

        /// Removes the custom serialization provider set by a previous call to SetSerializationProvider().
        public void ClearSerializationProvider()
            => _serializationProvider = null;

        /// <summary>
        /// Gets the current custom serialization provider, if any.
        /// </summary>
        public IDmSerializationProvider? GetSerializationProvider()
            => _serializationProvider;

        #endregion

        #region IDmCompressionProvider.

        /// <summary>
        /// Sets the custom compression provider that this client should use when sending/receiving data. Can be cleared by passing null or calling ClearCompressionProvider().
        /// </summary>
        public void SetCompressionProvider(IDmCompressionProvider? provider)
            => _compressionProvider = provider;

        /// Removes the custom compression provider set by a previous call to SetCompressionProvider().
        public void ClearCompressionProvider()
            => _compressionProvider = null;

        /// <summary>
        /// Gets the current custom compression provider, if any.
        /// </summary>
        public IDmCompressionProvider? GetCompressionProvider()
            => _compressionProvider;

        #endregion

        #region IDmCryptographyProvider.

        /// <summary>
        /// Sets the custom encryption provider that this client should use when sending/receiving data. Can be cleared by passing null or calling ClearCryptographyProvider().
        /// </summary>
        public void SetCryptographyProvider(IDmCryptographyProvider? provider)
            => _cryptographyProvider = provider;

        /// <summary>
        /// Removes the custom encryption provider set by a previous call to SetCryptographyProvider().
        /// </summary>
        public void ClearCryptographyProvider()
            => _cryptographyProvider = null;

        /// <summary>
        /// Gets the current custom cryptography provider, if any.
        /// </summary>
        public IDmCryptographyProvider? GetCryptographyProvider()
            => _cryptographyProvider;

        #endregion
    }
}
