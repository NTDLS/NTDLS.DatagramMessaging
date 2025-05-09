﻿using System;

namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Datagram to send in response to a keep-alive from server to client.
    /// This is discarded by the receive thread.
    /// </summary>
    public class DmKeepAliveReplyDatagram
        : IDmDatagram, IDmKeepAliveDatagram
    {
        /// <summary>
        /// The date-time that the keep-alive packet was sent.
        /// </summary>
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
    }
}
