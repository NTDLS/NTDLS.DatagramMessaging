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
        public DmContext Context { get; private set; }

        /// <summary>
        /// Instantiates a managed UDP instance that sends data to the specified ip address and port.
        /// </summary>
        public DmClient(IPAddress ipAddress, int port, bool canReceiveData = true)
        {
            Client = new UdpClient(0);
            Context = new DmContext(this, Client, new IPEndPoint(ipAddress, port));
            if (canReceiveData)
            {
                ListenAsync(0);
            }
        }

        /// <summary>
        /// Instantiates a managed UDP instance that sends data to the specified ip address and port.
        /// </summary>
        public DmClient(string hostOrIpAddress, int port, bool canReceiveData = true)
        {
            var addresses = Dns.GetHostAddresses(hostOrIpAddress);

            var ipAddress = addresses.FirstOrDefault(o => o.AddressFamily == AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault(o => o.AddressFamily == AddressFamily.InterNetworkV6)
                ?? throw new ArgumentException("Could not resolve IP address.", nameof(hostOrIpAddress));

            Client = new UdpClient(0);
            Context = new DmContext(this, Client, new IPEndPoint(ipAddress, port));
            if (canReceiveData)
            {
                ListenAsync(0);
            }
        }

        /// <summary>
        /// Instantiates a managed UDP instance that sends data to the specified ip endpoint.
        /// </summary>
        public DmClient(IPEndPoint ipEndpoint, bool canReceiveData = true)
        {
            Client = new UdpClient(0);
            Context = new DmContext(this, Client, ipEndpoint);
            if (canReceiveData)
            {
                ListenAsync(0);
            }
        }

        /// <summary>
        /// Instantiates a managed UDP instance that without a defined destination endpoint.
        /// </summary>
        public DmClient(bool canReceiveData = true)
        {
            Client = new UdpClient();
            Context = new DmContext(this, Client, null);
            if (canReceiveData)
            {
                ListenAsync(0);
            }
        }

        /// <summary>
        /// Clean up any remaining resources.
        /// </summary>
        ~DmClient()
        {
            try { Client?.Close(); } catch { }
            try { Client?.Dispose(); } catch { }

            Client = null;
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
                            if (Context.Endpoint != null)
                            {
                                try
                                {
                                    Dispatch(new DmKeepAliveDatagram());
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
                    try { Client?.Close(); } catch { }
                    try { Client?.Dispose(); } catch { }

                    Client = null;

                    _keepReceiveRunning = false;
                    if (waitForCompletion)
                    {
                        _receiveThread?.Join();
                    }

                    StopKeepAlive(waitForCompletion);
                }
            }
            catch (Exception ex)
            {
                OnException?.Invoke(Context, ex);
                throw;
            }
        }

        private void ListenAsync(int listenPort)
        {
            try
            {
                if (Client == null)
                {
                    throw new Exception("The UDP client has not been initialized.");
                }

                FrameBuffer frameBuffer = new();
                var serverEndpoint = new IPEndPoint(IPAddress.Any, listenPort);

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
                });

                _receiveThread.Start();
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
            => Context.Dispatch(datagram);

        /// <summary>
        /// Sends a frame containing the given bytes to the remote endpoint defined when the client was created.
        /// </summary>
        public void Dispatch(byte[] bytes)
            => Context.Dispatch(bytes);

        /// <summary>
        /// Sends a serialized message to the specified endpoint.
        /// </summary>
        public void Dispatch(string hostOrIPAddress, int port, IDmDatagram datagram)
            => Context.Dispatch(hostOrIPAddress, port, datagram);

        /// <summary>
        /// Sends a serialized message to the specified endpoint.
        /// </summary>
        public void Dispatch(IPAddress ipAddress, int port, IDmDatagram datagram)
            => Context.Dispatch(ipAddress, port, datagram);

        /// <summary>
        /// Sends a serialized message to the specified endpoint.
        /// </summary>
        public void Dispatch(IPEndPoint endpoint, IDmDatagram datagram)
            => Context.Dispatch(endpoint, datagram);

        /// <summary>
        /// Sends a frame containing the given bytes to the specified endpoint.
        /// </summary>
        public void Dispatch(string hostOrIPAddress, int port, byte[] bytes)
            => Context.Dispatch(hostOrIPAddress, port, bytes);

        /// <summary>
        /// Sends a frame containing the given bytes to the specified endpoint.
        /// </summary>
        public void Dispatch(IPAddress ipAddress, int port, byte[] bytes)
            => Context.Dispatch(ipAddress, port, bytes);

        /// <summary>
        /// Sends a frame containing the given bytes to the specified endpoint.
        /// </summary>
        public void Dispatch(IPEndPoint endpoint, byte[] bytes)
            => Context.Dispatch(endpoint, bytes);

        #endregion
    }
}
