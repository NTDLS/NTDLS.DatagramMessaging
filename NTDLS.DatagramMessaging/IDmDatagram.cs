namespace NTDLS.DatagramMessaging
{
    /// <summary>
    /// All simple notifications frames must in inherit from this interface and be json serializable.
    /// </summary>
    public interface IDmDatagram : IDmPayload
    {
    }
}
