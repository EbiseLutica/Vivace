using Vivace.Core.Time;

namespace Vivace.Tests.Support;

/// <summary>仮想時計を包み、決めた時刻への待ちだけを、解放されるまで止める時計。</summary>
internal sealed class GatedClock : IClock
{
    /// <summary>中の仮想時計。</summary>
    private readonly VirtualClock _inner;

    /// <summary>止める待ちの先の時刻。</summary>
    private readonly DateTimeOffset _gate;

    /// <summary>開始の時刻と、止める待ちの先の時刻から作る。</summary>
    /// <param name="start">開始の時刻。</param>
    /// <param name="gate">この時刻への待ちを止める。</param>
    public GatedClock(DateTimeOffset start, DateTimeOffset gate)
    {
        _inner = new VirtualClock(start);
        _gate = gate;
    }

    /// <summary>止める時刻への待ちに入ったら完了する。</summary>
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>止めた待ちを先へ進める合図。</summary>
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>いまの仮想の時刻。</summary>
    public DateTimeOffset UtcNow => _inner.UtcNow;

    /// <summary>止める時刻への待ちなら、解放されるまで待ってから進める。ほかは待たずに進める。</summary>
    public async ValueTask WaitUntilAsync(DateTimeOffset t, CancellationToken ct)
    {
        if (t == _gate)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(ct);
        }

        await _inner.WaitUntilAsync(t, ct);
    }
}
