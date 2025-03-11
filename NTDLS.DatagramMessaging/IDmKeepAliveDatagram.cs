using System;

namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Datagram that is used to communicate a keep-alive and keep-alive replies.
    /// </summary>
    public interface IDmKeepAliveDatagram
    {
        /// <summary>
        /// The date-time that the keep-alive packet was sent.
        /// </summary>
        DateTime TimeStamp { get; }
    }
}
