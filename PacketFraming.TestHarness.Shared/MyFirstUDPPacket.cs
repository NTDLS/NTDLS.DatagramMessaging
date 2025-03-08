using NTDLS.DatagramMessaging;

namespace PacketFraming.TestHarness.Shared
{
    public class MyFirstUDPPacket : IDmNotification
    {
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
        public Guid UID { get; set; } = Guid.NewGuid();
        public string Message { get; set; } = string.Empty;

        public MyFirstUDPPacket()
        {
        }

        public MyFirstUDPPacket(string message)
        {
            Message = message;
        }
    }
}
