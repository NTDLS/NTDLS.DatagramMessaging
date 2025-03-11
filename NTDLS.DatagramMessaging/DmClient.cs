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
        private bool _keepReceiveRunning = false;
        private bool _keepKeepAliveRunning = false;
        private Thread? _receiveThread = null;
        private Thread? _keepAliveThread = null;

        #region Event: OnNotificationReceived.

        /// <summary>
        /// Event fired when a notification is received from a client.
        /// </summary>
        public event NotificationReceivedHandler? OnNotificationReceived;

        /// <summary>
        /// Event fired when a notification is received from a client.
        /// </summary>
        /// <param name="context">Information about the connection.</param>
        /// <param name="notification">Interface containing the instance of the notification class.</param>
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
        public bool IsReceiveRunning { get { return _keepReceiveRunning; } }

        /// <summary>
        /// Denotes whether the keep-alive is active.
        /// </summary>
        public bool IsKeepAliveRunning { get { return _keepKeepAliveRunning; } }

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
                                    Dispatch(new DmKeepAliveMessage());
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
                OnException?.Invoke(Context, ex);
                throw;
            }
        }

        #region DmContext passthrough.

        /// <summary>
        /// Sends a return serialized message to the remote endpoint defined when the client was created.
        /// </summary>
        public void Dispatch(IDmDatagram payload)
            => Context.Dispatch(payload);

        /// <summary>
        /// Sends a frame containing the given bytes to the remote endpoint defined when the client was created.
        /// </summary>
        public void Dispatch(byte[] payload)
            => Context.Dispatch(payload);

        /// <summary>
        /// Sends a serialized message to the specified endpoint.
        /// </summary>
        public void Dispatch(string hostOrIPAddress, int port, IDmDatagram payload)
            => Context.Dispatch(hostOrIPAddress, port, payload);

        /// <summary>
        /// Sends a serialized message to the specified endpoint.
        /// </summary>
        public void Dispatch(IPAddress ipAddress, int port, IDmDatagram payload)
            => Context.Dispatch(ipAddress, port, payload);

        /// <summary>
        /// Sends a serialized message to the specified endpoint.
        /// </summary>
        public void Dispatch(IPEndPoint endpoint, IDmDatagram payload)
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
