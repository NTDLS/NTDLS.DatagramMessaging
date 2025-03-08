using NTDLS.DatagramMessaging;
using PacketFraming.TestHarness.Shared;

namespace PacketFraming.TestHarness.Client
{
    internal class Program
    {
        static void Main()
        {
            var dm = new DatagramMessenger();

            var rand = new Random();

            int packetNumber = 0;

            while (true)
            {
                dm.WriteMessage("127.0.0.1", 1234,
                    new MyFirstUDPPacket($"Packet#:{packetNumber++} "));

                byte[] randomBytes = new byte[100];
                rand.NextBytes(randomBytes); // Fill array with random values

                dm.WriteBytes("127.0.0.1", 1234, randomBytes);

                Thread.Sleep(10);
            }
        }
    }
}
