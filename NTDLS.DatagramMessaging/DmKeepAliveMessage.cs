using System;

namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Payload to send for keep-alive. This is discarded by the receive thread.
    /// </summary>
    public class DmKeepAliveMessage
        : IDmNotification
    {
        /// <summary>
        /// The date-time that the keep-alive packet was sent.
        /// </summary>
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
    }
}
