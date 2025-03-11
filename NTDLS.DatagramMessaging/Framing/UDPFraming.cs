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
        private static readonly PessimisticCriticalResource<Dictionary<string, MethodInfo>> _reflectionCache = new();

        #region Extension methods.

        /// <summary>
        /// Waits on bytes to become available, reads those bytes then parses the available frames (if any) and calls the appropriate callbacks.
        /// </summary>
        /// <param name="udpClient">The UDP client to receive data from.</param>
        /// <param name="endpoint">Endpoint to dispatch the datagram to.</param>
        /// <param name="messenger">Parent client or server instance.</param>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="frameBuffer">The frame buffer that will be used to receive bytes.</param>
        /// <returns>Returns true if bytes were received.</returns>
        public static bool ReadAndProcessFrames(this UdpClient udpClient, IPEndPoint endpoint,
            IDmMessenger messenger, DmContext? context, FrameBuffer frameBuffer)
        {
            try
            {
                if (udpClient == null)
                {
                    throw new Exception("ReadAndProcessFrames: client can not be null.");
                }

                if (frameBuffer.ReadData(udpClient, ref endpoint))
                {
                    if (context == null)
                    {
                        //For the server, the context will be null because the endpoint is created via ReadData() when the NAT is established.
                        //Assuming the NAT is open for 30 seconds sans any activity, we are going to cache the context with a ~similar expiration.
                        var cacheKey = $"UPD.NAT.Context[{endpoint}]";
                        if (!DmCaching.TryGet(cacheKey, out context) || context == null)
                        {
                            context = new DmContext(messenger, udpClient, endpoint);
                            DmCaching.SetTenMinutes(cacheKey, context);
                        }
                    }

                    ProcessFrameBuffer(context, frameBuffer);

                    return true;
                }
            }
            catch (Exception ex)
            {
                messenger.InvokeOnException(context, ex);
            }

            return false;
        }

        /// <summary>
        /// Sends a one-time fire-and-forget datagram.
        /// </summary>
        /// <param name="udpClient">The client to send the data on.</param>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="datagram">The datagram that will be sent.</param>
        /// <param name="hostOrIPAddress">Host or IP address to dispatch the datagram to.</param>
        /// <param name="port">Port to dispatch the datagram to.</param>
        public static void Dispatch(this UdpClient udpClient, DmContext context, string hostOrIPAddress, int port, IDmDatagram datagram)
        {
            try
            {
                var frameBody = new FrameBody(context.GetSerializationProvider(), datagram);
                var frameBytes = AssembleFrame(context, frameBody);
                udpClient.Send(frameBytes, frameBytes.Length, hostOrIPAddress, port);
            }
            catch (Exception ex)
            {
                context.Messenger.InvokeOnException(context, ex);
                throw;
            }
        }

        /// <summary>
        /// Sends a one-time fire-and-forget datagram.
        /// </summary>
        /// <param name="udpClient">The client to send the data on.</param>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="datagram">The datagram that will be sent.</param>
        /// <param name="ipAddress">IP address to dispatch the datagram to.</param>
        /// <param name="port">Port to dispatch the datagram to.</param>
        public static void Dispatch(this UdpClient udpClient, DmContext context, IPAddress ipAddress, int port, IDmDatagram datagram)
        {
            try
            {
                var frameBody = new FrameBody(context.GetSerializationProvider(), datagram);
                var frameBytes = AssembleFrame(context, frameBody);
                udpClient.Send(frameBytes, frameBytes.Length, new IPEndPoint(ipAddress, port));
            }
            catch (Exception ex)
            {
                context.Messenger.InvokeOnException(context, ex);
                throw;
            }
        }

        /// <summary>
        /// Sends a one-time fire-and-forget datagram.
        /// </summary>
        /// <param name="udpClient">The client to send the data on.</param>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="endpoint">Endpoint to dispatch the datagram to.</param>
        /// <param name="datagram">The datagram that will be sent.</param>
        public static void Dispatch(this UdpClient udpClient, DmContext context, IPEndPoint endpoint, IDmDatagram datagram)
        {
            try
            {
                var frameBody = new FrameBody(context.GetSerializationProvider(), datagram);
                var frameBytes = AssembleFrame(context, frameBody);
                udpClient.Send(frameBytes, frameBytes.Length, endpoint);
            }
            catch (Exception ex)
            {
                context.Messenger.InvokeOnException(context, ex);
                throw;
            }
        }

        /// <summary>
        /// Sends a one-time fire-and-forget byte array datagram. These are and handled in ProcessDatagramCallback().
        /// When a raw byte array is use, all json serialization is skipped and checks for this datagram type are prioritized for performance.
        /// </summary>
        /// <param name="udpClient">The client to send the data on.</param>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="datagramBytes">The bytes will make up the body of the frame which is written.</param>
        /// <param name="hostOrIPAddress">Host or IP address to dispatch the datagram to.</param>
        /// <param name="port">Port to dispatch the datagram to.</param>
        public static void Dispatch(this UdpClient udpClient, DmContext context, string hostOrIPAddress, int port, byte[] datagramBytes)
        {
            try
            {
                if (udpClient == null)
                {
                    throw new Exception("WriteBytesFrame: client can not be null.");
                }

                var frameBody = new FrameBody(datagramBytes);
                var frameBytes = AssembleFrame(context, frameBody);
                udpClient.Send(frameBytes, frameBytes.Length, hostOrIPAddress, port);
            }
            catch (Exception ex)
            {
                context.Messenger.InvokeOnException(context, ex);
                throw;
            }
        }

        /// <summary>
        /// Sends a one-time fire-and-forget byte array datagram. These are and handled in ProcessDatagramCallback().
        /// When a raw byte array is use, all json serialization is skipped and checks for this datagram type are prioritized for performance.
        /// </summary>
        /// <param name="udpClient">The client to send the data on.</param>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="datagramBytes">The bytes will make up the body of the frame which is written.</param>
        /// <param name="ipAddress">IP address to dispatch the datagram to.</param>
        /// <param name="port">Port to dispatch the datagram to.</param>
        public static void Dispatch(this UdpClient udpClient, DmContext context, IPAddress ipAddress, int port, byte[] datagramBytes)
        {
            try
            {
                if (udpClient == null)
                {
                    throw new Exception("WriteBytesFrame: client can not be null.");
                }

                var frameBody = new FrameBody(datagramBytes);
                var frameBytes = AssembleFrame(context, frameBody);
                udpClient.Send(frameBytes, frameBytes.Length, new IPEndPoint(ipAddress, port));
            }
            catch (Exception ex)
            {
                context.Messenger.InvokeOnException(context, ex);
                throw;
            }
        }

        /// <summary>
        /// Sends a one-time fire-and-forget byte array datagram. These are and handled in ProcessDatagramCallback().
        /// When a raw byte array is use, all json serialization is skipped and checks for this datagram type are prioritized for performance.
        /// </summary>
        /// <param name="udpClient">The client to send the data on.</param>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="endpoint">Endpoint to dispatch the datagram to.</param>
        /// <param name="datagramBytes">The bytes will make up the body of the frame which is written.</param>
        public static void Dispatch(this UdpClient udpClient, DmContext context, IPEndPoint endpoint, byte[] datagramBytes)
        {
            try
            {
                if (udpClient == null)
                {
                    throw new Exception("WriteBytesFrame: client can not be null.");
                }

                var frameBody = new FrameBody(datagramBytes);
                var frameBytes = AssembleFrame(context, frameBody);
                udpClient.Send(frameBytes, frameBytes.Length, endpoint);
            }
            catch (Exception ex)
            {
                context.Messenger.InvokeOnException(context, ex);
                throw;
            }
        }

        #endregion

        private static byte[] AssembleFrame(DmContext context, FrameBody frameBody)
        {
            try
            {
                var frameBodyBytes = Serialization.SerializeToByteArray(frameBody);

                var compressedFrameBodyBytes = context.GetCompressionProvider()?.Compress(context, frameBodyBytes)
                    ?? Compression.Compress(frameBodyBytes);

                compressedFrameBodyBytes = context.GetCryptographyProvider()?.Encrypt(context, compressedFrameBodyBytes)
                    ?? compressedFrameBodyBytes;

                var grossFrameSize = compressedFrameBodyBytes.Length + NtFrameDefaults.FRAME_HEADER_SIZE;
                var grossFrameBytes = new byte[grossFrameSize];
                var frameCrc = CRC16.ComputeChecksum(compressedFrameBodyBytes);

                Buffer.BlockCopy(BitConverter.GetBytes(NtFrameDefaults.FRAME_DELIMITER), 0, grossFrameBytes, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(grossFrameSize), 0, grossFrameBytes, 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(frameCrc), 0, grossFrameBytes, 8, 2);
                Buffer.BlockCopy(compressedFrameBodyBytes, 0, grossFrameBytes, NtFrameDefaults.FRAME_HEADER_SIZE, compressedFrameBodyBytes.Length);

                return grossFrameBytes;
            }
            catch (Exception ex)
            {
                context.Messenger.InvokeOnException(context, ex);
                throw;
            }
        }

        private static void SkipFrame(DmContext context, ref FrameBuffer frameBuffer)
        {
            try
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
            catch (Exception ex)
            {
                context.Messenger.InvokeOnException(context, ex);
                throw;
            }
        }

        private static void ProcessFrameBuffer(DmContext context, FrameBuffer frameBuffer)
        {
            try
            {
                if (frameBuffer.FrameBuilderLength + frameBuffer.ReceiveBufferUsed >= frameBuffer.FrameBuilder.Length)
                {
                    Array.Resize(ref frameBuffer.FrameBuilder, frameBuffer.FrameBuilderLength + frameBuffer.ReceiveBufferUsed);
                }

                Buffer.BlockCopy(frameBuffer.ReceiveBuffer, 0, frameBuffer.FrameBuilder, frameBuffer.FrameBuilderLength, frameBuffer.ReceiveBufferUsed);

                frameBuffer.FrameBuilderLength += frameBuffer.ReceiveBufferUsed;

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
                        SkipFrame(context, ref frameBuffer);
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
                        SkipFrame(context, ref frameBuffer);
                        continue;
                    }

                    var netFrameSize = grossFrameSize - NtFrameDefaults.FRAME_HEADER_SIZE;
                    var compressedFrameBodyBytes = new byte[netFrameSize];
                    Buffer.BlockCopy(frameBuffer.FrameBuilder, NtFrameDefaults.FRAME_HEADER_SIZE, compressedFrameBodyBytes, 0, netFrameSize);

                    compressedFrameBodyBytes = context.GetCryptographyProvider()?.Decrypt(context, compressedFrameBodyBytes)
                        ?? compressedFrameBodyBytes;

                    var frameBodyBytes = context.GetCompressionProvider()?.Decompress(context, compressedFrameBodyBytes)
                        ?? Compression.Decompress(compressedFrameBodyBytes);
                    var frameBody = Serialization.DeserializeToObject<FrameBody>(frameBodyBytes);

                    //Zero out the consumed portion of the frame buffer - more for fun than anything else.
                    Array.Clear(frameBuffer.FrameBuilder, 0, grossFrameSize);

                    Buffer.BlockCopy(frameBuffer.FrameBuilder, grossFrameSize, frameBuffer.FrameBuilder, 0, frameBuffer.FrameBuilderLength - grossFrameSize);
                    frameBuffer.FrameBuilderLength -= grossFrameSize;

                    var datagram = ExtractFrameDatagram(context, frameBody);

                    if (datagram is DmDatagramBytes dmDatagramBytes)
                    {
                        if (context.Messenger.AsynchronousDatagramProcessing)
                        {
                            //Keep a reference to the datagram that we are going to perform an async operation on.
                            var asynchronousDatagramBytes = dmDatagramBytes;
                            Task.Run(() =>
                            {
                                context.Messenger.ProcessFrameDatagramByConvention(context, asynchronousDatagramBytes);
                            });
                        }
                        else
                        {
                            context.Messenger.ProcessFrameDatagramByConvention(context, dmDatagramBytes);
                        }
                    }
                    else if (datagram is DmKeepAliveMessage dmKeepAliveMessage)
                    {
                        Task.Run(() =>
                        {
                            //Discard keep-alive message.
                            context.Messenger.InvokeOnKeepAlive(context, dmKeepAliveMessage);
                            context.Dispatch(new DmKeepAliveReplyMessage()); //Reply to the keep-alive request.
                        });
                    }
                    else if (datagram is DmKeepAliveReplyMessage dmKeepAliveReply)
                    {
                        Task.Run(() =>
                        {
                            //Discard keep-alive message.
                            context.Messenger.InvokeOnKeepAlive(context, dmKeepAliveReply);
                        });
                    }
                    else if (datagram is IDmDatagram dmDatagram)
                    {
                        if (context.Messenger.AsynchronousDatagramProcessing)
                        {
                            //Keep a reference to the datagram that we are going to perform an async operation on.
                            var asynchronousDatagram = dmDatagram;
                            Task.Run(() =>
                            {
                                context.Messenger.ProcessFrameDatagramByConvention(context, asynchronousDatagram);
                            });
                        }
                        else
                        {
                            context.Messenger.ProcessFrameDatagramByConvention(context, dmDatagram);
                        }
                    }
                    else
                    {
                        throw new Exception("ProcessFrameBuffer: Encountered undefined datagram type.");
                    }
                }
            }
            catch (Exception ex)
            {
                context.Messenger.InvokeOnException(context, ex);
                throw;
            }
        }

        /// <summary>
        /// Uses the ObjectType to determine the type of the frame payload and then uses reflection
        /// to deserialize the json to a datagram containing that type.
        /// Deserialization is skipped when the type is byte[].
        /// </summary>
        private static IDmDatagram ExtractFrameDatagram(DmContext context, FrameBody frame)
        {
            try
            {
                if (frame.ObjectType == "byte[]")
                {
                    return new DmDatagramBytes(frame.Bytes);
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

                var json = Encoding.UTF8.GetString(frame.Bytes);

                var serializationProvider = context.GetSerializationProvider();

                if (genericToObjectMethod != null)
                {
                    //Call the generic deserialization:
                    return (IDmDatagram?)genericToObjectMethod.Invoke(null, [serializationProvider, json])
                        ?? throw new Exception($"Extraction payload can not be null.");
                }

                var genericType = Type.GetType(frame.ObjectType)
                    ?? throw new Exception($"Unknown extraction payload type [{frame.ObjectType}].");

                var toObjectMethod = typeof(Serialization).GetMethod("RmDeserializeFramePayloadToObject")
                        ?? throw new Exception($"Could not resolve RmDeserializeFramePayloadToObject().");

                genericToObjectMethod = toObjectMethod.MakeGenericMethod(genericType);

                _reflectionCache.Use((o) => o.TryAdd(cacheKey, genericToObjectMethod));

                //Call the generic deserialization:
                return (IDmDatagram?)genericToObjectMethod.Invoke(null, [serializationProvider, json])
                    ?? throw new Exception($"Extraction payload can not be null.");
            }
            catch (Exception ex)
            {
                context.Messenger.InvokeOnException(context, ex);
                throw;
            }
        }
    }
}
