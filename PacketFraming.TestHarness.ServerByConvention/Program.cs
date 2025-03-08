using NTDLS.DatagramMessaging;
using PacketFraming.TestHarness.Shared;

namespace PacketFraming.TestHarness.ServerByConvention
{
    internal class Program
    {
        static void Main()
        {
            var dm = new DatagramMessenger(1234);

            dm.AddHandler(new HandlePackets());
        }

        private class HandlePackets : IDmMessageHandler
        {
            public static void ProcessFrameNotificationCallback(DmContext context, DmNotificationBytes bytes)
            {
                Console.WriteLine($"Received {bytes.Bytes.Length} bytes.");
            }

            public static void ProcessFrameNotificationCallback(DmContext context, MyFirstUDPPacket payload)
            {
                Console.WriteLine($"{payload.Message}->{payload.UID}->{payload.TimeStamp}");
            }
        }
    }
}
