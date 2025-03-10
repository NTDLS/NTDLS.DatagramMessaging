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

            Console.ReadLine();

            dm.Stop();
        }

        private class HandlePackets : IDmMessageHandler
        {
            public static void ProcessFrameNotificationCallback(DmContext context, DmNotificationBytes bytes)
            {
                context.WriteReplyBytes(bytes.Bytes); //Echo the payload back to the sender.

                Console.WriteLine($"Received {bytes.Bytes.Length} bytes.");
            }

            public static void ProcessFrameNotificationCallback(DmContext context, MyFirstUDPPacket payload)
            {
                context.WriteReplyMessage(payload); //Echo the payload back to the sender.
                Console.WriteLine($"{payload.Message}->{payload.UID}->{payload.TimeStamp}");
            }
        }
    }
}
