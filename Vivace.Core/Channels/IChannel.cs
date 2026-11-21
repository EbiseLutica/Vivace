namespace Vivace.Core.Channels;

/// <summary>
/// 外のサービスへの送信を受け持つチャンネル。コアは実装を知らず、ホストが組み立てて渡す。
/// </summary>
public interface IChannel
{
    /// <summary>チャンネルの種類（"console" など）。ペルソナ定義の channels と、未送信の操作の宛先に使う。</summary>
    string Kind { get; }

    /// <summary>エンジンが動き出したあとに、チャンネルを起動する。長く待たずに返す。</summary>
    /// <param name="engine">エンジン。</param>
    /// <param name="ct">キャンセル。</param>
    Task StartAsync(VivaceEngine engine, CancellationToken ct);

    /// <summary>未送信の操作を1件送り、結果を返す。</summary>
    /// <param name="request">送る操作。</param>
    /// <param name="ct">キャンセル。</param>
    Task<SendResult> SendAsync(OutboundRequest request, CancellationToken ct);

    /// <summary>
    /// 圏外が明けたことを伝える。記録から起動するたびに1回呼ばれる。圏外だった区間を渡すので、取り直しに使える。既定では何もしない。
    /// </summary>
    /// <param name="offlineFrom">圏外になった時刻（記録の最後に適用した時刻）。</param>
    /// <param name="onlineAt">いまに着いた時刻（圏外が明けた時刻）。</param>
    /// <param name="ct">キャンセル。</param>
    Task OnlineAsync(DateTimeOffset offlineFrom, DateTimeOffset onlineAt, CancellationToken ct) => Task.CompletedTask;
}
