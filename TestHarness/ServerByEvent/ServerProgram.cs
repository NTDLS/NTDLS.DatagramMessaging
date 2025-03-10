using NTDLS.DatagramMessaging;
using Shared;

namespace ServerByEvent
{
    internal class ServerProgram
    {
        static void Main()
        {
            var dmServer = new DmServer(1234);

            dmServer.OnNotificationReceived += UdpManager_OnNotificationReceived;

            Console.ReadLine();

            dmServer.Stop();
        }

        private static void UdpManager_OnNotificationReceived(DmContext context, IDmNotification payload)
        {
            if (payload is DmNotificationBytes bytes)
            {
                context.Dispatch(bytes.Bytes); //Echo the payload back to the sender.

                Console.WriteLine($"Received {bytes.Bytes.Length} bytes.");
            }
            else if (payload is MyFirstUDPPacket myFirstUDPPacket)
            {
                context.Dispatch(myFirstUDPPacket); //Echo the payload back to the sender.

                Console.WriteLine($"{myFirstUDPPacket.Message}->{myFirstUDPPacket.UID}->{myFirstUDPPacket.TimeStamp}");
            }
        }
    }
}
