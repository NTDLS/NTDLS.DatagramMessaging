namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Used to send a datagram of a raw byte array. Used by WriteBytesFrame() and handled in processDatagramCallback().
    /// When a raw byte array is used, all json serialization is skipped and checks for this datagram type are prioritized for performance.
    /// </summary>
    public class DmBytesDatagram
        : IDmDatagram
    {
        /// <summary>
        /// The payload bytes of the datagram.
        /// </summary>
        public byte[] Bytes { get; set; }

        /// <summary>
        /// Instantiates a new datagram from a byte array.
        /// </summary>
        public DmBytesDatagram(byte[] bytes)
        {
            Bytes = bytes;
        }
    }
}
