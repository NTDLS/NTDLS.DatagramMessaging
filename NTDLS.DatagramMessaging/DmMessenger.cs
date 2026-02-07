using NTDLS.DatagramMessaging.Framing;
using NTDLS.DatagramMessaging.Internal;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Wrapper for UdpClient. Set of classes and extensions methods that allow you to send/receive UDP packets with ease.
    /// </summary>
    public class DmMessenger
        : IDisposable
    {
        #region Backend variables.

        private bool _keepReceiveRunning = false;
        private Thread? _receiveThread = null;
        private readonly ConcurrentDictionary<IPEndPoint, DmContext> _endpointContexts = new();
        internal ReflectionCache ReflectionCache = new();

        #endregion

        #region Event: OnDatagramReceived.

        /// <summary>
        /// Event fired when a datagram is received from a client.
        /// </summary>
        public event DatagramReceivedHandler? OnDatagramReceived;

        /// <summary>
        /// Event fired when a datagram is received from a client.
        /// </summary>
        /// <param name="context">Information about the connection.</param>
        /// <param name="datagram">Interface containing the instance of the datagram class.</param>
        public delegate void DatagramReceivedHandler(DmContext context, IDmDatagram datagram);

        #endregion

        #region Event: OnKeepAliveReceived.

        /// <summary>
        /// Event fired when a keep-alive datagram is received.
        /// </summary>
        public event KeepAliveReceivedHandler? OnKeepAliveReceived;

        /// <summary>
        /// Event fired when a keep-alive datagram is received.
        /// </summary>
        /// <param name="context">Information about the connection.</param>
        /// <param name="keepAlive">Instance of the keep-alive class that was received.</param>
        public delegate void KeepAliveReceivedHandler(DmContext context, IDmKeepAliveDatagram keepAlive);

        /// <summary>
        /// Event fired when a keep-alive datagram is received.
        /// </summary>
        /// <param name="context">Information about the connection.</param>
        /// <param name="keepAlive">Instance of the keep-alive class that was received.</param>
        public void InvokeOnKeepAlive(DmContext context, IDmKeepAliveDatagram keepAlive)
            => OnKeepAliveReceived?.Invoke(context, keepAlive);

        #endregion

        #region Event: OnException

        /// <summary>
        /// Event fired when a keep-alive datagram is received.
        /// </summary>
        public event OnExceptionHander? OnException;

        /// <summary>
        /// Event fired when a keep-alive datagram is received.
        /// </summary>
        /// <param name="context">Information about the connection.</param>
        /// <param name="ex">information about the exception that occurred.</param>
        public delegate void OnExceptionHander(DmContext? context, Exception ex);

        /// <summary>
        /// Event fired when a keep-alive datagram is received.
        /// </summary>
        /// <param name="context">Information about the connection.</param>
        /// <param name="ex">information about the exception that occurred.</param>
        public void InvokeOnException(DmContext? context, Exception ex)
            => OnException?.Invoke(context, ex);

        #endregion

        #region IDmCompressionProvider.

        /// <summary>
        /// Gets the default compression provider used for compression and decompression operations.
        /// </summary>
        public IDmCompressionProvider? DefaultCompressionProvider { get; private set; }

        /// <summary>
        /// Sets the compression provider that this client should use when sending/receiving data.
        /// Can be cleared by passing null or calling ClearCompressionProvider().
        /// </summary>
        /// <param name="provider"></param>
        public void SetCompressionProvider(IDmCompressionProvider? provider)
        {
            DefaultCompressionProvider = provider;
        }

        /// <summary>
        /// Gets the current custom compression provider, if any.
        /// </summary>
        public IDmCompressionProvider? GetCompressionProvider()
            => DefaultCompressionProvider;

        /// <summary>
        /// Removes the compression provider set by a previous call to SetCompressionProvider().
        /// </summary>
        public void ClearCompressionProvider()
        {
            DefaultCompressionProvider = null;
        }

        #endregion

        #region IDmCryptographyProvider.

        /// <summary>
        /// Gets the default cryptography provider used for encryption and decryption operations.
        /// </summary>
        public IDmCryptographyProvider? DefaultCryptographyProvider { get; private set; }

        /// <summary>
        /// Sets the encryption provider that this client should use when sending/receiving data. Can be cleared by passing null or calling ClearCryptographyProvider().
        /// </summary>
        /// <param name="provider"></param>
        public void SetCryptographyProvider(IDmCryptographyProvider? provider)
        {
            DefaultCryptographyProvider = provider;
        }

        /// <summary>
        /// Gets the current custom cryptography provider, if any.
        /// </summary>
        public IDmCryptographyProvider? GetCryptographyProvider()
            => DefaultCryptographyProvider;

        /// <summary>
        /// Removes the encryption provider set by a previous call to SetCryptographyProvider().
        /// </summary>
        public void ClearCryptographyProvider()
        {
            DefaultCryptographyProvider = null;
        }

        #endregion

        #region Properties.

        /// <summary>
        /// Gets the network port number on which the server listens for incoming connections.
        /// </summary>
        public int ListenPort { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the listen port is currently bound and accepting connections.
        /// </summary>
        public bool IsListenPortBound { get; private set; }

        /// <summary>
        /// The amount of time that a context will be kept alive without receiving a keep-alive datagram before
        /// it is eligible for removal from the context cache. Default is 10 minutes, which is typically longer
        /// than most NAT timeouts to help ensure connectivity is maintained.
        /// </summary>
        public TimeSpan ContextKeepaliveExpiration { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Denotes whether the receive thread is active.
        /// </summary>
        public bool IsReceiveRunning => _keepReceiveRunning;

        /// <summary>
        /// When true, datagrams are queued in a thread pool.
        /// Otherwise, datagrams block other activities.
        /// </summary>
        public bool AsynchronousDatagramProcessing { get; set; } = true;

        /// <summary>
        /// Underlying native UDP client.
        /// </summary>
        public UdpClient UdpClient { get; private set; }

        #endregion

        #region Constructors.

        /// <summary>
        /// Instantiates a managed UDP client that sends data to the specified endpoint by default.
        /// </summary>
        public DmMessenger(int listenPort)
        {
            UdpClient = new UdpClient();
            Listen(listenPort);
            StartContextCustodian();
        }

        /// <summary>
        /// Instantiates a managed UDP client that sends data to the specified endpoint by default.
        /// </summary>
        public DmMessenger()
        {
            UdpClient = new UdpClient();
            StartContextCustodian();
        }

        #endregion

        #region Stop and Dispose

        private bool disposedValue;

        /// <summary>
        /// Closes the UDP manager, stops all listening threads.
        /// </summary>
        public void Stop(bool waitForCompletion = true)
        {
            try
            {
                if (_keepReceiveRunning)
                {
                    StopContextCustodian();

                    _keepReceiveRunning = false;
                    if (waitForCompletion)
                    {
                        _receiveThread?.Join();
                    }
                    _receiveThread = null;

                    try { UdpClient?.Close(); } catch { }
                    try { UdpClient?.Dispose(); } catch { }
                }
            }
            catch (Exception ex)
            {
                OnException?.Invoke(null, ex);
                throw;
            }
        }

        /// <summary>
        /// Releases the unmanaged resources used by the object and optionally releases the managed resources.
        /// </summary>
        /// <remarks>This method is called by both the public Dispose() method and the finalizer. When
        /// disposing is true, managed resources should be released. When disposing is false, only unmanaged resources
        /// should be released. Override this method to provide custom cleanup logic for derived classes.</remarks>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Stop(true);
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Releases all resources used by the DmMessenger instance.
        /// </summary>
        /// <remarks>Call this method when you are finished using the DmMessenger to free both managed and
        /// unmanaged resources. After calling Dispose, the instance should not be used further. This method suppresses
        /// finalization for the object, preventing the garbage collector from calling the finalizer.</remarks>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Context builders.

        internal DmContext? PeekEndpointContext(IPEndPoint endpoint)
        {
            if (_endpointContexts.TryGetValue(endpoint, out var context) && context != null)
            {
                return context;
            }

            return null;
        }

        /// <summary>
        /// Retrieves the context associated with the specified network endpoint, creating and caching it if necessary.
        /// </summary>
        /// <remarks>The returned context is cached with an expiration period similar to the typical NAT
        /// timeout. Calling this method will start a keep-alive process for the context to help maintain NAT
        /// traversal.</remarks>
        /// <param name="endpoint">The network endpoint for which to obtain the context. Cannot be null.</param>
        /// <returns>A <see cref="DmContext"/> instance representing the context for the specified endpoint.</returns>
        /// <exception cref="Exception">Thrown if the context cannot be instantiated for the specified endpoint.</exception>
        public DmContext GetEndpointContext(IPEndPoint endpoint)
        {
            var context = _endpointContexts.GetOrAdd(endpoint, (o) =>
            {
                var context = new DmContext(this, UdpClient, o);
                if (IsListenPortBound)
                {
                    context.StartKeepAlive();
                }
                else
                {
                    //No need to start keep-alives if we're not listening for incoming datagrams, since the context will only be
                    //used for sending datagrams to a specific endpoint and won't be receiving any datagrams that would require
                    //keep-alives to maintain NAT traversal.
                }
                return context;
            });

            return context;
        }

        /// <summary>
        /// Retrieves a context object for the specified network endpoint, resolving the given host name or IP address
        /// and port.
        /// </summary>
        /// <param name="hostOrIpAddress">The host name or IP address to resolve for the endpoint. Cannot be null or empty.</param>
        /// <param name="port">The port number associated with the endpoint. Must be in the range 0 to 65535.</param>
        /// <returns>A DmContext instance representing the resolved network endpoint.</returns>
        /// <exception cref="ArgumentException">Thrown if the host name or IP address cannot be resolved.</exception>
        public DmContext GetEndpointContext(string hostOrIpAddress, int port)
        {
            var addresses = Dns.GetHostAddresses(hostOrIpAddress);

            var ipAddress = addresses.FirstOrDefault(o => o.AddressFamily == AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault(o => o.AddressFamily == AddressFamily.InterNetworkV6)
                ?? throw new ArgumentException("Could not resolve IP address.", nameof(hostOrIpAddress));

            return GetEndpointContext(new IPEndPoint(ipAddress, port));
        }

        /// <summary>
        /// Retrieves the context associated with the specified IP address and port.
        /// </summary>
        /// <param name="ipAddress">The IP address of the endpoint for which to obtain the context.</param>
        /// <param name="port">The port number of the endpoint for which to obtain the context. Must be between 0 and 65535.</param>
        /// <returns>A <see cref="DmContext"/> representing the context for the specified endpoint.</returns>
        public DmContext GetEndpointContext(IPAddress ipAddress, int port)
        {
            return GetEndpointContext(new IPEndPoint(ipAddress, port));
        }

        #endregion

        #region DmContext passthrough.

        /// <summary>
        /// Sends a return serialized message to the remote endpoint defined when the client was created.
        /// </summary>
        public void Dispatch(IDmDatagram datagram, DmContext context)
            => context.Dispatch(datagram, context.Endpoint);

        /// <summary>
        /// Sends a frame containing the given bytes to the remote endpoint defined when the client was created.
        /// </summary>
        public void Dispatch(byte[] bytes, DmContext context)
            => context.Dispatch(bytes, context.Endpoint);

        /// <summary>
        /// Sends a return serialized message to the remote endpoint defined when the client was created.
        /// </summary>
        public void Dispatch(IDmDatagram datagram, IPEndPoint iPEndPoint)
            => GetEndpointContext(iPEndPoint).Dispatch(datagram, iPEndPoint);

        /// <summary>
        /// Sends a frame containing the given bytes to the remote endpoint defined when the client was created.
        /// </summary>
        public void Dispatch(byte[] bytes, IPEndPoint iPEndPoint)
            => GetEndpointContext(iPEndPoint).Dispatch(bytes, iPEndPoint);

        #endregion

        #region Context custodian.

        private Timer? _contextCustodianTimer;
        private int _custodianRunning;

        /// <summary>
        /// Sends transparent keep-alive messages to the server, default 10 seconds.
        /// This is primarily to satisfy NAT reply port timeouts.
        /// </summary>
        public void StartContextCustodian(TimeSpan? interval = null)
        {
            interval ??= TimeSpan.FromSeconds(10);

            if (_contextCustodianTimer != null)
                return;

            _contextCustodianTimer = new Timer(_ =>
                {

                    if (Interlocked.Exchange(ref _custodianRunning, 1) == 1)
                        return; // Prevent overlapping executions

                    try
                    {
                        var now = DateTime.UtcNow;

                        foreach (var kvp in _endpointContexts)
                        {
                            var endpoint = kvp.Key;
                            var context = kvp.Value;

                            if (context.IsShutdown)
                            {
                                _endpointContexts.TryRemove(endpoint, out var _);
                                continue;
                            }

                            var age = now - context.LastActivityUTC;
                            if (age > ContextKeepaliveExpiration)
                            {
                                context.Shutdown();
                                _endpointContexts.TryRemove(endpoint, out var _);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        OnException?.Invoke(null, ex);
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _custodianRunning, 0);
                    }
                }, null, interval.Value, interval.Value);
        }

        /// <summary>
        /// Stops the keep-alive thread, if its running.
        /// </summary>
        public void StopContextCustodian()
        {
            try
            {
                _contextCustodianTimer?.Dispose();
                _contextCustodianTimer = null;
            }
            catch (Exception ex)
            {
                OnException?.Invoke(null, ex); throw;
            }
        }

        #endregion

        #region Listen, Bind and Ports.

        /// <summary>
        /// Gets a random and unused port number that can be used for listening.
        /// </summary>
        public static int GetRandomUnusedUdpPort()
            => GetRandomUnusedUdpPort(1, 65534);

        /// <summary>
        /// Gets a random and unused port number that can be used for listening.
        /// </summary>
        public static int GetRandomUnusedUdpPort(int minPort, int maxPort)
        {
            for (int i = 0; i < 1000; i++)
            {
                int port = Random.Shared.Next(minPort, maxPort + 1);
                if (IsPortAvailable(port))
                {
                    return port;
                }
            }

            throw new Exception("Could not find an available port.");
        }

        /// <summary>
        /// Determines if a given UDP port is in use.
        /// </summary>
        public static bool IsPortAvailable(int port)
        {
            try
            {
                using var udpClient = new UdpClient(port);
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        /// <summary>
        /// Instantiates a managed UDP instance without a defined destination endpoint.
        /// </summary>
        private void Listen(int listenPort)
        {
            try
            {
                ListenPort = listenPort;

                var frameBuffer = new FrameBuffer();
                var serverEndpoint = new IPEndPoint(IPAddress.Any, listenPort);

                UdpClient.Client.Bind(serverEndpoint);
                IsListenPortBound = true;

                // Disable the ICMP "Port Unreachable" messages that cause SocketExceptions when sending datagrams to
                // endpoints that are not listening. We will handle this ourselves by ignoring exceptions on receive
                // and treating them as a sign that the remote endpoint is unreachable.
                const int SIO_UDP_CONNRESET = -1744830452; // 0x9800000C
                UdpClient.Client.IOControl(
                    (IOControlCode)SIO_UDP_CONNRESET,
                    [0], // false = disable SIO_UDP_CONNRESET exceptions
                    null
                );

                _keepReceiveRunning = true;

                _receiveThread = new Thread(o =>
                {
                    while (_keepReceiveRunning)
                    {
                        try
                        {
                            while (_keepReceiveRunning && UdpClient.ReadAndProcessFrames(serverEndpoint, this, frameBuffer))
                            {
                            }
                        }
                        catch (Exception ex)
                        {
                            OnException?.Invoke(null, ex);
                        }
                    }
                })
                {
                    IsBackground = true,
                    Name = $"DmMessenger Receive Thread (Port {listenPort})"
                };

                _receiveThread.Start();
            }
            catch (Exception ex)
            {
                OnException?.Invoke(null, ex);
                throw;
            }
        }

        #endregion

        #region Message routing.

        /// <summary>
        /// Adds a class that contains datagram handler functions.
        /// </summary>
        public void AddHandler(IDmDatagramHandler handler)
        {
            ReflectionCache.AddInstance(handler);
        }

        /// <summary>
        /// Routes inbound packets to the appropriate handler.
        /// </summary>
        public void ProcessFrameDatagramByConvention(DmContext context, IDmDatagram datagram)
        {
            try
            {
                //First we try to invoke functions that match the signature, if that fails we will fall back to invoking the OnDatagramReceived() event.
                if (ReflectionCache.RouteToDatagramHander(context, datagram))
                {
                    return; //Datagram was handled by handler routing.
                }

                OnDatagramReceived?.Invoke(context, datagram);
            }
            catch (Exception ex)
            {
                OnException?.Invoke(null, ex);
                throw;
            }
        }

        #endregion
    }
}
