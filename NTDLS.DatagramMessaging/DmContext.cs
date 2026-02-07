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
        /// Gets a value indicating whether the component has been shut down.
        /// </summary>
        public bool IsShutdown { get; private set; } = false;

        /// <summary>
        /// This is the RPC server or client instance.
        /// </summary>
        public DmMessenger Messenger { get; private set; }

        /// <summary>
        /// The UDP client associated with this peer.
        /// </summary>
        public UdpClient Client { get; private set; }

        /// <summary>
        /// The endpoint associated with this context.
        /// </summary>
        public IPEndPoint Endpoint { get; private set; }

        /// <summary>
        /// Gets or sets the round-trip time, in milliseconds, for a network operation.
        /// </summary>
        public double RoundTripTimeMilliseconds { get; internal set; }

        /// <summary>
        /// Gets the timestamp (UTC), when the most recent message was sent/received.
        /// This is used to close the context if the remote endpoint becomes unresponsive which
        /// is typically a sign that the remote endpoint has disconnected or is no longer reachable.
        /// </summary>
        public DateTime LastActivityUTC { get; internal set; } = DateTime.UtcNow;

        /// <summary>
        /// Creates a new DmContext instance for a client that sends data to the given IP endpoint.
        /// </summary>
        public DmContext(DmMessenger messenger, UdpClient client, IPEndPoint endpoint)
        {
            _compressionProvider = messenger.GetCompressionProvider();
            _cryptographyProvider = messenger.GetCryptographyProvider();

            Messenger = messenger;
            Client = client;
            Endpoint = endpoint;
        }

        internal void Shutdown()
        {
            IsShutdown = true;
            StopKeepAlive();
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

        #region Dispatch.

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

        #endregion

        #region Keep-alive.

        private readonly Lock _keepAliveLock = new();
        private Timer? _keepAliveTimer;

        /// <summary>
        /// Sends transparent keep-alive messages to the server, default 10 seconds.
        /// This is primarily to satisfy NAT reply port timeouts.
        /// </summary>
        public void StartKeepAlive(TimeSpan? interval = null)
        {
            interval ??= TimeSpan.FromSeconds(10);

            lock (_keepAliveLock)
            {
                if (_keepAliveTimer != null)
                    return;

                _keepAliveTimer = new Timer(_ =>
                {
                    try
                    {
                        Dispatch(new DmKeepAliveDatagram(), Endpoint);
                    }
                    catch (Exception ex)
                    {
                        Messenger.InvokeOnException(this, ex);
                    }
                }, null, interval.Value, interval.Value);
            }
        }

        /// <summary>
        /// Stops the keep-alive thread, if its running.
        /// </summary>
        public void StopKeepAlive()
        {
            try
            {
                _keepAliveTimer?.Dispose();
                _keepAliveTimer = null;
            }
            catch (Exception ex)
            {
                Messenger.InvokeOnException(this, ex);
                throw;
            }
        }

        #endregion
    }
}
