using System.Collections.Immutable;

namespace Vivace.Core.Definitions;

/// <summary>
/// 読み込んだ定義の全体（不変）。定義は TOML が正で、メモリの上では不変の型にする（D3 3.1）。
/// </summary>
/// <param name="Worlds">ワールドの定義。キーは定義 ID。</param>
/// <param name="Personas">ペルソナの定義。キーは定義 ID。</param>
/// <param name="Actions">行動の定義。キーは定義 ID（組み込みは core.*）。</param>
/// <param name="Incidents">出来事の定義。キーは定義 ID。</param>
/// <param name="Conditions">状態の定義。キーは定義 ID。</param>
/// <param name="Hash">定義全体のハッシュ（読み直しの記録に使う）。</param>
public sealed record Definitions(
    ImmutableDictionary<string, WorldDef> Worlds,
    ImmutableDictionary<string, PersonaDef> Personas,
    ImmutableDictionary<string, ActionDef> Actions,
    ImmutableDictionary<string, IncidentDef> Incidents,
    ImmutableDictionary<string, ConditionDef> Conditions,
    string Hash);
