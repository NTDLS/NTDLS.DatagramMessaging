using NTDLS.DatagramMessaging.Framing;
using System;
using System.Net;
using System.Net.Sockets;

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
        public IDmMessenger Messenger { get; private set; }

        /// <summary>
        /// The UDP client associated with this peer.
        /// </summary>
        public UdpClient Client { get; private set; }

        /// <summary>
        /// The endpoint associated with this context.
        /// </summary>
        public IPEndPoint? Endpoint { get; private set; }

        /// <summary>
        /// Creates a new DmContext instance for a client that sends data to the given IP endpoint.
        /// </summary>
        public DmContext(IDmMessenger dmClient, UdpClient client, IPEndPoint? endpoint)
        {
            _compressionProvider = dmClient.GetCompressionProvider();
            _cryptographyProvider = dmClient.GetCryptographyProvider();

            Messenger = dmClient;
            Client = client;
            Endpoint = endpoint;
        }

        /// <summary>
        /// Sets the network endpoint associated with this instance.
        /// </summary>
        /// <param name="endpoint">The network endpoint to associate with this instance. Cannot be null.</param>
        internal void SetEndpoint(IPEndPoint endpoint)
        {
            Endpoint = endpoint;
        }

        #region IDmSerializationProvider.

        private IDmSerializationProvider? _serializationProvider = null;

        /// <summary>
        /// Sets the custom serialization provider that this client should use when sending/receiving data.
        /// Can be cleared by passing null or calling ClearSerializationProvider().
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

        private IDmCompressionProvider? _compressionProvider = null;

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

        private IDmCryptographyProvider? _cryptographyProvider = null;

        /// <summary>
        /// Sets the custom encryption provider that this client should use when sending/receiving data.
        /// Can be cleared by passing null or calling ClearCryptographyProvider().
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

        /// <summary>
        /// Sends a return serialized message to the remote endpoint via NAT.
        /// </summary>
        public void Dispatch(IDmDatagram datagram, IPEndPoint iPEndPoint)
        {
            if (Client == null) throw new Exception("The UDP client has not been initialized.");
            Client.Dispatch(this, datagram, iPEndPoint);
        }

        /// <summary>
        /// Sends a return serialized message to the remote endpoint via NAT.
        /// </summary>
        public void Dispatch(IDmDatagram datagram, string hostOrIPAddress, int port)
        {
            if (Client == null) throw new Exception("The UDP client has not been initialized.");
            Client.Dispatch(this, datagram, hostOrIPAddress, port);
        }

        /// <summary>
        /// Sends a return serialized message to the remote endpoint via NAT.
        /// </summary>
        public void Dispatch(IDmDatagram datagram)
        {
            if (Client == null) throw new Exception("The UDP client has not been initialized.");
            if (Endpoint == null) throw new Exception("The endpoint has not been set for this context.");
            Client.Dispatch(this, datagram, Endpoint);
        }

        /// <summary>
        /// Sends a frame containing the given bytes to the remote endpoint via NAT.
        /// </summary>
        public void Dispatch(byte[] bytes, IPEndPoint iPEndPoint)
        {
            if (Client == null) throw new Exception("The UDP client has not been initialized.");
            Client.Dispatch(this, bytes, iPEndPoint);
        }

        /// <summary>
        /// Sends a frame containing the given bytes to the remote endpoint via NAT.
        /// </summary>
        public void Dispatch(byte[] bytes, string hostOrIPAddress, int port)
        {
            if (Client == null) throw new Exception("The UDP client has not been initialized.");
            Client.Dispatch(this, bytes, hostOrIPAddress, port);
        }

        /// <summary>
        /// Sends a frame containing the given bytes to the remote endpoint via NAT.
        /// </summary>
        public void Dispatch(byte[] bytes)
        {
            if (Client == null) throw new Exception("The UDP client has not been initialized.");
            if (Endpoint == null) throw new Exception("The endpoint has not been set for this context.");
            Client.Dispatch(this, bytes, Endpoint);
        }
    }
}
