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
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="frameBytes">Contains the raw uncompressed data.</param>
        /// <returns>Return the altered bytes.</returns>
        public byte[] Compress(DmContext context, byte[] frameBytes);

        /// <summary>
        /// Encrypt the frame payload after it is received.
        /// </summary>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="compressedFrameBytes">Contains the compressed data.</param>
        /// <returns>Return the altered bytes.</returns>
        public byte[] Decompress(DmContext context, byte[] compressedFrameBytes);
    }
}
