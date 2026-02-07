using NTDLS.DatagramMessaging;
using Shared;

namespace ServerByConvention
{
    internal class ServerByConventionProgram
    {
        static void Main()
        {
            var messenger = new DmMessenger(TestHarnessConstants.ServerPort);
            messenger.SetCompressionProvider(new DmBrotliCompressionProvider());
            messenger.SetCryptographyProvider(new DmAesCryptographyProvider("This is my password"));

            messenger.OnException += DmServer_OnException;
            messenger.AddHandler(new HandlePackets());

            Console.ReadLine();

            messenger.Stop();
        }

        private static void DmServer_OnException(DmContext? context, Exception ex)
        {
            Console.WriteLine(ex.GetBaseException().Message);
        }

        private class HandlePackets : IDmDatagramHandler
        {
            public static void DatagramHandler(DmContext context, DmBytesDatagram datagram)
            {
                context.Dispatch(datagram.Bytes); //Echo the datagram back to the sender.

                Console.WriteLine($"Received {datagram.Bytes.Length} bytes.");
            }

            public static void DatagramHandler(DmContext context, MyFirstUDPPacket datagram)
            {
                Console.WriteLine($"{datagram.Message}->{datagram.UID}->{datagram.TimeStamp}");

                datagram.Message += " - Received by ServerByConvention";
                context.Dispatch(datagram); //Echo the datagram back to the sender.
            }

            public static void DatagramHandler(DmContext context, MySecondUDPPacket datagram)
            {
                Console.WriteLine($"{datagram.Message}->{datagram.UID}->{datagram.TimeStamp}");

                datagram.Message += " - Received by ServerByConvention";
                context.Dispatch(datagram); //Echo the datagram back to the sender.
            }

        }
    }
}
