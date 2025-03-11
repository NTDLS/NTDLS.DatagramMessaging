using NTDLS.DatagramMessaging.Internal;
using System;
using System.Net.Sockets;

namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Interface for DmServer and DmClient.
    /// </summary>
    public interface IDmMessenger
    {
        /// <summary>
        /// When true, notifications are queued in a thread pool.
        /// Otherwise, notifications block other activities.
        /// </summary>
        bool AsynchronousNotifications { get; }

        /// <summary>
        /// Routes inbound packets to the appropriate handler.
        /// </summary>
        void ProcessFrameNotificationByConvention(DmContext context, IDmDatagram payload);

        /// <summary>
        /// Denotes whether the receive thread is active.
        /// </summary>
        bool IsReceiveRunning { get; }

        /// <summary>
        /// Underlying native UDP client.
        /// </summary>
        UdpClient? Client { get; }

        /// <summary>
        /// Cache of class instances and method reflection information for message handlers.
        /// </summary>
        ReflectionCache ReflectionCache { get; }

        /// <summary>
        /// Used to invoke the OnKeepAlive event on the server and client, if its hooked.
        /// </summary>
        void InvokeOnKeepAlive(DmContext context, IDmKeepAliveMessage keepAlive);

        /// <summary>
        /// Used to invoke the OnException event on the server and client, if its hooked.
        /// </summary>
        void InvokeOnException(DmContext? context, Exception ex);
    }
}
