using System.Collections.Immutable;
using Vivace.Core.Domain;

namespace Vivace.Core.Definitions.Model;

/// <summary>読み込んだ定義の全体。</summary>
/// <param name="Worlds">ワールドの定義。キーはワールドの定義 ID。</param>
/// <param name="Personas">ペルソナの定義。キーはペルソナの定義 ID。</param>
/// <param name="Actions">行動の定義。キーは定義 ID（組み込みは core.*）。</param>
/// <param name="Incidents">出来事の定義。キーは定義 ID。</param>
/// <param name="Conditions">状態の定義。キーは定義 ID。</param>
/// <param name="Hash">定義全体のハッシュ（読み直しの記録に使う）。</param>
public sealed record DefinitionPack(
    ImmutableDictionary<WorldDefId, WorldDef> Worlds,
    ImmutableDictionary<PersonaDefId, PersonaDef> Personas,
    ImmutableDictionary<string, ActionDef> Actions,
    ImmutableDictionary<string, IncidentDef> Incidents,
    ImmutableDictionary<string, ConditionDef> Conditions,
    string Hash);
