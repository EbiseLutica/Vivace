namespace Vivace.Core.Events;

/// <summary>乱数を引いた範囲。どの列の、どこからどこまで引いたかを残す（D5 5.9）。</summary>
/// <param name="Owner">持ち主。</param>
/// <param name="Purpose">用途。</param>
/// <param name="From">引き始めた位置。</param>
/// <param name="To">引き終わった後の位置。</param>
public sealed record RngDraws(string Owner, string Purpose, ulong From, ulong To);
