namespace Vivace.Core.Channels;

/// <summary>
/// 配信役。未送信の操作を、種類の一致するチャンネルに渡し、送信の結果を返す。例外と時間切れは失敗の結果にする。
/// </summary>
/// <param name="channels">つないだチャンネル。</param>
/// <param name="sendTimeout">送信の時間切れ（実時間）。</param>
internal sealed class Dispatcher(IReadOnlyList<IChannel> channels, TimeSpan sendTimeout)
{
    /// <summary>送信の既定の時間切れ（実時間）。</summary>
    public static readonly TimeSpan DefaultSendTimeout = TimeSpan.FromSeconds(30);

    /// <summary>種類からチャンネルを引く表。種類の重複は、ビルダーで拒否してある。</summary>
    private readonly Dictionary<string, IChannel> _channels = channels.ToDictionary(channel => channel.Kind, StringComparer.Ordinal);

    /// <summary>つないだチャンネルの一覧。追加した順。圏外明けの通知に使う。</summary>
    private readonly IReadOnlyList<IChannel> _all = channels;

    /// <summary>圏外が明けたことを、つないだチャンネルに追加した順に伝える。チャンネルの例外は、そのまま投げる。</summary>
    /// <param name="ct">エンジンを止めるキャンセル。</param>
    public async Task NotifyOnlineAsync(CancellationToken ct)
    {
        foreach (var channel in _all)
        {
            await channel.OnlineAsync(ct);
        }
    }

    /// <summary>操作を種類のチャンネルに送る。チャンネルがなければ、送っていない失敗にする。時間切れは、チャンネルが返らなくても失敗として返す。</summary>
    /// <param name="kind">チャンネルの種類。</param>
    /// <param name="request">送る操作。</param>
    /// <param name="ct">エンジンを止めるキャンセル。</param>
    public async Task<SendResult> SendAsync(string kind, OutboundRequest request, CancellationToken ct)
    {
        if (!_channels.TryGetValue(kind, out var channel))
        {
            return new SendResult.Failed($"チャンネル {kind} がない", SurelyNotSent: true, RetryAfter: null);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(sendTimeout);
        try
        {
            var result = await channel.SendAsync(request, timeout.Token).WaitAsync(timeout.Token);
            return result ?? new SendResult.Failed("チャンネルが結果を返さなかった", SurelyNotSent: false, RetryAfter: null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new SendResult.Failed($"時間切れ（{sendTimeout.TotalSeconds:0} 秒）", SurelyNotSent: false, RetryAfter: null);
        }
        catch (OperationCanceledException)
        {
            return new SendResult.Failed("エンジンが止まった", SurelyNotSent: false, RetryAfter: null);
        }
        catch (Exception ex)
        {
            return new SendResult.Failed(ex.Message, SurelyNotSent: false, RetryAfter: null);
        }
    }
}
