namespace Vivace.Core.Channels;

/// <summary>送信の結果。</summary>
public abstract record SendResult
{
    /// <summary>送れた。</summary>
    /// <param name="ExternalId">送信先での ID。</param>
    public sealed record Sent(string ExternalId) : SendResult;

    /// <summary>送れなかった。</summary>
    /// <param name="Reason">失敗の理由。</param>
    /// <param name="SurelyNotSent">送信されていないことが確かかどうか。</param>
    /// <param name="RetryAfter">次に投稿できる時刻（あれば）。</param>
    public sealed record Failed(string Reason, bool SurelyNotSent, DateTimeOffset? RetryAfter) : SendResult;
}
