using NTDLS.DatagramMessaging;
using PacketFraming.TestHarness.Shared;

namespace PacketFraming.TestHarness.Server
{
    internal class Program
    {
        static void Main()
        {
            var udpManager = new DmMessenger(1234);

            udpManager.AddHandler(new HandlePackets());
        }

        private class HandlePackets : IDmMessageHandler
        {
            public static void ProcessFrameNotificationCallback(DmContext context, MyFirstUDPPacket payload)
            {
                Console.WriteLine($"{payload.Message}->{payload.UID}->{payload.TimeStamp}");
            }
        }
    }
}
