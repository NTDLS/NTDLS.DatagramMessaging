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
    public class DmMessenger
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
        /// <param name="payload"></param>
        public delegate void NotificationReceivedEvent(DmContext context, IDmNotification payload);

        #endregion

        /// <summary>
        /// Underlying native UDP client.
        /// </summary>
        public UdpClient? Client { get; set; }

        /// <summary>
        /// Cache of class instances and method reflection information for message handlers.
        /// </summary>
        public ReflectionCache ReflectionCache { get; private set; } = new();

        /// <summary>
        /// Starts a new managed UDP "connection" that can send and receive. You must also call AddHandler() or hook the OnNotificationReceivedevent  so that messages can be handled.
        /// </summary>
        /// <param name="listenPort"></param>
        public DmMessenger(int listenPort)
        {
            Client = new UdpClient(listenPort);
            ListenAsync(listenPort);
        }

        /// <summary>
        /// Starts a new managed UDP "connection" that can send and receive, with a default handler.
        /// </summary>
        /// <param name="listenPort"></param>
        /// <param name="handlerClass"></param>
        public DmMessenger(int listenPort, IDmMessageHandler handlerClass)
        {
            Client = new UdpClient(listenPort);
            AddHandler(handlerClass);
            ListenAsync(listenPort);
        }

        /// <summary>
        /// Starts a new managed UDP handler that can send only.
        /// </summary>
        public DmMessenger()
        {
            Client = new UdpClient();
        }

        /// <summary>
        /// Clean up any remaining resources.
        /// </summary>
        ~DmMessenger()
        {
            try { Client?.Close(); } catch { }
            try { Client?.Dispose(); } catch { }

            Client = null;

        }

        /// <summary>
        /// Adds a class that contains notification and query handler functions.
        /// </summary>
        /// <param name="handlerClass"></param>
        public void AddHandler(IDmMessageHandler handlerClass)
        {
            ReflectionCache.AddInstance(handlerClass);
        }

        /// <summary>
        /// Closes the UDP manager, stops all listening threads.
        /// </summary>
        public void Shutdown()
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
        /// Gets a random and unused port number that can be used for listending.
        /// </summary>
        /// <param name="minPort"></param>
        /// <param name="maxPort"></param>
        /// <returns></returns>
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
        /// <param name="port"></param>
        /// <returns></returns>
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
        /// Sends a serialized message to the specified endpoint.
        /// </summary>
        /// <param name="hostOrIPAddress"></param>
        /// <param name="port"></param>
        /// <param name="payload"></param>
        /// <exception cref="Exception"></exception>
        public void WriteMessage(string hostOrIPAddress, int port, IDmNotification payload)
        {
            if (Client == null) throw new Exception("The UDP client has not been initialized.");
            Client.WriteNotificationFrame(hostOrIPAddress, port, payload);
        }

        /// <summary>
        /// Sends a serialized message to the specified endpoint.
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <param name="port"></param>
        /// <param name="payload"></param>
        /// <exception cref="Exception"></exception>
        public void WriteMessage(IPAddress ipAddress, int port, IDmNotification payload)
        {
            if (Client == null) throw new Exception("The UDP client has not been initialized.");
            Client.WriteNotificationFrame(ipAddress, port, payload);
        }

        /// <summary>
        /// Sends a serialized message to the specified endpoint.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="payload"></param>
        /// <exception cref="Exception"></exception>
        public void WriteMessage(IPEndPoint endpoint, IDmNotification payload)
        {
            if (Client == null) throw new Exception("The UDP client has not been initialized.");
            Client.WriteNotificationFrame(endpoint, payload);
        }

        /// <summary>
        /// Sends a frame containing the given bytes to the specified endpoint.
        /// </summary>
        /// <param name="hostOrIPAddress"></param>
        /// <param name="port"></param>
        /// <param name="payload"></param>
        /// <exception cref="Exception"></exception>
        public void WriteBytes(string hostOrIPAddress, int port, byte[] payload)
        {
            if (Client == null) throw new Exception("The UDP client has not been initialized.");
            Client.WriteBytesFrame(hostOrIPAddress, port, payload);
        }

        /// <summary>
        /// Sends a frame containing the given bytes to the specified endpoint.
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <param name="port"></param>
        /// <param name="payload"></param>
        /// <exception cref="Exception"></exception>
        public void WriteBytes(IPAddress ipAddress, int port, byte[] payload)
        {
            if (Client == null) throw new Exception("The UDP client has not been initialized.");
            Client.WriteBytesFrame(ipAddress, port, payload);
        }

        /// <summary>
        /// Sends a frame containing the given bytes to the specified endpoint.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="payload"></param>
        /// <exception cref="Exception"></exception>
        public void WriteBytes(IPEndPoint endpoint, byte[] payload)
        {
            if (Client == null) throw new Exception("The UDP client has not been initialized.");
            Client.WriteBytesFrame(endpoint, payload);
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
                var context = new DmContext(this, Client, Thread.CurrentThread);

                while (_keepRunning)
                {
                    try
                    {
                        while (_keepRunning && Client.ReadAndProcessFrames(ref clientEndPoint, frameBuffer,
                            (payload) => LocalProcessFrameNotificationByConvention(context, payload)))
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
                if (ReflectionCache.GetCachedMethod(payload.GetType(), out var cachedMethod))
                {
                    if (ReflectionCache.GetCachedInstance(cachedMethod, out var cachedInstance))
                    {
                        cachedMethod.Invoke(cachedInstance, new object[] { context, payload });
                        return;
                    }
                }

                OnNotificationReceived?.Invoke(context, payload);
            }
        }
    }
}
