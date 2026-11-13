using Vivace.Core.Domain;

namespace Vivace.Core.Events.Guards;

/// <summary>乱数の列の位置が、決めたときと同じであること。</summary>
/// <param name="Owner">持ち主（ペルソナのインスタンス ID）。</param>
/// <param name="Purpose">用途（"decide"、"sns"）。</param>
/// <param name="Position">決めたときの位置。</param>
public sealed record RngCursorCheck(string Owner, string Purpose, ulong Position) : GuardCheck;
