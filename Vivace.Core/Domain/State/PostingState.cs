using System.Collections.Immutable;

namespace Vivace.Core.Domain.State;

/// <summary>投稿の状態。</summary>
/// <param name="LastPostAt">最後に投稿した時刻。</param>
/// <param name="LastSkippedAt">最後にスキップした時刻。</param>
/// <param name="RemoteNotBefore">サーバーの制限で、次に投稿できる時刻。</param>
/// <param name="Recent">直近の投稿。古いものが前。件数は RecentLimit までに保つ。</param>
public sealed record PostingState(DateTimeOffset? LastPostAt, DateTimeOffset? LastSkippedAt, DateTimeOffset? RemoteNotBefore, ImmutableList<RecentPost> Recent)
{
    /// <summary>直近の投稿として残す件数。</summary>
    public const int RecentLimit = 5;

    /// <summary>投稿を記録する。最後の投稿の時刻を更新し、直近の投稿に足して、古いものを外す。</summary>
    /// <param name="at">投稿の時刻。</param>
    /// <param name="body">本文。</param>
    public PostingState Posted(DateTimeOffset at, string body)
    {
        var recent = Recent.Add(new RecentPost(at, body));
        if (recent.Count > RecentLimit)
        {
            recent = recent.RemoveRange(0, recent.Count - RecentLimit);
        }

        return this with { LastPostAt = at, Recent = recent };
    }

    /// <summary>スキップを記録する。最後のスキップの時刻を更新する。</summary>
    /// <param name="at">スキップの時刻。</param>
    public PostingState Skipped(DateTimeOffset at) => this with { LastSkippedAt = at };
}
