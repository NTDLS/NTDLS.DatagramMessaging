using NTDLS.UDPPacketFraming;
using PacketFraming.TestHarness.Shared;

namespace PacketFraming.TestHarness.Client
{
    internal class Program
    {
        static void Main()
        {
            var udpManager = new UdpMessageManager();

            int packetNumber = 0;

            while (true)
            {
                udpManager.WriteMessage("127.0.0.1", 1234,
                    new MyFirstUPDPacket($"Packet#:{packetNumber++} "));

                Thread.Sleep(100);
            }
        }
    }
}
