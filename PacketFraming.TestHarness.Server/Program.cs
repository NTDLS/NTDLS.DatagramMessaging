using NTDLS.UDPPacketFraming;
using NTDLS.UDPPacketFraming.Payloads;
using PacketFraming.TestHarness.Shared;

namespace PacketFraming.TestHarness.Server
{
    internal class Program
    {
        static void Main()
        {
            var udpManager = new UdpMessageManager(1234, ProcessFrameNotificationCallback);
        }

        private static void ProcessFrameNotificationCallback(IUDPPayloadNotification payload)
        {
            if (payload is MyFirstUPDPacket myFirstUPDPacket)
            {
                Console.WriteLine($"{myFirstUPDPacket.Message}->{myFirstUPDPacket.UID}->{myFirstUPDPacket.TimeStamp}");
            }
        }
    }
}
