﻿using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace NTDLS.DatagramMessaging.Framing
{
    /// <summary>
    /// Auto-resizing frame buffer for receiving and frame reassembly.
    /// </summary>
    public class FrameBuffer
    {
        /// <summary>
        /// The initial size of the receive buffer. If the buffer ever gets full while receiving data it will be automatically resized up to MaxReceiveBufferSize.
        /// </summary>
        public int InitialReceiveBufferSize { get; private set; } = 16 * 1024;

        /// <summary>
        ///The maximum size of the receive buffer. If the buffer ever gets full while receiving data it will be automatically resized up to MaxReceiveBufferSize.
        /// </summary>
        public int MaxReceiveBufferSize { get; private set; } = 1024 * 1024;

        /// <summary>
        ///The growth rate of the auto-resizing for the receive buffer.
        /// </summary>
        public double ReceiveBufferGrowthRate { get; private set; } = 0.2;

        /// <summary>
        /// The number of bytes in the current receive buffer.
        /// </summary>
        public int ReceiveBufferUsed = 0;
        /// <summary>
        /// The current receive buffer. May be more than one frame or even a partial frame.
        /// </summary>
        public byte[] ReceiveBuffer;

        /// <summary>
        /// The buffer used to build a full message from the frame. This will be automatically resized if its too small.
        /// </summary>
        public byte[] FrameBuilder;

        /// <summary>
        /// The length of the data currently contained in the FrameBuilder.
        /// </summary>
        public int FrameBuilderLength = 0;

        internal bool ReadData(UdpClient udpClient, ref IPEndPoint endPoint)
        {
            try
            {
                try
                {
                    ReceiveBuffer = udpClient.Receive(ref endPoint);
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode != SocketError.Interrupted)
                    {
                        throw;
                    }
                    return false; //Graceful disconnect.
                }
                ReceiveBufferUsed = ReceiveBuffer.Length;

                if (ReceiveBufferUsed == 0)
                {
                    return false; //Graceful disconnect.
                }
                if (ReceiveBufferUsed == ReceiveBuffer.Length && ReceiveBufferUsed < MaxReceiveBufferSize)
                {
                    AutoGrowReceiveBuffer();
                }
            }
            catch (IOException)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Resizes the receive buffer by the given growth rate, up to the maxFrameBufferSize.
        /// </summary>
        private void AutoGrowReceiveBuffer()
        {
            if (ReceiveBuffer.Length < MaxReceiveBufferSize)
            {
                int newSize = (int)(ReceiveBuffer.Length + ReceiveBuffer.Length * ReceiveBufferGrowthRate);
                if (newSize > MaxReceiveBufferSize)
                {
                    newSize = MaxReceiveBufferSize;
                }
                Array.Resize(ref ReceiveBuffer, newSize);
            }
        }

        /// <summary>
        /// Instantiates a new frame buffer with a pre-defined size.
        /// </summary>
        public FrameBuffer(int initialReceiveBufferSize, int maxReceiveBufferSize, double receiveBufferGrowthRate = 0.2)
        {
            InitialReceiveBufferSize = initialReceiveBufferSize;
            MaxReceiveBufferSize = maxReceiveBufferSize;
            ReceiveBufferGrowthRate = receiveBufferGrowthRate;

            ReceiveBuffer = new byte[initialReceiveBufferSize];
            FrameBuilder = new byte[initialReceiveBufferSize];
        }

        /// <summary>
        /// Instantiates a new frame buffer with a default initial size of 16KB, a max size of 1MB and a growth rate of 0.1 (10%).
        /// </summary>
        public FrameBuffer()
        {
            ReceiveBuffer = new byte[InitialReceiveBufferSize];
            FrameBuilder = new byte[InitialReceiveBufferSize];
        }
    }
}
