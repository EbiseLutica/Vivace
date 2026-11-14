namespace Vivace.Core.Domain.State;

/// <summary>投稿の状態。</summary>
/// <param name="LastPostAt">最後に投稿した時刻。</param>
/// <param name="LastSkippedAt">最後にスキップした時刻。</param>
/// <param name="RemoteNotBefore">サーバーの制限で、次に投稿できる時刻。</param>
public sealed record PostingState(DateTimeOffset? LastPostAt, DateTimeOffset? LastSkippedAt, DateTimeOffset? RemoteNotBefore);
