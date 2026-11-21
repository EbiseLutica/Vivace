namespace Vivace.Core.Signals;

/// <summary>
/// 圏外が明けたことを、つないだチャンネルに伝える。圏外だった区間は、記録の最後に適用した時刻（offlineFrom）から、いまに着いた時刻（onlineAt）まで。
/// </summary>
/// <param name="OfflineFrom">圏外になった時刻（記録の最後に適用した時刻）。</param>
/// <param name="OnlineAt">いまに着いた時刻（圏外が明けた時刻）。</param>
public sealed record ChannelOnlineSignal(DateTimeOffset OfflineFrom, DateTimeOffset OnlineAt) : EngineSignal;
