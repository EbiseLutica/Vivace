using Vivace.Core.Decision;

namespace Vivace.Tests.Support;

/// <summary>最初の決定で、解放されるまで止まる Decider。止まっている間、エンジンは出力を待つ。</summary>
internal sealed class BlockingDecider : IDecider
{
    /// <summary>止めたあとに使う決定。</summary>
    private readonly UtilityDecider _inner = new();

    /// <summary>決定に入ったら完了する。</summary>
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>決定を先へ進める合図。</summary>
    public ManualResetEventSlim Release { get; } = new();

    /// <summary>入ったことを知らせ、解放されるまで待ってから、効用で決める。</summary>
    public DeciderOutput Decide(DecisionContext context)
    {
        Entered.TrySetResult();
        Release.Wait();
        return _inner.Decide(context);
    }
}
