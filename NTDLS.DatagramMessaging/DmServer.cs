using NTDLS.DatagramMessaging.Framing;
using NTDLS.DatagramMessaging.Internal;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using static NTDLS.DatagramMessaging.Framing.UDPFraming;

namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Wrapper for UdpClient. Set of classes and extensions methods that allow you to send/receive UDP packets with ease.
    /// </summary>
    public class DmServer :
        IDmMessenger
    {
        private static readonly Random _random = new();
        private bool _keepReceiveRunning = false;
        private Thread? _receiveThread = null;

        #region Event: OnNotificationReceived.

        /// <summary>
        /// Event fired when a notification is received from a client.
        /// </summary>
        public event NotificationReceivedHandler? OnNotificationReceived;

        /// <summary>
        /// Event fired when a notification is received from a client.
        /// </summary>
        /// <param name="context">Information about the connection.</param>
        /// <param name="notification"></param>
        public delegate void NotificationReceivedHandler(DmContext context, IDmDatagram notification);

        #endregion

        #region Event: OnKeepAliveReceived.

        /// <summary>
        /// Event fired when a keep-alive notification is received.
        /// </summary>
        public event KeepAliveReceivedHandler? OnKeepAliveReceived;

        /// <summary>
        /// Event fired when a keep-alive notification is received.
        /// </summary>
        /// <param name="context">Information about the connection.</param>
        /// <param name="keepAlive">Instance of the keep-alive class that was received.</param>
        public delegate void KeepAliveReceivedHandler(DmContext context, IDmKeepAliveMessage keepAlive);

        /// <summary>
        /// Event fired when a keep-alive notification is received.
        /// </summary>
        /// <param name="context">Information about the connection.</param>
        /// <param name="keepAlive">Instance of the keep-alive class that was received.</param>
        public void InvokeOnKeepAlive(DmContext context, IDmKeepAliveMessage keepAlive)
            => OnKeepAliveReceived?.Invoke(context, keepAlive);

        #endregion

        #region Event: OnException

        /// <summary>
        /// Event fired when a keep-alive notification is received.
        /// </summary>
        public event OnExceptionHander? OnException;

        /// <summary>
        /// Event fired when a keep-alive notification is received.
        /// </summary>
        /// <param name="context">Information about the connection.</param>
        /// <param name="ex">information about the exception that occurred.</param>
        public delegate void OnExceptionHander(DmContext? context, Exception ex);

        /// <summary>
        /// Event fired when a keep-alive notification is received.
        /// </summary>
        /// <param name="context">Information about the connection.</param>
        /// <param name="ex">information about the exception that occurred.</param>
        public void InvokeOnException(DmContext? context, Exception ex)
            => OnException?.Invoke(context, ex);

        #endregion

        /// <summary>
        /// Denotes whether the receive thread is active.
        /// </summary>
        public bool IsReceiveRunning { get => _keepReceiveRunning; }

        /// <summary>
        /// When true, notifications are queued in a thread pool.
        /// Otherwise, notifications block other activities.
        /// </summary>
        public bool AsynchronousNotifications { get; set; } = true;

        /// <summary>
        /// Underlying native UDP client.
        /// </summary>
        public UdpClient? Client { get; private set; }

        /// <summary>
        /// Cache of class instances and method reflection information for message handlers.
        /// </summary>
        public ReflectionCache ReflectionCache { get; private set; } = new();

        /// <summary>
        /// Starts a new managed UDP "connection" that can send and receive.
        /// You must also call AddHandler() or hook the OnNotificationReceivedEvent so that messages can be handled.
        /// </summary>
        public DmServer(int listenPort)
        {
            Client = new UdpClient(listenPort);
            ListenAsync(listenPort);
        }

        /// <summary>
        /// Starts a new managed UDP "connection" that can send and receive, with a default handler.
        /// </summary>
        /// <param name="listenPort"></param>
        /// <param name="handlerClass"></param>
        public DmServer(int listenPort, IDmMessageHandler handlerClass)
        {
            Client = new UdpClient(listenPort);
            AddHandler(handlerClass);
            ListenAsync(listenPort);
        }

        /// <summary>
        /// Instantiates a managed UDP instance.
        /// </summary>
        public DmServer()
        {
        }

        /// <summary>
        /// Starts a new managed UDP "connection" that can send and receive.
        /// You must also call AddHandler() or hook the OnNotificationReceivedEvent so that messages can be handled.
        /// </summary>
        public void Start(int listenPort)
        {
            try
            {
            Client = new UdpClient(listenPort);
            ListenAsync(listenPort);
            }
            catch (Exception ex)
            {
                OnException?.Invoke(null, ex);
                throw;
            }
        }

        /// <summary>
        /// Clean up any remaining resources.
        /// </summary>
        ~DmServer()
        {
            try { Client?.Close(); } catch { }
            try { Client?.Dispose(); } catch { }

            Client = null;
        }

        /// <summary>
        /// Adds a class that contains notification handler functions.
        /// </summary>
        /// <param name="handler"></param>
        public void AddHandler(IDmMessageHandler handler)
        {
            try
            {
            ReflectionCache.AddInstance(handler);
            }
            catch (Exception ex)
            {
                OnException?.Invoke(null, ex);
                throw;
            }
        }

        /// <summary>
        /// Closes the UDP manager, stops all listening threads.
        /// </summary>
        public void Stop()
        {
            try{
            if (_keepReceiveRunning)
            {
                try { Client?.Close(); } catch { }
                try { Client?.Dispose(); } catch { }

                Client = null;

                _keepReceiveRunning = false;
                _receiveThread?.Join();
                }
            }
            catch (Exception ex)
            {
                OnException?.Invoke(null, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets a random and unused port number that can be used for listening.
        /// </summary>
        public static int GetRandomUnusedUDPPort(int minPort, int maxPort)
        {
            while (true)
            {
                int port = _random.Next(minPort, maxPort + 1);
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

        private void ListenAsync(int listenPort)
        {
            try{
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
                        while (_keepReceiveRunning && Client.ReadAndProcessFrames(serverEndpoint, this, null, frameBuffer))
                        {
                        }
                    }
                    catch (Exception ex)
                    {
                        OnException?.Invoke(null, ex);
                    }
                }
            });

            _receiveThread.Start();
            }
            catch (Exception ex)
            {
                OnException?.Invoke(null, ex);
                throw;
            }
        }

        /// <summary>
        /// Routes inbound packets to the appropriate handler.
        /// </summary>
        public void ProcessFrameNotificationByConvention(DmContext context, IDmDatagram payload)
        {
            try
            {
            //First we try to invoke functions that match the signature, if that fails we will fall back to invoking the OnNotificationReceived() event.
            if (ReflectionCache.RouteToNotificationHander(context, payload))
            {
                return; //Notification was handled by handler routing.
            }

            OnNotificationReceived?.Invoke(context, payload);
            }
            catch (Exception ex)
            {
                OnException?.Invoke(null, ex);
                throw;
            }
        }
    }
}
