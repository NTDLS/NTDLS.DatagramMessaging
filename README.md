# NTDLS.DatagramMessaging

📦 Be sure to check out the NuGet package: https://www.nuget.org/packages/NTDLS.DatagramMessaging

NTDLS.DatagramMessaging is a set of classes and extensions methods that allow you to send/receive
UDP packets with ease. It handles corruption checks, concatenation, fragmentation, serialization
and compression with optional overloads.

## UDP Sever (Event based):
> Here we are instantiating a DmMessenger and giving it a listen port. This will cause the
> manager to go into listen mode. Any received messages will handled by the OnNotificationReceived event.
```csharp
static void Main()
{
    var dm = new DatagramMessenger(1234);

    dm.OnNotificationReceived += UdpManager_OnNotificationReceived;
}

private static void UdpManager_OnNotificationReceived(DmContext context, IDmNotification payload)
{
    if (payload is MyFirstUDPPacket myFirstUDPPacket)
    {
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
    var dm = new DatagramMessenger(1234);

    dm.AddHandler(new HandlePackets());
}

private class HandlePackets : IDmMessageHandler
{
    public static void ProcessFrameNotificationCallback(DmContext context, MyFirstUDPPacket payload)
    {
        Console.WriteLine($"{payload.Message}->{payload.UID}->{payload.TimeStamp}");
    }
}
```

## UDP Client:
> Here we are instantiating a DmMessenger without a a listen port. This means that this this
> manager is in write-only mode. We are going to loop and send frames containing serialized MyFirstUDPPacket.
```csharp
static void Main()
{
    var dm = new DatagramMessenger();

    int packetNumber = 0;

    while (true)
    {
        dm.WriteMessage("127.0.0.1", 1234,
            new MyFirstUDPPacket($"Packet#:{packetNumber++} "));

        Thread.Sleep(10);
    }
}
```

## Supporting Code:
> The class that we are going to be serializing and deserializing in the examples.
```csharp
public class MyFirstUDPPacket: IDmNotification
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
