namespace Vivace.Core.Time;

/// <summary>
/// 中の時計を、動いている間に替えられる時計。追いつきで早送りの時計に替え、いまに着いたら元の時計に戻すために使う。
/// </summary>
internal sealed class SwitchableClock : IClock
{
    /// <summary>使っている時計を守るロック。</summary>
    private readonly Lock _gate = new();

    /// <summary>いま使っている時計。</summary>
    private IClock _current;

    /// <summary>最初に使う時計で作る。</summary>
    /// <param name="initial">最初に使う時計。</param>
    public SwitchableClock(IClock initial)
    {
        _current = initial;
    }

    /// <summary>いま使っている時計。</summary>
    public IClock Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow => Current.UtcNow;

    /// <summary>使う時計を替える。以後の時刻と待ちは、この時計で行う。</summary>
    /// <param name="clock">新しく使う時計。</param>
    public void Replace(IClock clock)
    {
        lock (_gate)
        {
            _current = clock;
        }
    }

    /// <inheritdoc />
    public ValueTask WaitUntilAsync(DateTimeOffset t, CancellationToken ct) => Current.WaitUntilAsync(t, ct);
}
