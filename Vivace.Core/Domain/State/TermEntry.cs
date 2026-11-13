namespace Vivace.Core.Domain.State;

/// <summary>変換の辞書の1件。</summary>
/// <param name="Term">語。</param>
/// <param name="Verdict">判定。</param>
/// <param name="Mapped">変換先（変換のとき）。</param>
/// <param name="Category">構造に載るかの分類。</param>
/// <param name="CreatedSeq">記録したイベントの連番。</param>
public sealed record TermEntry(string Term, TermVerdict Verdict, string? Mapped, string? Category, long CreatedSeq);
