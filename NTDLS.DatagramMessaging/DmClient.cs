﻿using NTDLS.DatagramMessaging.Framing;
using NTDLS.DatagramMessaging.Internal;
using System;
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
        private static readonly Random _random = new();
        private bool _keepRunning = false;
        private bool _keepKeepAliveRunning = false;
        private Thread? _receiveThread = null;
        private Thread? _keepAliveThread = null;

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
        public DmClient(string ipAddress, int port, bool canReceiveData = true)
        {
            Client = new UdpClient(0);
            Context = new DmContext(this, Client, new IPEndPoint(IPAddress.Parse(ipAddress), port));
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
                                Dispatch(new DmKeepAliveMessage());
                            }
                            catch
                            {
                            }
                        }
                    }
                    Thread.Sleep(100);
                }
            });

            _keepAliveThread.Start();
        }

        /// <summary>
        /// Stops the keep-alive thread, if its running.
        /// </summary>
        public void StopKeepAlive(bool waitForCompletion = true)
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
        public void Stop(bool waitForCompletion = true)
        {
            if (_keepRunning)
            {
                try { Client?.Close(); } catch { }
                try { Client?.Dispose(); } catch { }

                Client = null;

                _keepRunning = false;
                if (waitForCompletion)
                {
                    _receiveThread?.Join();
                }

                StopKeepAlive(waitForCompletion);
            }
        }

        private void ListenAsync(int listenPort)
        {
            if (Client == null)
            {
                throw new Exception("The UDP client has not been initialized.");
            }

            FrameBuffer frameBuffer = new();
            var serverEndpoint = new IPEndPoint(IPAddress.Any, listenPort);

            _keepRunning = true;

            _receiveThread = new Thread(o =>
            {
                while (_keepRunning)
                {
                    try
                    {
                        while (_keepRunning && Client.ReadAndProcessFrames(serverEndpoint, this, Context, frameBuffer))
                        {
                        }
                    }
                    catch { }
                }
            });

            _receiveThread.Start();
        }

        /// <summary>
        /// Routes inbound packets to the appropriate handler.
        /// </summary>
        public void ProcessFrameNotificationByConvention(DmContext context, IDmNotification payload)
        {
            //First we try to invoke functions that match the signature, if that fails we will fall back to invoking the OnNotificationReceived() event.
            if (ReflectionCache.RouteToNotificationHander(context, payload))
            {
                return; //Notification was handled by handler routing.
            }

            OnNotificationReceived?.Invoke(context, payload);
        }

        #region DmContext passthrough.

        /// <summary>
        /// Sends a return serialized message to the remote endpoint defined when the client was created.
        /// </summary>
        public void Dispatch(IDmNotification payload)
            => Context.Dispatch(payload);

        /// <summary>
        /// Sends a frame containing the given bytes to the remote endpoint defined when the client was created.
        /// </summary>
        public void Dispatch(byte[] payload)
            => Context.Dispatch(payload);

        /// <summary>
        /// Sends a serialized message to the specified endpoint.
        /// </summary>
        public void Dispatch(string hostOrIPAddress, int port, IDmNotification payload)
            => Context.Dispatch(hostOrIPAddress, port, payload);

        /// <summary>
        /// Sends a serialized message to the specified endpoint.
        /// </summary>
        public void Dispatch(IPAddress ipAddress, int port, IDmNotification payload)
            => Context.Dispatch(ipAddress, port, payload);

        /// <summary>
        /// Sends a serialized message to the specified endpoint.
        /// </summary>
        public void Dispatch(IPEndPoint endpoint, IDmNotification payload)
            => Context.Dispatch(endpoint, payload);

        /// <summary>
        /// Sends a frame containing the given bytes to the specified endpoint.
        /// </summary>
        public void Dispatch(string hostOrIPAddress, int port, byte[] payload)
            => Context.Dispatch(hostOrIPAddress, port, payload);

        /// <summary>
        /// Sends a frame containing the given bytes to the specified endpoint.
        /// </summary>
        public void Dispatch(IPAddress ipAddress, int port, byte[] payload)
            => Context.Dispatch(ipAddress, port, payload);

        /// <summary>
        /// Sends a frame containing the given bytes to the specified endpoint.
        /// </summary>
        public void Dispatch(IPEndPoint endpoint, byte[] payload)
            => Context.Dispatch(endpoint, payload);

        #endregion
    }
}
