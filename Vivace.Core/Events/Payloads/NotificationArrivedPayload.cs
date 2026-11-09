using Vivace.Core.Domain;

namespace Vivace.Core.Events;

/// <summary>外からの通知の到着。受信箱に積む。</summary>
/// <param name="Item">受信箱のアイテム。Seq は適用のときに入る。</param>
public sealed record NotificationArrivedPayload(InboxItem Item) : EventPayload(1);
