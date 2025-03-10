﻿namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Use to manipulate the payload bytes after they are compressed but before they are framed.
    /// </summary>
    public interface IDmCryptographyProvider
    {
        /// <summary>
        /// Encrypt the frame payload before it is sent.
        /// </summary>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="payload">Contains the raw unencrypted data.</param>
        /// <returns>Return the altered bytes.</returns>
        public byte[] Encrypt(DmContext context, byte[] payload);

        /// <summary>
        /// Decrypt the frame payload after it is received.
        /// </summary>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="encryptedPayload">Contains the encrypted data.</param>
        /// <returns>Return the altered bytes.</returns>
        public byte[] Decrypt(DmContext context, byte[] encryptedPayload);
    }
}
