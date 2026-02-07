# NTDLS.DatagramMessaging

📦 Be sure to check out the NuGet package: https://www.nuget.org/packages/NTDLS.DatagramMessaging

NTDLS.DatagramMessaging is a set of classes and extensions methods that allow you to send/receive
UDP packets with ease. It handles corruption checks, concatenation, fragmentation, serialization
and compression with optional overloads.

## UDP Sever (Event based):
> Here we are instantiating a DmMessenger and giving it a listen port. This will cause the
> manager to go into listen mode. Any received messages will handled by the OnDatagramReceived event.
```csharp
static void Main()
{
    var udpManager = new dmClient(1234);

    udpManager.OnDatagramReceived += UdpManager_OnDatagramReceived;

    udpManager.Stop();
}

private static void UdpManager_OnDatagramReceived(DmContext context, IDmDatagram datagram)
{
    if (datagram is MyFirstUDPPacket myFirstUDPPacket)
    {
        context.Dispatch(myFirstUDPPacket); //Echo the datagram back to the sender.
        Console.WriteLine($"{myFirstUDPPacket.Message}->{myFirstUDPPacket.UID}->{myFirstUDPPacket.TimeStamp}");
    }
}
```

## UDP Sever (Convention based):
> Here we are instantiating a DmMessenger and giving it a listen port. This will cause the
> manager to go into listen mode. Any received messages will handled by the class HandlePackets
> which was suppled to the UDP messenger by a call to AddHandler().
```csharp
static void Main()
{
    var udpManager = new DmClient(1234);

    udpManager.AddHandler(new HandlePackets());

    Console.ReadLine();

    udpManager.Stop();
}

private class HandlePackets : IDmMessageHandler
{
    private class HandlePackets : IDmMessageHandler
    {
        public static void DatagramHandler(DmContext context, MyFirstUDPPacket datagram)
        {
            context.Dispatch(datagram); //Echo the datagram back to the sender.
            Console.WriteLine($"{datagram.Message}->{datagram.UID}->{datagram.TimeStamp}");
        }
    }
}
```

## UDP Client:
> Here we are instantiating a DmMessenger without a listen port. This means that this this
> manager is in write-only mode. Note that we could also receive data by calling Listen().
We are going to loop and send frames containing serialized MyFirstUDPPacket.
```csharp
static void Main()
{
    var udpManager = new DmClient("127.0.0.1", 1234);

    udpManager.OnDatagramReceived += UdpManager_OnDatagramReceived;

    int packetNumber = 0;

    while (true)
    {
        udpManager.Dispatch(new MyFirstUDPPacket($"Packet#:{packetNumber++} "));
        Thread.Sleep(10);
    }

    Console.ReadLine();

    udpManager.Stop();
}
```

## Supporting Code:
> The class that we are going to be serializing and deserializing in the examples.
```csharp
public class MyFirstUDPPacket: IDmDatagram
{
    public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
    public Guid UID { get; set; } = Guid.NewGuid();
    public string Message { get; set; } = string.Empty;

    public MyFirstUDPPacket()
    {
    }

    public MyFirstUDPPacket(string message)
    {
        Message = message;
    }
}
```
