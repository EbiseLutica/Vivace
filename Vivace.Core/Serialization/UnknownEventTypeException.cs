namespace Vivace.Core.Serialization;

/// <summary>知らないイベントタイプのペイロードを読もうとした。新しいバージョンで書かれた記録を、古いコードで読んだことを示す。</summary>
public sealed class UnknownEventTypeException : Exception
{
    /// <summary>知らないイベントタイプの名前で例外を作る。</summary>
    /// <param name="eventType">イベントタイプの名前。</param>
    public UnknownEventTypeException(string eventType)
        : base($"知らないイベントタイプです: {eventType}")
    {
        EventType = eventType;
    }

    /// <summary>知らないイベントタイプの名前。</summary>
    public string EventType { get; }
}
