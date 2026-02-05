using NTDLS.DatagramMessaging;
using Shared;

namespace Client
{
    internal class ClientProgram
    {
        static void Main()
        {
            var dmClient = new DmClient();

            dmClient.SetCompressionProvider(new DmBrotliCompressionProvider());
            dmClient.SetCryptographyProvider(new DmAesCryptographyProvider("This is my password"));

            dmClient.Connect("127.0.0.1", TestHarnessConstants.ServerPort);
            dmClient.Listen(DmClient.GetRandomUnusedUdpPort());

            dmClient.OnDatagramReceived += UdpManager_OnDatagramReceived;
            dmClient.OnException += DmClient_OnException;

            var rand = new Random();

            int packetNumber = 0;

            dmClient.StartKeepAlive();

            while (true)
            {
                dmClient.Dispatch(new MyFirstUDPPacket($"Packet#:{packetNumber++} "));
                dmClient.Dispatch(new MySecondUDPPacket($"Packet#:{packetNumber++} "));

                var randomBytes = new byte[100];
                rand.NextBytes(randomBytes); // Fill array with random values

                dmClient.Dispatch(randomBytes);

                Thread.Sleep(10);
            }

            Console.ReadLine();

            dmClient.Stop();
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
