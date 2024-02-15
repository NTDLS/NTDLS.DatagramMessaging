namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// All simple notifications frames must in herit from this interface and be json serializable.
    /// </summary>
    public interface IDmNotification : IDmPayload
    {
    }
}
