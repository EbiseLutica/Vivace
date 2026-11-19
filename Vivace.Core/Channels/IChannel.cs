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
}
