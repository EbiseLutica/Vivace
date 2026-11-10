using System.Collections.Immutable;

namespace Vivace.Core.Definitions;

/// <summary>読み込みの結果。エラーがなければ Definitions がある。</summary>
/// <param name="Definitions">定義。エラーがあれば null。</param>
/// <param name="Issues">エラーと警告の全件。</param>
public sealed record DefinitionLoadResult(Definitions? Definitions, ImmutableArray<DefinitionIssue> Issues)
{
    /// <summary>エラーがあるかどうか。</summary>
    public bool HasErrors => Issues.Any(issue => issue.Severity == IssueSeverity.Error);
}
