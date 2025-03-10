using NTDLS.DatagramMessaging;
using PacketFraming.TestHarness.Shared;

namespace PacketFraming.TestHarness.Client
{
    internal class ClientProgram
    {
        static void Main()
        {
            var dmClient = new DmClient("127.0.0.1", 1234);

            dmClient.OnNotificationReceived += UdpManager_OnNotificationReceived;

            var rand = new Random();

            int packetNumber = 0;

            while (true)
            {
                dmClient.Dispatch(new MyFirstUDPPacket($"Packet#:{packetNumber++} "));

                byte[] randomBytes = new byte[100];
                rand.NextBytes(randomBytes); // Fill array with random values

                dmClient.Dispatch(randomBytes);

                Thread.Sleep(10);
            }

            Console.ReadLine();

            dmClient.Stop();
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
