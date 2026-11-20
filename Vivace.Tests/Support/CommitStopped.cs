namespace Vivace.Tests.Support;

/// <summary>停止の模擬で、決めたコミットのあとに投げる例外。コミットは済んでいる。</summary>
internal sealed class CommitStopped : Exception
{
    /// <summary>停止の例外を作る。</summary>
    public CommitStopped()
        : base("決めたコミットのあとで停止しました。")
    {
    }
}
