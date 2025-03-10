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
        private bool _keepRunning = false;
        private Thread? _receiveThread = null;

        #region Events.

        /// <summary>
        /// Event fired when a notification is received from a client.
        /// </summary>
        public event NotificationReceivedEvent? OnNotificationReceived;

        /// <summary>
        /// Event fired when a notification is received from a client.
        /// </summary>
        /// <param name="context">Information about the connection.</param>
        /// <param name="notification"></param>
        public delegate void NotificationReceivedEvent(DmContext context, IDmNotification notification);

        #endregion

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
            Client = new UdpClient(listenPort);
            ListenAsync(listenPort);
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
            ReflectionCache.AddInstance(handler);
        }

        /// <summary>
        /// Closes the UDP manager, stops all listening threads.
        /// </summary>
        public void Stop()
        {
            if (_keepRunning)
            {
                try { Client?.Close(); } catch { }
                try { Client?.Dispose(); } catch { }

                Client = null;

                _keepRunning = false;
                _receiveThread?.Join();
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
            if (Client == null)
            {
                throw new Exception("The UDP client has not been initialized.");
            }

            FrameBuffer frameBuffer = new();
            var clientEndPoint = new IPEndPoint(IPAddress.Any, listenPort);

            _keepRunning = true;

            _receiveThread = new Thread(o =>
            {
                var context = new DmContext(this, Client, clientEndPoint);

                while (_keepRunning)
                {
                    try
                    {
                        while (_keepRunning && Client.ReadAndProcessFrames(ref clientEndPoint, context, frameBuffer,
                            (payload) => LocalProcessFrameNotificationByConvention(context, payload),
                                context.GetSerializationProvider,/*This is a delegate function call so that we can get the provider at the latest possible moment.*/
                                context.GetCompressionProvider,/*This is a delegate function call so that we can get the provider at the latest possible moment.*/
                                context.GetCryptographyProvider/*This is a delegate function call so that we can get the provider at the latest possible moment.*/))
                        {
                        }
                    }
                    catch { }
                }
            });

            _receiveThread.Start();

            void LocalProcessFrameNotificationByConvention(DmContext context, IDmNotification payload)
            {
                //First we try to invoke functions that match the signature, if that fails we will fall back to invoking the OnNotificationReceived() event.
                if (ReflectionCache.RouteToNotificationHander(context, payload))
                {
                    return; //Notification was handled by handler routing.
                }

                OnNotificationReceived?.Invoke(context, payload);
            }
        }
    }
}
