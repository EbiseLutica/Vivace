namespace Vivace.Core.Time;

/// <summary>
/// 早送りの時計（ドライラン、追いつき）。待たずに時刻を進める。時刻は戻らない。
/// </summary>
public sealed class VirtualClock : IClock
{
    private readonly Lock _gate = new();
    private DateTimeOffset _now;

    /// <summary>仮想の時計を作る。</summary>
    /// <param name="start">開始の時刻。</param>
    public VirtualClock(DateTimeOffset start)
    {
        _now = start;
    }

    /// <summary>いまの仮想の時刻。</summary>
    public DateTimeOffset UtcNow
    {
        get
        {
            lock (_gate)
            {
                return _now;
            }
        }
    }

    /// <summary>
    /// 時刻を t まで進める。t がいまより前なら、何もしない。
    /// </summary>
    /// <param name="t">進める先の時刻。</param>
    public void AdvanceTo(DateTimeOffset t)
    {
        lock (_gate)
        {
            if (t > _now)
            {
                _now = t;
            }
        }
    }

    /// <summary>待たずに、時刻を t まで進めて返す。</summary>
    /// <param name="t">待つ先の時刻。</param>
    /// <param name="ct">キャンセル。</param>
    public ValueTask WaitUntilAsync(DateTimeOffset t, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        AdvanceTo(t);
        return ValueTask.CompletedTask;
    }
}
