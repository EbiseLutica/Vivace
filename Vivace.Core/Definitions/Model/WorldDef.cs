using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Time;

namespace Vivace.Core.Definitions.Model;

/// <summary>ワールドの定義。</summary>
/// <param name="Id">定義 ID。</param>
/// <param name="Name">名前。</param>
/// <param name="Lore">世界観の説明。</param>
/// <param name="UtcOffset">時差。</param>
/// <param name="GeneratedPlaces">生成する場所の扱い。</param>
/// <param name="NotExist">存在しないものの一覧（任意）。</param>
/// <param name="HolidayCalendar">祝日のカレンダーの提供元。なければ祝日なし。</param>
/// <param name="Events">年中行事（月日 → 名前）。</param>
/// <param name="Terms">変換の辞書の初期値。</param>
/// <param name="Places">定義の場所。</param>
/// <param name="Source">出どころ。</param>
public sealed record WorldDef(
    WorldDefId Id,
    string Name,
    string Lore,
    TimeSpan UtcOffset,
    GeneratedPlacesMode GeneratedPlaces,
    string? NotExist,
    string? HolidayCalendar,
    ImmutableDictionary<MonthDay, string> Events,
    ImmutableArray<TermDef> Terms,
    ImmutableArray<PlaceDef> Places,
    DefinitionSource Source);
