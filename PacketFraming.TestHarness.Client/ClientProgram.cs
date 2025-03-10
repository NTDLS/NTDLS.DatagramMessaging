using NTDLS.DatagramMessaging;
using PacketFraming.TestHarness.Shared;

namespace PacketFraming.TestHarness.Client
{
    internal class ClientProgram
    {
        static void Main()
        {
            var dm = new DatagramMessenger(0);

            dm.OnNotificationReceived += UdpManager_OnNotificationReceived;

            var rand = new Random();

            int packetNumber = 0;

            while (true)
            {
                dm.Dispatch("127.0.0.1", 1234,
                    new MyFirstUDPPacket($"Packet#:{packetNumber++} "));

                byte[] randomBytes = new byte[100];
                rand.NextBytes(randomBytes); // Fill array with random values

                dm.Dispatch("127.0.0.1", 1234, randomBytes);

                Thread.Sleep(10);
            }

            Console.ReadLine();

            dm.Stop();
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
