using NTDLS.DatagramMessaging;
using Shared;

namespace ServerByConvention
{
    internal class Program
    {
        static void Main()
        {
            var dmServer = new DmServer(1234);
            dmServer.OnException += DmServer_OnException;

            dmServer.AddHandler(new HandlePackets());

            Console.ReadLine();

            dmServer.Stop();
        }

        private static void DmServer_OnException(DmContext? context, Exception ex)
        {
            Console.WriteLine(ex.GetBaseException().Message);
        }

        private class HandlePackets : IDmMessageHandler
        {
            public static void ProcessFrameDatagramCallback(DmContext context, DmDatagramBytes datagram)
            {
                context.Dispatch(datagram.Bytes); //Echo the datagram back to the sender.

                Console.WriteLine($"Received {datagram.Bytes.Length} bytes.");
            }

            public static void ProcessFrameDatagramCallback(DmContext context, MyFirstUDPPacket datagram)
            {
                context.Dispatch(datagram); //Echo the datagram back to the sender.
                Console.WriteLine($"{datagram.Message}->{datagram.UID}->{datagram.TimeStamp}");
            }
        }
    }
}
