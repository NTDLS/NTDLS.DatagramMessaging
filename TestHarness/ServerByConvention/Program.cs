using NTDLS.DatagramMessaging;
using Shared;

namespace ServerByConvention
{
    internal class Program
    {
        static void Main()
        {
            var dmServer = new DmServer(1234);

            dmServer.AddHandler(new HandlePackets());

            Console.ReadLine();

            dmServer.Stop();
        }

        private class HandlePackets : IDmMessageHandler
        {
            public static void ProcessFrameNotificationCallback(DmContext context, DmNotificationBytes bytes)
            {
                context.Dispatch(bytes.Bytes); //Echo the payload back to the sender.

                Console.WriteLine($"Received {bytes.Bytes.Length} bytes.");
            }

            public static void ProcessFrameNotificationCallback(DmContext context, MyFirstUDPPacket payload)
            {
                context.Dispatch(payload); //Echo the payload back to the sender.
                Console.WriteLine($"{payload.Message}->{payload.UID}->{payload.TimeStamp}");
            }
        }
    }
}
