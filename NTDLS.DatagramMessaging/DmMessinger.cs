using NTDLS.DatagramMessaging.Framing;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using static NTDLS.DatagramMessaging.Framing.UDPFraming;

namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Wrapper for UdpClient. Set of classes and extensions methods that allow you to send/receive UPD packets with ease.
    /// </summary>
    public class DmMessenger
    {
        private static readonly Random _random = new();
        private bool _keepRunning = false;
        private Thread? _receiveThread = null;

        /// <summary>
        /// Underlying native UDP client.
        /// </summary>
        public UdpClient? Client { get; set; }

        /// <summary>
        /// Starts a new managed UPD "connection" that can send and receive.
        /// </summary>
        /// <param name="listenPort"></param>
        /// <param name="processNotificationCallback"></param>
        public DmMessenger(int listenPort, ProcessFrameNotificationCallback processNotificationCallback)
        {
            Client = new UdpClient(listenPort);
            ListenAsync(listenPort, processNotificationCallback);
        }

        /// <summary>
        /// Starts a new managed UPD handler that can send only.
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

        private void ListenAsync(int listenPort, ProcessFrameNotificationCallback processNotificationCallback)
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
                while (_keepRunning)
                {
                    try
                    {
                        while (_keepRunning && Client.ReadAndProcessFrames(ref clientEndPoint, frameBuffer, processNotificationCallback))
                        {
                        }
                    }
                    catch { }
                }
            });

            _receiveThread.Start();
        }
    }
}
