using NTDLS.DatagramMessaging;

namespace Shared
{
    public class MySecondUDPPacket : IDmDatagram
    {
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
        public Guid UID { get; set; } = Guid.NewGuid();
        public string Message { get; set; } = string.Empty;

        public MySecondUDPPacket()
        {
        }

        public MySecondUDPPacket(string message)
        {
            Message = message;
        }
    }
}
