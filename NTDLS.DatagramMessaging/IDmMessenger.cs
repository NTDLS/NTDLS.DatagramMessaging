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
    }
}
