namespace Vivace.Core.Time;

/// <summary>
/// 実時間の時計。時刻は TimeProvider から取り、待ちも TimeProvider で行う。
/// </summary>
public sealed class SystemClock : IClock
{
    /// <summary>1回の待ちの上限。長い待ちはこの単位に分け、起きるたびに時刻を読み直す。壁時計が飛んでも、遅れは1回の待ちの上限まで。</summary>
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(1);

    /// <summary>時刻と待ちの元になる時計。</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>実時間の時計を作る。時刻と待ちの元は、指定の TimeProvider。省略（null）すると TimeProvider.System。テストでは差し替える。</summary>
    /// <param name="timeProvider">時刻と待ちの元になる時計。省略すると TimeProvider.System。</param>
    public SystemClock(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>いまの UTC の時刻。</summary>
    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    /// <summary>
    /// t まで実際に待つ。t がいまより前か同じなら、待たずに完了する。
    /// </summary>
    /// <param name="t">待つ先の時刻。</param>
    /// <param name="ct">キャンセル。</param>
    public async ValueTask WaitUntilAsync(DateTimeOffset t, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        while (true)
        {
            var remaining = t - _timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            await Task.Delay(remaining < MaxDelay ? remaining : MaxDelay, _timeProvider, ct);
        }
    }
}
