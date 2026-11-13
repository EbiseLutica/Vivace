using Vivace.Core.Domain;

namespace Vivace.Core.Events.Payloads;

/// <summary>変換の辞書への記録。</summary>
/// <param name="Term">語。</param>
/// <param name="Verdict">判定。</param>
/// <param name="Mapped">変換先（変換のとき）。</param>
/// <param name="Category">構造に載るかの分類。</param>
public sealed record TermRecordedPayload(string Term, TermVerdict Verdict, string? Mapped, string? Category) : EventPayload(1);
