namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Use to manipulate the payload bytes before they are optionally encrypted and then framed.
    /// </summary>
    public interface IDmCompressionProvider
    {
        /// <summary>
        /// Compress the frame payload before it is sent.
        /// </summary>
        /// <param name="messenger">Contains the parent instance of DatagramMessenger.</param>
        /// <param name="payload">Contains the raw uncompressed data.</param>
        /// <returns>Return the altered bytes.</returns>
        public byte[] Compress(DatagramMessenger messenger, byte[] payload);

        /// <summary>
        /// Encrypt the frame payload after it is received.
        /// </summary>
        /// <param name="messenger">Contains the parent instance of DatagramMessenger.</param>
        /// <param name="compressedPayload">Contains the compressed data.</param>
        /// <returns>Return the altered bytes.</returns>
        public byte[] Decompress(DatagramMessenger messenger, byte[] compressedPayload);
    }
}
