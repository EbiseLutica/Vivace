using System.Collections.Immutable;
using Vivace.Core.Domain;

namespace Vivace.Core.Definitions;

/// <summary>行動の定義（D3 3.6）。</summary>
/// <param name="Id">定義 ID（組み込みは core.*）。</param>
/// <param name="Display">表示名。</param>
/// <param name="Description">説明。</param>
/// <param name="Tags">行動タグ。</param>
/// <param name="Duration">所要時間。固定予定のブロックの行動では省略してよい。</param>
/// <param name="Weight">基本の重み。</param>
/// <param name="Phone">行動中にスマホを開けるか。</param>
/// <param name="ScheduleOnly">固定予定と反射からだけ始まるか（D2 2.5）。</param>
/// <param name="Requires">実行の条件。</param>
/// <param name="Limits">制限。</param>
/// <param name="Effects">効果。</param>
/// <param name="Source">出どころ。</param>
public sealed record ActionDef(
    string Id,
    string Display,
    string Description,
    ImmutableArray<string> Tags,
    ActionDuration? Duration,
    double Weight,
    PhonePolicy Phone,
    bool ScheduleOnly,
    ActionRequires Requires,
    ActionLimits Limits,
    ActionEffects Effects,
    DefinitionSource Source);
