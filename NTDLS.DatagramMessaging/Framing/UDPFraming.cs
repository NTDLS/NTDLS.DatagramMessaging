using NTDLS.DatagramMessaging.Internal;
using NTDLS.Semaphore;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static NTDLS.DatagramMessaging.Framing.Defaults;

namespace NTDLS.DatagramMessaging.Framing
{
    /// <summary>
    /// UDP packets can be fragmented or combined. Framing rebuilds what was
    /// originally sent while also providing compression and CRC checking.
    /// </summary>
    public static class UDPFraming
    {
        /// <summary>
        /// The callback that is used to notify of the receipt of a notification frame.
        /// </summary>
        /// <param name="payload">The notification payload.</param>
        public delegate void ProcessFrameNotificationCallback(IDmNotification payload);

        /// <summary>
        /// Callback to get the callback that is called to allow for manipulation of bytes before/after they are sent/received.
        /// </summary>
        /// <returns></returns>
        public delegate IDmCryptographyProvider? GetEncryptionProviderCallback();

        /// <summary>
        /// Callback to get the callback that is called to allow for manipulation of bytes before/after they are sent/received.
        /// </summary>
        /// <returns></returns>
        public delegate IDmCompressionProvider? GetCompressionProviderCallback();

        /// <summary>
        /// Callback to get the callback that is called to allow for custom serialization.
        /// </summary>
        /// <returns></returns>
        public delegate IDmSerializationProvider? GetSerializationProviderCallback();


        private static readonly PessimisticCriticalResource<Dictionary<string, MethodInfo>> _reflectionCache = new();

        #region Extension methods.

        /// <summary>
        /// Waits on bytes to become available, reads those bytes then parses the available frames (if any) and calls the appropriate callbacks.
        /// </summary>
        /// <param name="udpClient">The UDP client to receive data from.</param>
        /// <param name="messenger">Contains information about the endpoint and the connection.</param>
        /// <param name="endpoint">Endpoint to dispatch the datagram to.</param>
        /// <param name="frameBuffer">The frame buffer that will be used to receive bytes.</param>
        /// <param name="processNotificationCallback">Optional callback to call when a notification frame is received.</param>
        /// <param name="getSerializationProviderCallback">An optional callback to get the callback that is called to allow for custom serialization.</param>
        /// <param name="getCompressionProviderCallback">An optional callback to get the callback that is called to allow for manipulation of bytes after they are received.</param>/// 
        /// <param name="getEncryptionProviderCallback">An optional callback to get the callback that is called to allow for manipulation of bytes after they are received.</param>
        /// <returns>Returns true if bytes were received.</returns>
        /// <exception cref="Exception"></exception>
        public static bool ReadAndProcessFrames(this UdpClient udpClient, ref IPEndPoint endpoint, DmMessenger messenger, FrameBuffer frameBuffer,
            ProcessFrameNotificationCallback processNotificationCallback,
            GetSerializationProviderCallback? getSerializationProviderCallback,
            GetCompressionProviderCallback? getCompressionProviderCallback,
            GetEncryptionProviderCallback? getEncryptionProviderCallback)
        {
            if (udpClient == null)
            {
                throw new Exception("ReadAndProcessFrames: client can not be null.");
            }

            if (frameBuffer.ReadData(udpClient, ref endpoint))
            {
                IDmSerializationProvider? serializationProvider = null;
                IDmCompressionProvider? compressionProvider = null;
                IDmCryptographyProvider? cryptographyProvider = null;

                if (getSerializationProviderCallback != null)
                {
                    //We use a callback because frameBuffer.ReadStream() blocks and we may have assigned an serialization provider after we called ReadAndProcessFrames().
                    serializationProvider = getSerializationProviderCallback();
                }

                if (getCompressionProviderCallback != null)
                {
                    //We use a callback because frameBuffer.ReadStream() blocks and we may have assigned an compression provider after we called ReadAndProcessFrames().
                    compressionProvider = getCompressionProviderCallback();
                }

                if (getEncryptionProviderCallback != null)
                {
                    //We use a callback because frameBuffer.ReadStream() blocks and we may have assigned an encryption provider after we called ReadAndProcessFrames().
                    cryptographyProvider = getEncryptionProviderCallback();
                }

                ProcessFrameBuffer(messenger, frameBuffer, processNotificationCallback, serializationProvider, compressionProvider, cryptographyProvider);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sends a one-time fire-and-forget notification.
        /// </summary>
        /// <param name="udpClient">The client to send the data on.</param>
        /// <param name="messenger">Contains information about the endpoint and the connection.</param>
        /// <param name="framePayload">The notification payload that will be sent.</param>
        /// <param name="hostOrIPAddress">Host or IP address to dispatch the datagram to.</param>
        /// <param name="port">Port to dispatch the datagram to.</param>
        /// <param name="serializationProvider">An optional callback that is called to allow for custom serialization.</param>
        /// <param name="compressionProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <param name="cryptographyProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        public static void WriteNotificationFrame(this UdpClient udpClient, DmMessenger messenger, string hostOrIPAddress, int port, IDmNotification framePayload,
            IDmSerializationProvider? serializationProvider, IDmCompressionProvider? compressionProvider, IDmCryptographyProvider? cryptographyProvider)
        {
            var frameBody = new FrameBody(framePayload);
            var frameBytes = AssembleFrame(messenger, frameBody, compressionProvider, cryptographyProvider);
            udpClient.Send(frameBytes, frameBytes.Length, hostOrIPAddress, port);
        }

        /// <summary>
        /// Sends a one-time fire-and-forget notification.
        /// </summary>
        /// <param name="udpClient">The client to send the data on.</param>
        /// <param name="messenger">Contains information about the endpoint and the connection.</param>
        /// <param name="framePayload">The notification payload that will be sent.</param>
        /// <param name="ipAddress">IP address to dispatch the datagram to.</param>
        /// <param name="port">Port to dispatch the datagram to.</param>
        /// <param name="serializationProvider">An optional callback that is called to allow for custom serialization.</param>
        /// <param name="compressionProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <param name="cryptographyProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        public static void WriteNotificationFrame(this UdpClient udpClient, DmMessenger messenger, IPAddress ipAddress, int port, IDmNotification framePayload,
            IDmSerializationProvider? serializationProvider, IDmCompressionProvider? compressionProvider, IDmCryptographyProvider? cryptographyProvider)
        {
            var frameBody = new FrameBody(framePayload);
            var frameBytes = AssembleFrame(messenger, frameBody, compressionProvider, cryptographyProvider);
            udpClient.Send(frameBytes, frameBytes.Length, new IPEndPoint(ipAddress, port));
        }

        /// <summary>
        /// Sends a one-time fire-and-forget notification.
        /// </summary>
        /// <param name="udpClient">The client to send the data on.</param>
        /// <param name="messenger">Contains information about the endpoint and the connection.</param>
        /// <param name="endpoint">Endpoint to dispatch the datagram to.</param>
        /// <param name="framePayload">The notification payload that will be sent.</param>
        /// <param name="serializationProvider">An optional callback that is called to allow for custom serialization.</param>
        /// <param name="compressionProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <param name="cryptographyProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        public static void WriteNotificationFrame(this UdpClient udpClient, DmMessenger messenger, IPEndPoint endpoint, IDmNotification framePayload,
            IDmSerializationProvider? serializationProvider, IDmCompressionProvider? compressionProvider, IDmCryptographyProvider? cryptographyProvider)
        {
            var frameBody = new FrameBody(framePayload);
            var frameBytes = AssembleFrame(messenger, frameBody, compressionProvider, cryptographyProvider);
            udpClient.Send(frameBytes, frameBytes.Length, endpoint);
        }

        /// <summary>
        /// Sends a one-time fire-and-forget byte array payload. These are and handled in processNotificationCallback().
        /// When a raw byte array is use, all json serialization is skipped and checks for this payload type are prioritized for performance.
        /// </summary>
        /// <param name="udpClient">The client to send the data on.</param>
        /// <param name="messenger">Contains information about the endpoint and the connection.</param>
        /// <param name="framePayload">The bytes will make up the body of the frame which is written.</param>
        /// <param name="hostOrIPAddress">Host or IP address to dispatch the datagram to.</param>
        /// <param name="port">Port to dispatch the datagram to.</param>
        /// <param name="serializationProvider">An optional callback that is called to allow for custom serialization.</param>
        /// <param name="compressionProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <param name="cryptographyProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        public static void WriteBytesFrame(this UdpClient udpClient, DmMessenger messenger, string hostOrIPAddress, int port, byte[] framePayload,
            IDmSerializationProvider? serializationProvider, IDmCompressionProvider? compressionProvider, IDmCryptographyProvider? cryptographyProvider)
        {
            if (udpClient == null)
            {
                throw new Exception("WriteBytesFrame: client can not be null.");
            }

            var frameBody = new FrameBody(framePayload);
            var frameBytes = AssembleFrame(messenger, frameBody, compressionProvider, cryptographyProvider);
            udpClient.Send(frameBytes, frameBytes.Length, hostOrIPAddress, port);
        }

        /// <summary>
        /// Sends a one-time fire-and-forget byte array payload. These are and handled in processNotificationCallback().
        /// When a raw byte array is use, all json serialization is skipped and checks for this payload type are prioritized for performance.
        /// </summary>
        /// <param name="udpClient">The client to send the data on.</param>
        /// <param name="messenger">Contains information about the endpoint and the connection.</param>
        /// <param name="framePayload">The bytes will make up the body of the frame which is written.</param>
        /// <param name="ipAddress">IP address to dispatch the datagram to.</param>
        /// <param name="port">Port to dispatch the datagram to.</param>
        /// <param name="serializationProvider">An optional callback that is called to allow for custom serialization.</param>
        /// <param name="compressionProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <param name="cryptographyProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        public static void WriteBytesFrame(this UdpClient udpClient, DmMessenger messenger, IPAddress ipAddress, int port, byte[] framePayload,
            IDmSerializationProvider? serializationProvider, IDmCompressionProvider? compressionProvider, IDmCryptographyProvider? cryptographyProvider)
        {
            if (udpClient == null)
            {
                throw new Exception("WriteBytesFrame: client can not be null.");
            }

            var frameBody = new FrameBody(framePayload);
            var frameBytes = AssembleFrame(messenger, frameBody, compressionProvider, cryptographyProvider);
            udpClient.Send(frameBytes, frameBytes.Length, new IPEndPoint(ipAddress, port));
        }

        /// <summary>
        /// Sends a one-time fire-and-forget byte array payload. These are and handled in processNotificationCallback().
        /// When a raw byte array is use, all json serialization is skipped and checks for this payload type are prioritized for performance.
        /// </summary>
        /// <param name="udpClient">The client to send the data on.</param>
        /// <param name="messenger">Contains information about the endpoint and the connection.</param>
        /// <param name="endpoint">Endpoint to dispatch the datagram to.</param>
        /// <param name="framePayload">The bytes will make up the body of the frame which is written.</param>
        /// <param name="serializationProvider">An optional callback that is called to allow for custom serialization.</param>
        /// <param name="compressionProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <param name="cryptographyProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        public static void WriteBytesFrame(this UdpClient udpClient, DmMessenger messenger, IPEndPoint endpoint, byte[] framePayload,
            IDmSerializationProvider? serializationProvider, IDmCompressionProvider? compressionProvider, IDmCryptographyProvider? cryptographyProvider)
        {
            if (udpClient == null)
            {
                throw new Exception("WriteBytesFrame: client can not be null.");
            }

            var frameBody = new FrameBody(framePayload);
            var frameBytes = AssembleFrame(messenger, frameBody, compressionProvider, cryptographyProvider);
            udpClient.Send(frameBytes, frameBytes.Length, endpoint);
        }

        #endregion

        private static byte[] AssembleFrame(DmMessenger messenger, FrameBody frameBody,
            IDmCompressionProvider? compressionProvider, IDmCryptographyProvider? cryptographyProvider)
        {
            var frameBodyBytes = Serialization.SerializeToByteArray(frameBody);

            var compressedFrameBodyBytes = compressionProvider?.Compress(messenger, frameBodyBytes) ?? Compression.Compress(frameBodyBytes);

            if (cryptographyProvider != null)
            {
                compressedFrameBodyBytes = cryptographyProvider.Encrypt(messenger, compressedFrameBodyBytes);
            }

            var grossFrameSize = compressedFrameBodyBytes.Length + NtFrameDefaults.FRAME_HEADER_SIZE;
            var grossFrameBytes = new byte[grossFrameSize];
            var frameCrc = CRC16.ComputeChecksum(compressedFrameBodyBytes);

            Buffer.BlockCopy(BitConverter.GetBytes(NtFrameDefaults.FRAME_DELIMITER), 0, grossFrameBytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(grossFrameSize), 0, grossFrameBytes, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(frameCrc), 0, grossFrameBytes, 8, 2);
            Buffer.BlockCopy(compressedFrameBodyBytes, 0, grossFrameBytes, NtFrameDefaults.FRAME_HEADER_SIZE, compressedFrameBodyBytes.Length);

            return grossFrameBytes;
        }

        private static void SkipFrame(ref FrameBuffer frameBuffer)
        {
            var frameDelimiterBytes = new byte[4];

            for (int offset = 1; offset < frameBuffer.FrameBuilderLength - frameDelimiterBytes.Length; offset++)
            {
                Buffer.BlockCopy(frameBuffer.FrameBuilder, offset, frameDelimiterBytes, 0, frameDelimiterBytes.Length);

                var value = BitConverter.ToInt32(frameDelimiterBytes, 0);

                if (value == NtFrameDefaults.FRAME_DELIMITER)
                {
                    Buffer.BlockCopy(frameBuffer.FrameBuilder, offset, frameBuffer.FrameBuilder, 0, frameBuffer.FrameBuilderLength - offset);
                    frameBuffer.FrameBuilderLength -= offset;
                    return;
                }
            }
            Array.Clear(frameBuffer.FrameBuilder, 0, frameBuffer.FrameBuilder.Length);
            frameBuffer.FrameBuilderLength = 0;
        }

        private static void ProcessFrameBuffer(DmMessenger messenger, FrameBuffer frameBuffer,
            ProcessFrameNotificationCallback processNotificationCallback,
            IDmSerializationProvider? serializationProvider,
            IDmCompressionProvider? compressionProvider,
            IDmCryptographyProvider? cryptographyProvider)
        {
            if (frameBuffer.FrameBuilderLength + frameBuffer.ReceiveBufferUsed >= frameBuffer.FrameBuilder.Length)
            {
                Array.Resize(ref frameBuffer.FrameBuilder, frameBuffer.FrameBuilderLength + frameBuffer.ReceiveBufferUsed);
            }

            Buffer.BlockCopy(frameBuffer.ReceiveBuffer, 0, frameBuffer.FrameBuilder, frameBuffer.FrameBuilderLength, frameBuffer.ReceiveBufferUsed);

            frameBuffer.FrameBuilderLength = frameBuffer.FrameBuilderLength + frameBuffer.ReceiveBufferUsed;

            while (frameBuffer.FrameBuilderLength > NtFrameDefaults.FRAME_HEADER_SIZE) //[FrameSize] and [CRC16]
            {
                var frameDelimiterBytes = new byte[4];
                var frameSizeBytes = new byte[4];
                var expectedCRC16Bytes = new byte[2];

                Buffer.BlockCopy(frameBuffer.FrameBuilder, 0, frameDelimiterBytes, 0, frameDelimiterBytes.Length);
                Buffer.BlockCopy(frameBuffer.FrameBuilder, 4, frameSizeBytes, 0, frameSizeBytes.Length);
                Buffer.BlockCopy(frameBuffer.FrameBuilder, 8, expectedCRC16Bytes, 0, expectedCRC16Bytes.Length);

                var frameDelimiter = BitConverter.ToInt32(frameDelimiterBytes, 0);
                var grossFrameSize = BitConverter.ToInt32(frameSizeBytes, 0);
                var expectedCRC16 = BitConverter.ToUInt16(expectedCRC16Bytes, 0);

                if (frameDelimiter != NtFrameDefaults.FRAME_DELIMITER || grossFrameSize < 0)
                {
                    //Possible corrupt frame.
                    SkipFrame(ref frameBuffer);
                    continue;
                }

                if (frameBuffer.FrameBuilderLength < grossFrameSize)
                {
                    //We have data in the buffer, but it's not enough to make up
                    //  the entire message so we will break and wait on more data.
                    break;
                }

                if (CRC16.ComputeChecksum(frameBuffer.FrameBuilder, NtFrameDefaults.FRAME_HEADER_SIZE, grossFrameSize - NtFrameDefaults.FRAME_HEADER_SIZE) != expectedCRC16)
                {
                    //Corrupt frame.
                    SkipFrame(ref frameBuffer);
                    continue;
                }

                var netFrameSize = grossFrameSize - NtFrameDefaults.FRAME_HEADER_SIZE;
                var compressedFrameBodyBytes = new byte[netFrameSize];
                Buffer.BlockCopy(frameBuffer.FrameBuilder, NtFrameDefaults.FRAME_HEADER_SIZE, compressedFrameBodyBytes, 0, netFrameSize);

                var FrameBodyBytes = compressionProvider?.Decompress(messenger, compressedFrameBodyBytes) ?? Compression.Decompress(compressedFrameBodyBytes);

                var frameBody = Serialization.DeserializeToObject<FrameBody>(FrameBodyBytes);

                //Zero out the consumed portion of the frame buffer - more for fun than anything else.
                Array.Clear(frameBuffer.FrameBuilder, 0, grossFrameSize);

                Buffer.BlockCopy(frameBuffer.FrameBuilder, grossFrameSize, frameBuffer.FrameBuilder, 0, frameBuffer.FrameBuilderLength - grossFrameSize);
                frameBuffer.FrameBuilderLength -= grossFrameSize;

                var framePayload = ExtractFramePayload(serializationProvider, frameBody);

                if (framePayload is UDPFramePayloadBytes frameNotificationBytes)
                {
                    if (messenger.AsynchronousNotifications)
                    {
                        //Keep a reference to the frame payload that we are going to perform an async wait on.
                        var asynchronousNotificationBytes = frameNotificationBytes;
                        Task.Run(() =>
                        {
                            processNotificationCallback(asynchronousNotificationBytes);
                        });
                    }
                    else
                    {
                        processNotificationCallback(frameNotificationBytes);
                    }
                }
                else if (framePayload is IDmNotification notification)
                {
                    if (messenger.AsynchronousNotifications)
                    {
                        //Keep a reference to the frame payload that we are going to perform an async wait on.
                        var asynchronousNotification = notification;
                        Task.Run(() =>
                        {
                            processNotificationCallback(asynchronousNotification);
                        });
                    }
                    else
                    {
                        processNotificationCallback(notification);
                    }
                }
                else
                {
                    throw new Exception("ProcessFrameBuffer: Encountered undefined frame payload type.");
                }
            }
        }

        /// <summary>
        /// Uses the "EnclosedPayloadType" to determine the type of the payload and then uses reflection
        /// to deserialize the json to that type. Deserialization is skipped when the type is byte[].
        /// </summary>
        private static IDmPayload ExtractFramePayload(IDmSerializationProvider? serializationProvider, FrameBody frame)
        {
            if (frame.ObjectType == "byte[]")
            {
                return new UDPFramePayloadBytes(frame.Bytes);
            }

            string cacheKey = $"{frame.ObjectType}";

            var genericToObjectMethod = _reflectionCache.Use((o) =>
            {
                if (o.TryGetValue(cacheKey, out var method))
                {
                    return method;
                }
                return null;
            });

            string json = Encoding.UTF8.GetString(frame.Bytes);

            if (genericToObjectMethod != null)
            {
                //Call the generic deserialization:
                return (IDmPayload?)genericToObjectMethod.Invoke(null, new object?[] { serializationProvider, json })
                    ?? throw new Exception($"Extraction payload can not be null.");
            }

            var genericType = Type.GetType(frame.ObjectType)
                ?? throw new Exception($"Unknown extraction payload type [{frame.ObjectType}].");

            var toObjectMethod = typeof(Serialization).GetMethod("RmDeserializeFramePayloadToObject")
                    ?? throw new Exception($"Could not resolve RmDeserializeFramePayloadToObject().");

            genericToObjectMethod = toObjectMethod.MakeGenericMethod(genericType);

            _reflectionCache.Use((o) => o.TryAdd(cacheKey, genericToObjectMethod));

            //Call the generic deserialization:
            return (IDmPayload?)genericToObjectMethod.Invoke(null, new object?[] { serializationProvider, json })
                ?? throw new Exception($"Extraction payload can not be null.");
        }
    }
}
