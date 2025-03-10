namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// Payload to send for keep-alive. This is discarded by the receive thread.
    /// </summary>
    public class DmKeepAliveMessage
        : IDmNotification
    {
    }
}
