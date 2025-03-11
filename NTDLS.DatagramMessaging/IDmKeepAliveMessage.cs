using System;

namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Payload that is used to communicate a keep-alive.
    /// </summary>
    public interface IDmKeepAliveMessage
    {
        /// <summary>
        /// The date-time that the keep-alive packet was sent.
        /// </summary>
        DateTime TimeStamp { get; }
    }
}
