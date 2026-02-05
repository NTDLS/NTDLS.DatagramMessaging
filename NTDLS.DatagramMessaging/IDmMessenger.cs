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
        /// Gets the compression provider used for data compression operations, if one is configured.
        /// </summary>
        /// <returns>An <see cref="IDmCompressionProvider"/> instance that provides compression functionality, or <see
        /// langword="null"/> if no compression provider is available.</returns>
        public IDmCompressionProvider? GetCompressionProvider();

        /// <summary>
        /// Gets the cryptography provider used for encryption and decryption operations.
        /// </summary>
        /// <returns>An object that implements <see cref="IDmCryptographyProvider"/> if a cryptography provider is configured;
        /// otherwise, <see langword="null"/>.</returns>
        public IDmCryptographyProvider? GetCryptographyProvider();

        /// <summary>
        /// Whether or not received datagrams are processed in a separate thread.
        /// Otherwise, datagram processing blocks other activities.
        /// </summary>
        bool AsynchronousDatagramProcessing { get; }

        /// <summary>
        /// Routes inbound packets to the appropriate handler.
        /// </summary>
        void ProcessFrameDatagramByConvention(DmContext context, IDmDatagram datagram);

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
        void InvokeOnKeepAlive(DmContext context, IDmKeepAliveDatagram keepAlive);

        /// <summary>
        /// Used to invoke the OnException event on the server and client, if its hooked.
        /// </summary>
        void InvokeOnException(DmContext? context, Exception ex);
    }
}
