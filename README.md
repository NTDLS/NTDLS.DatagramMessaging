# NTDLS.UDPPacketFraming

📦 Be sure to check out the NuGet pacakge: https://www.nuget.org/packages/NTDLS.UDPPacketFraming

NTDLS.UDPPacketFraming is a set of classes and extensions methods that allow you to send/receive
UPD packets with ease. It handles corruption checks, concatenation, fragmentation, serialization
and adds compression.

## UPD Sever:
> Here we are instantiating a UdpMessageManager and giving it a listen port. This will cause the
> manager to go into listen mode and pass any received frames to the supplied callback.
```csharp
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
```

## UPD Client:
> Here we are instantiating a UdpMessageManager without a a listen port. This means that this this
> manager is in write-only mode. We are going to loop and send frames containing serialized MyFirstUDPPacket.
```csharp
static void Main()
{
    var udpManager = new UdpMessageManager();

    int packetNumber = 0;

    while (true)
    {
        udpManager.WriteMessage("127.0.0.1", 1234,
            new MyFirstUPDPacket($"Packet#:{packetNumber++} "));

        Thread.Sleep(100);
    }
}
```

## Supporting Code:
> The class that we are going to be serializing and deserializing in the examples.

```csharp
public class MyFirstUPDPacket: IUDPPayloadNotification
{
    public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
    public Guid UID { get; set; } = Guid.NewGuid();
    public string Message { get; set; } = string.Empty;

    public MyFirstUPDPacket()
    {
    }

    public MyFirstUPDPacket(string message)
    {
        Message = message;
    }
}
```
