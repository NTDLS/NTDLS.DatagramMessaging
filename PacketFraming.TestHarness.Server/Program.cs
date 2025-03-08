using NTDLS.DatagramMessaging;
using PacketFraming.TestHarness.Shared;

namespace PacketFraming.TestHarness.Server
{
    internal class Program
    {
        static void Main()
        {
            var dm = new DatagramMessenger(1234);

            dm.OnNotificationReceived += UdpManager_OnNotificationReceived;
        }

        private static void UdpManager_OnNotificationReceived(DmContext context, IDmNotification payload)
        {
            if (payload is DmNotificationBytes bytes)
            {
                Console.WriteLine($"Received {bytes.Bytes.Length} bytes.");
            }
            else if (payload is MyFirstUDPPacket myFirstUDPPacket)
            {
                Console.WriteLine($"{myFirstUDPPacket.Message}->{myFirstUDPPacket.UID}->{myFirstUDPPacket.TimeStamp}");
            }
        }
    }
}
