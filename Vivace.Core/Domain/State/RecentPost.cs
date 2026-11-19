namespace Vivace.Core.Domain.State;

/// <summary>直近の投稿の1件。文面を作るときに、同じことを繰り返さないために読む。</summary>
/// <param name="At">投稿の時刻。</param>
/// <param name="Body">本文。</param>
public sealed record RecentPost(DateTimeOffset At, string Body);
