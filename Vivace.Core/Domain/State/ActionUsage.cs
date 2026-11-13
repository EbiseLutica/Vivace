namespace Vivace.Core.Domain.State;

/// <summary>行動ごとの利用の記録（制限の判定に使う）。</summary>
/// <param name="LastStartedAt">前回の開始の時刻。</param>
/// <param name="CountDate">今日の回数を数えている現地の日付。</param>
/// <param name="CountToday">今日の回数。</param>
/// <param name="Consecutive">直前から連続して同じ行動を続けた回数（繰り返しの減衰に使う）。</param>
public sealed record ActionUsage(DateTimeOffset? LastStartedAt, DateOnly CountDate, int CountToday, int Consecutive);
