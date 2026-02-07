# NTDLS.DatagramMessaging

📦 Be sure to check out the NuGet package: https://www.nuget.org/packages/NTDLS.DatagramMessaging

NTDLS.DatagramMessaging is a set of classes and extensions methods that allow you to send/receive
UDP packets with ease. It handles corruption checks, concatenation, fragmentation, serialization,
compression and encryption.

## UDP Sever (Event based):
> Here we are instantiating a DmMessenger and giving it a listen port. This will cause the
> manager to go into listen mode. Any received messages will handled by the OnDatagramReceived event.
```csharp
static void Main()
{
    using var messenger = new DmMessenger(1234);

    messenger.OnDatagramReceived += messenger_OnDatagramReceived;

    Console.WriteLine("Press enter to quit.");
    Console.ReadLine();
}

private static void Messenger_OnDatagramReceived(DmContext context, IDmDatagram datagram)
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
    var messenger = new DmMessenger(1234);

    messenger.AddHandler(new HandlePackets());

    Console.WriteLine("Press enter to quit.");
    Console.ReadLine();
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
> messenger is in write-only mode. We are going to loop and send frames containing serialized MyFirstUDPPacket.
> Note that the we could also specify a handler for convention based message handling.
```csharp
static void Main()
{
    using var messenger = new DmMessenger();

    messenger.OnDatagramReceived += messenger_OnDatagramReceived;

    int packetNumber = 0;

    var endpointCtx = messenger.GetEndpointContext("127.0.0.1", 1234);

    while (true)
    {
        messenger.Dispatch(new MyFirstUDPPacket($"Packet#:{packetNumber++} "), endpointCtx);
        Thread.Sleep(10);
    }

    Console.WriteLine("Press enter to quit.");
    Console.ReadLine();
}
```

## Supporting Code:
> The class that we are going to be serializing and deserializing in the examples.
```csharp
public class MyFirstUDPPacket : IDmDatagram
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
