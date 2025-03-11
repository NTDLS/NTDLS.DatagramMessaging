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
        void ProcessFrameNotificationByConvention(DmContext context, IDmNotification payload);

        /// <summary>
        /// Used to invoke the OnKeepAlive event on the server, if its hooked.
        /// This is not used for the client as the server does not send keep-alive messages.
        /// </summary>
        void InvokeOnKeepAlive(DmContext context, IDmKeepAliveMessage keepAlive);
    }
}
