namespace Vivace.Core.Time;

/// <summary>時刻と待ちのインターフェース。</summary>
public interface IClock
{
    /// <summary>いまの UTC の時刻。</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// t まで待つ。早送りの時計は、待たずに UtcNow を t まで進めて返す。
    /// </summary>
    /// <param name="t">待つ先の時刻。</param>
    /// <param name="ct">キャンセル。</param>
    ValueTask WaitUntilAsync(DateTimeOffset t, CancellationToken ct);
}
