namespace Vivace.Core.Time;

/// <summary>
/// 開始の時刻から、実時間の経過の speed 倍の速さで進む時計。眺める時間を縮めるために使う。時刻は戻らない。
/// </summary>
public sealed class ScaledClock : IClock
{
    /// <summary>1回の待ちの上限。長い待ちはこの単位に分け、起きるたびに時刻を読み直す。壁時計が飛んでも、遅れは1回の待ちの上限まで。</summary>
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(1);

    /// <summary>実時間の経過を読む時計。</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>開始の時刻（UTC）。</summary>
    private readonly DateTimeOffset _start;

    /// <summary>実時間の経過に掛ける倍率。</summary>
    private readonly double _speed;

    /// <summary>作った時点の実時間の目盛り。経過はここからの差で測る。</summary>
    private readonly long _baseTimestamp;

    /// <summary>開始の時刻と倍率から作る。作った時点を、実時間の基点にする。</summary>
    /// <param name="start">開始の時刻。</param>
    /// <param name="speed">倍率。0 より大きい有限の値。</param>
    /// <param name="timeProvider">実時間の元になる時計。省略すると TimeProvider.System。</param>
    /// <exception cref="ArgumentOutOfRangeException">倍率が 0 以下、NaN、無限大のとき。</exception>
    public ScaledClock(DateTimeOffset start, double speed, TimeProvider? timeProvider = null)
    {
        if (!double.IsFinite(speed) || speed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "倍率は 0 より大きい有限の値にする。");
        }

        _timeProvider = timeProvider ?? TimeProvider.System;
        _start = start.ToUniversalTime();
        _speed = speed;
        _baseTimestamp = _timeProvider.GetTimestamp();
    }

    /// <summary>
    /// いまの時刻。開始の時刻に、実時間の経過の倍率を掛けた分を足す。表せる最後の時刻で頭打ちにする。
    /// </summary>
    public DateTimeOffset UtcNow
    {
        get
        {
            var elapsed = _timeProvider.GetElapsedTime(_baseTimestamp);
            var scaled = elapsed.Ticks * _speed;
            var limit = Math.BitDecrement((double)(DateTimeOffset.MaxValue - _start).Ticks);
            if (scaled >= limit)
            {
                return DateTimeOffset.MaxValue;
            }

            return _start + TimeSpan.FromTicks((long)scaled);
        }
    }

    /// <summary>
    /// t まで実時間で待つ。待つ長さは、時刻の差を倍率で割った分。t がいまと同じか前なら、待たずに完了する。
    /// </summary>
    /// <param name="t">待つ先の時刻。</param>
    /// <param name="ct">キャンセル。</param>
    public async ValueTask WaitUntilAsync(DateTimeOffset t, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        while (true)
        {
            var now = UtcNow;
            if (t <= now)
            {
                return;
            }

            await Task.Delay(ToRealDelay(t - now), _timeProvider, ct);
        }
    }

    /// <summary>仮想の時刻の差を、実時間の待ちに直す。切り上げて、最低 1 ミリ秒にする（0 の待ちで空回りしないように）。1回の上限を超えるときは上限にする。</summary>
    /// <param name="scaled">仮想の時刻の差。0 より大きい。</param>
    private TimeSpan ToRealDelay(TimeSpan scaled)
    {
        var real = Math.Ceiling(scaled.Ticks / _speed);
        return real < MaxDelay.Ticks ? TimeSpan.FromTicks((long)Math.Max(real, TimeSpan.TicksPerMillisecond)) : MaxDelay;
    }
}
