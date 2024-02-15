using NTDLS.DatagramMessaging;

namespace PacketFraming.TestHarness.Shared
{
    public class MyFirstUPDPacket : IDmNotification
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
