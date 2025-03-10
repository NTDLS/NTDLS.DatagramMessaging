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
        public bool AsynchronousNotifications { get; }

        /// <summary>
        /// Routes inbound packets to the appropriate handler.
        /// </summary>
        public void ProcessFrameNotificationByConvention(DmContext context, IDmNotification payload);

        /// <summary>
        /// Used to invoke the OnKeepAlive client event, if its hooked.
        /// </summary>
        public void InvokeOnKeepAlive(DmContext context, DmKeepAliveMessage keepAlive);
    }
}
