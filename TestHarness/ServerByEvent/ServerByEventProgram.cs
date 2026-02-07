using NTDLS.DatagramMessaging;
using Shared;

namespace ServerByEvent
{
    internal class ServerByEventProgram
    {
        static void Main()
        {
            var messenger = new DmMessenger(TestHarnessConstants.ServerPort);
            messenger.SetCompressionProvider(new DmBrotliCompressionProvider());
            messenger.SetCryptographyProvider(new DmAesCryptographyProvider("This is my password"));

            messenger.OnDatagramReceived += UdpManager_OnDatagramReceived;
            messenger.OnException += DmServer_OnException;

            messenger.Stop();
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
