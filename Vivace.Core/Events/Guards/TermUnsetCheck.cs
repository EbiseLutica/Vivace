namespace Vivace.Core.Events.Guards;

/// <summary>変換の辞書に、その語がまだないこと（または同じ変換であること）。</summary>
/// <param name="Term">語。</param>
public sealed record TermUnsetCheck(string Term) : GuardCheck;
