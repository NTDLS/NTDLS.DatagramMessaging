using NTDLS.DatagramMessaging.Framing;
using NTDLS.DatagramMessaging.Internal;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Wrapper for UdpClient. Set of classes and extensions methods that allow you to send/receive UDP packets with ease.
    /// </summary>
    public class DmClient :
        IDmMessenger
    {
        private bool _keepReceiveRunning = false;
        private bool _keepKeepAliveRunning = false;
        private Thread? _receiveThread = null;
        private Thread? _keepAliveThread = null;

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
            Context?.SetCompressionProvider(provider);
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
            Context?.SetCryptographyProvider(null);
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
            Context?.SetCryptographyProvider(provider);
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
            Context?.SetCryptographyProvider(null);
        }

        #endregion

        /// <summary>
        /// Denotes whether the receive thread is active.
        /// </summary>
        public bool IsReceiveRunning { get { return _keepReceiveRunning; } }

        /// <summary>
        /// Denotes whether the keep-alive is active.
        /// </summary>
        public bool IsKeepAliveRunning { get { return _keepKeepAliveRunning; } }

        /// <summary>
        /// When true, datagrams are queued in a thread pool.
        /// Otherwise, datagrams block other activities.
        /// </summary>
        public bool AsynchronousDatagramProcessing { get; set; } = true;

        /// <summary>
        /// Underlying native UDP client.
        /// </summary>
        public UdpClient? Client { get; private set; }

        /// <summary>
        /// Cache of class instances and method reflection information for message handlers.
        /// </summary>
        public ReflectionCache ReflectionCache { get; private set; } = new();

        /// <summary>
        /// Contains information about the endpoint and the connection.
        /// </summary>
        public DmContext? Context { get; private set; }

        /// <summary>
        /// Instantiates a managed UDP client that sends data to the specified IP address and port by default.
        /// </summary>
        public void Connect(IPAddress ipAddress, int port)
        {
            Client = new UdpClient();

            var endpoint = new IPEndPoint(ipAddress, port);
            Context = new DmContext(this, Client, endpoint);
        }

        /// <summary>
        /// Instantiates a managed UDP client that sends data to the specified host name or IP address and port by default.
        /// </summary>
        public void Connect(string hostOrIpAddress, int port)
        {
            var addresses = Dns.GetHostAddresses(hostOrIpAddress);

            var ipAddress = addresses.FirstOrDefault(o => o.AddressFamily == AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault(o => o.AddressFamily == AddressFamily.InterNetworkV6)
                ?? throw new ArgumentException("Could not resolve IP address.", nameof(hostOrIpAddress));

            Client = new UdpClient();

            var endpoint = new IPEndPoint(ipAddress, port);
            Context = new DmContext(this, Client, endpoint);
        }

        /// <summary>
        /// Instantiates a managed UDP client that sends data to the specified endpoint by default.
        /// </summary>
        public void Connect(IPEndPoint ipEndpoint)
        {
            Client ??= new UdpClient();
            Context ??= new DmContext(this, Client, ipEndpoint);
        }

        /// <summary>
        /// Instantiates a managed UDP instance without a defined destination endpoint.
        /// </summary>
        public void Listen(int listenPort)
        {
            try
            {
                var frameBuffer = new FrameBuffer();
                var serverEndpoint = new IPEndPoint(IPAddress.Any, listenPort);

                Client ??= new UdpClient();
                Context ??= new DmContext(this, Client, null);

                Client.Client.Bind(serverEndpoint);

                _keepReceiveRunning = true;

                _receiveThread = new Thread(o =>
                {
                    while (_keepReceiveRunning)
                    {
                        try
                        {
                            while (_keepReceiveRunning && Client.ReadAndProcessFrames(serverEndpoint, this, Context, frameBuffer))
                            {
                            }
                        }
                        catch (Exception ex)
                        {
                            OnException?.Invoke(Context, ex);
                        }
                    }
                })
                {
                    IsBackground = true
                };

                _receiveThread.Start();
            }
            catch (Exception ex)
            {
                OnException?.Invoke(Context, ex);
                throw;
            }
        }

        /// <summary>
        /// Sends transparent keep-alive messages to the server, default 10 seconds.
        /// This is primarily to satisfy NAT reply port timeouts.
        /// </summary>
        public void StartKeepAlive(TimeSpan? interval = null)
        {
            try
            {
                if (_keepAliveThread != null || _keepKeepAliveRunning)
                {
                    return;
                }

                _keepKeepAliveRunning = true;

                interval ??= TimeSpan.FromSeconds(10);
                DateTime? lastKeepAlive = null;

                _keepAliveThread = new Thread(() =>
                {
                    while (_keepKeepAliveRunning)
                    {
                        if (lastKeepAlive == null || (DateTime.UtcNow - lastKeepAlive.Value) > interval)
                        {
                            lastKeepAlive = DateTime.UtcNow;
                            if (Context?.Endpoint != null)
                            {
                                try
                                {
                                    Dispatch(new DmKeepAliveDatagram(), Context.Endpoint);
                                }
                                catch (Exception ex)
                                {
                                    OnException?.Invoke(Context, ex);
                                }
                            }
                        }

                        Thread.Sleep(100);
                    }
                });

                _keepAliveThread.Start();
            }
            catch (Exception ex)
            {
                OnException?.Invoke(Context, ex);
                throw;
            }
        }

        /// <summary>
        /// Stops the keep-alive thread, if its running.
        /// </summary>
        public void StopKeepAlive(bool waitForCompletion = true)
        {
            try
            {
                if (_keepAliveThread == null || !_keepKeepAliveRunning)
                {
                    return;
                }

                _keepKeepAliveRunning = false;

                if (waitForCompletion)
                {
                    _keepAliveThread.Join();
                }

                _keepAliveThread = null;
            }
            catch (Exception ex)
            {
                OnException?.Invoke(Context, ex);
                throw;
            }
        }

        /// <summary>
        /// Adds a class that contains datagram handler functions.
        /// </summary>
        /// <param name="handler"></param>
        public void AddHandler(IDmDatagramHandler handler)
        {
            ReflectionCache.AddInstance(handler);
        }

        /// <summary>
        /// Closes the UDP manager, stops all listening threads.
        /// </summary>
        public void Stop(bool waitForCompletion = true)
        {
            try
            {
                if (_keepReceiveRunning)
                {
                    _keepReceiveRunning = false;
                    if (waitForCompletion)
                    {
                        _receiveThread?.Join();
                    }

                    StopKeepAlive(waitForCompletion);

                    try { Client?.Close(); } catch { }
                    try { Client?.Dispose(); } catch { }

                    Client = null;
                }
            }
            catch (Exception ex)
            {
                OnException?.Invoke(Context, ex);
                throw;
            }
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
                OnException?.Invoke(Context, ex);
                throw;
            }
        }

        #region DmContext passthrough.

        /// <summary>
        /// Sends a return serialized message to the remote endpoint defined when the client was created.
        /// </summary>
        public void Dispatch(IDmDatagram datagram)
        {
            if (Context?.Endpoint == null) throw new Exception("The endpoint has not been set for this context.");
            Context?.Dispatch(datagram, Context.Endpoint);
        }

        /// <summary>
        /// Sends a return serialized message to the remote endpoint defined when the client was created.
        /// </summary>
        public void Dispatch(IDmDatagram datagram, IPEndPoint iPEndPoint)
            => Context?.Dispatch(datagram, iPEndPoint);

        /// <summary>
        /// Sends a frame containing the given bytes to the remote endpoint defined when the client was created.
        /// </summary>
        public void Dispatch(byte[] bytes)
        {
            if (Context?.Endpoint == null) throw new Exception("The endpoint has not been set for this context.");
            Context?.Dispatch(bytes, Context.Endpoint);
        }

        /// <summary>
        /// Sends a frame containing the given bytes to the remote endpoint defined when the client was created.
        /// </summary>
        public void Dispatch(byte[] bytes, IPEndPoint iPEndPoint)
            => Context?.Dispatch(bytes, iPEndPoint);

        #endregion

        /// <summary>
        /// Gets a random and unused port number that can be used for listening.
        /// </summary>
        public static int GetRandomUnusedUdpPort()
        {
            return GetRandomUnusedUDPPort(1, 65534);
        }

        /// <summary>
        /// Gets a random and unused port number that can be used for listening.
        /// </summary>
        public static int GetRandomUnusedUDPPort(int minPort, int maxPort)
        {
            while (true)
            {
                int port = Random.Shared.Next(minPort, maxPort + 1);
                if (IsPortAvailable(port))
                {
                    return port;
                }
            }
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


    }
}
