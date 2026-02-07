using NTDLS.DatagramMessaging;
using Shared;

namespace Client
{
    internal class ClientProgram
    {
        static void Main()
        {
            var listenPort = DmMessenger.GetRandomUnusedUdpPort();

            var messenger = new DmMessenger(listenPort);
            Console.WriteLine($"Client listening on port:{listenPort}");

            messenger.SetCompressionProvider(new DmBrotliCompressionProvider());
            messenger.SetCryptographyProvider(new DmAesCryptographyProvider("This is my password"));

            messenger.OnDatagramReceived += UdpManager_OnDatagramReceived;
            messenger.OnException += DmClient_OnException;

            var rand = new Random();

            int packetNumber = 0;

            var endpoint = messenger.GetEndpointContext("127.0.0.1", TestHarnessConstants.ServerPort);

            while (true)
            {
                messenger.Dispatch(new MyFirstUDPPacket($"Packet#:{packetNumber++} "), endpoint);
                messenger.Dispatch(new MySecondUDPPacket($"Packet#:{packetNumber++} "), endpoint);

                var randomBytes = new byte[100];
                rand.NextBytes(randomBytes); // Fill array with random values
                messenger.Dispatch(randomBytes, endpoint);

                Thread.Sleep(10);
            }

            Console.ReadLine();

            messenger.Stop();
        }

        private static void DmClient_OnException(DmContext? context, Exception ex)
        {
            Console.WriteLine(ex.GetBaseException().Message);
        }

        private static void UdpManager_OnDatagramReceived(DmContext context, IDmDatagram datagram)
        {
            if (datagram is DmBytesDatagram bytes)
            {
                Console.WriteLine($"Received {bytes.Bytes.Length} bytes.");
            }
            else if (datagram is MyFirstUDPPacket myFirstUDPPacket)
            {
                Console.WriteLine($"{myFirstUDPPacket.Message}->{myFirstUDPPacket.UID}->{myFirstUDPPacket.TimeStamp}");
            }
        }
    }
}
