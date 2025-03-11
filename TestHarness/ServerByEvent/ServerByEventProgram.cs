using NTDLS.DatagramMessaging;
using Shared;

namespace ServerByEvent
{
    internal class ServerByEventProgram
    {
        static void Main()
        {
            var dmServer = new DmServer(1234);

            dmServer.OnDatagramReceived += UdpManager_OnDatagramReceived;
            dmServer.OnException += DmServer_OnException;

            Console.ReadLine();

            dmServer.Stop();
        }

        private static void DmServer_OnException(DmContext? context, Exception ex)
        {
            Console.WriteLine(ex.GetBaseException().Message);
        }

        private static void UdpManager_OnDatagramReceived(DmContext context, IDmDatagram datagram)
        {
            if (datagram is DmBytesDatagram bytes)
            {
                context.Dispatch(bytes.Bytes); //Echo the datagram back to the sender.

                Console.WriteLine($"Received {bytes.Bytes.Length} bytes.");
            }
            else if (datagram is MyFirstUDPPacket myFirstUDPPacket)
            {
                context.Dispatch(myFirstUDPPacket); //Echo the datagram back to the sender.

                Console.WriteLine($"{myFirstUDPPacket.Message}->{myFirstUDPPacket.UID}->{myFirstUDPPacket.TimeStamp}");
            }
        }
    }
}
