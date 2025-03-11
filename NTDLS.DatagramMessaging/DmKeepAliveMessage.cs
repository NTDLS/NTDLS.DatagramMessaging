using System;

namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Payload to send for keep-alive from client to server.
    /// This is discarded by the receive thread.
    /// </summary>
    public class DmKeepAliveMessage
        : IDmDatagram, IDmKeepAliveMessage
    {
        /// <summary>
        /// The date-time that the keep-alive packet was sent.
        /// </summary>
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
    }
}
