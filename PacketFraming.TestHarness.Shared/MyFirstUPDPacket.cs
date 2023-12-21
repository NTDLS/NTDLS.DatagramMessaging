using NTDLS.UDPPacketFraming.Payloads;

namespace PacketFraming.TestHarness.Shared
{
    public class MyFirstUPDPacket: IUDPPayloadNotification
    {
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
        public Guid UID { get; set; } = Guid.NewGuid();
        public string Message { get; set; } = string.Empty;

        public MyFirstUPDPacket()
        {
        }

        public MyFirstUPDPacket(string message)
        {
            Message = message;
        }
    }
}
