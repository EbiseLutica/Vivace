
namespace Vivace.Core.Definitions;

/// <summary>問題1件。位置つき。</summary>
/// <param name="Severity">重さ。</param>
/// <param name="Code">種類。</param>
/// <param name="Location">位置。</param>
/// <param name="Message">説明（日本語）。</param>
public sealed record DefinitionIssue(IssueSeverity Severity, DefinitionIssueCode Code, SourceLocation Location, string Message)
{
    /// <summary>"位置: 重さ 種類: 説明" の形にする。</summary>
    public override string ToString() => $"{Location}: {Severity} {Code}: {Message}";
}
