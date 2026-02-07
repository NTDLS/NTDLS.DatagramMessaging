using System;

namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Datagram to send in response to a keep-alive from server to client.
    /// This is discarded by the receive thread.
    /// </summary>
    internal class DmKeepAliveReplyDatagram
        : IDmDatagram, IDmKeepAliveDatagram
    {
        /// <summary>
        /// The UTC date-time that the keep-alive packet was sent.
        /// This is not the reply time, but the original time that the keep-alive packet was sent by the client.
        /// This allows us to calculate the round-trip time (RTT) by comparing this timestamp with the current UTC time when the reply is received by the client.
        /// </summary>
        public DateTime TimeStampUTC { get; set; } = DateTime.MinValue;

        public DmKeepAliveReplyDatagram()
        {
        }

        public DmKeepAliveReplyDatagram(DateTime timeStampUTC)
        {
            TimeStampUTC = timeStampUTC;
        }
    }
}
