using NTDLS.DatagramMessaging.Internal;
using ProtoBuf;
using System;
using System.Text;

namespace NTDLS.DatagramMessaging.Framing
{
    /// <summary>
    /// Comprises the body of the frame. Contains the datagram payload and all information needed to deserialize it.
    /// </summary>
    [Serializable]
    [ProtoContract]
    public class FrameBody
    {
        /// <summary>
        /// The full assembly qualified name of the type of the payload.
        /// </summary>
        [ProtoMember(1)]
        public string ObjectType { get; set; } = string.Empty;

        /// <summary>
        /// Sometimes we just need to send a byte array without all the overhead of json, that's when we use BytesPayload.
        /// </summary>
        [ProtoMember(2)]
        public byte[] Bytes { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Instantiates a frame payload with a serialized datagram.
        /// </summary>
        public FrameBody(IDmSerializationProvider? serializationProvider, IDmDatagram datagram)
        {
            ObjectType = Reflection.GetAssemblyQualifiedTypeNameWithClosedGenerics(datagram);
            Bytes = Encoding.UTF8.GetBytes(Serialization.RmSerializeFramePayloadToText(serializationProvider, datagram));
        }

        /// <summary>
        /// Instantiates a frame payload using a raw byte array.
        /// </summary>
        public FrameBody(byte[] datagramBytes)
        {
            ObjectType = "byte[]";
            Bytes = datagramBytes;
        }

        /// <summary>
        /// Instantiates a frame payload.
        /// </summary>
        public FrameBody()
        {
        }
    }
}
