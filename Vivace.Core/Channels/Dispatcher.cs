namespace Vivace.Core.Channels;

/// <summary>
/// 配信役。未送信の操作を、種類の一致するチャンネルに渡し、送信の結果を返す。例外は失敗の結果にする。
/// </summary>
internal sealed class Dispatcher(IReadOnlyList<IChannel> channels)
{
    /// <summary>種類からチャンネルを引く表。種類の重複は、ビルダーで拒否してある。</summary>
    private readonly Dictionary<string, IChannel> _channels = channels.ToDictionary(channel => channel.Kind, StringComparer.Ordinal);

    /// <summary>操作を種類のチャンネルに送る。チャンネルがなければ、送っていない失敗にする。</summary>
    /// <param name="kind">チャンネルの種類。</param>
    /// <param name="request">送る操作。</param>
    /// <param name="ct">キャンセル。</param>
    public async Task<SendResult> SendAsync(string kind, OutboundRequest request, CancellationToken ct)
    {
        if (!_channels.TryGetValue(kind, out var channel))
        {
            return new SendResult.Failed($"チャンネル {kind} がない", SurelyNotSent: true, RetryAfter: null);
        }

        try
        {
            return await channel.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            return new SendResult.Failed(ex.Message, SurelyNotSent: false, RetryAfter: null);
        }
    }
}
