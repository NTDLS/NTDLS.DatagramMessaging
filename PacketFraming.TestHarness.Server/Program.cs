using NTDLS.DatagramMessaging;
using PacketFraming.TestHarness.Shared;

namespace PacketFraming.TestHarness.Server
{
    internal class Program
    {
        static void Main()
        {
            var udpManager = new DmMessenger(1234);

            udpManager.OnNotificationReceived += UdpManager_OnNotificationReceived;
        }

        private static void UdpManager_OnNotificationReceived(DmContext context, IDmNotification payload)
        {
            if (payload is MyFirstUDPPacket myFirstUDPPacket)
            {
                Console.WriteLine($"{myFirstUDPPacket.Message}->{myFirstUDPPacket.UID}->{myFirstUDPPacket.TimeStamp}");
            }
        }
    }
}
