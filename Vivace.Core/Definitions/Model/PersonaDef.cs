using System.Collections.Immutable;
using System.Text.Json;
using Vivace.Core.Domain.State;
using Vivace.Core.Domain;

namespace Vivace.Core.Definitions.Model;

/// <summary>ペルソナの定義。</summary>
/// <param name="Id">定義 ID。</param>
/// <param name="Name">名前。</param>
/// <param name="World">居住するワールドの定義 ID。</param>
/// <param name="Home">早退して帰る場所。</param>
/// <param name="Lore">人物の説明。</param>
/// <param name="Examples">発言例。</param>
/// <param name="Channels">つながるチャンネル。</param>
/// <param name="Items">携行品のタグ。</param>
/// <param name="Tools">使えるツール。</param>
/// <param name="Use">使える行動（ワイルドカード "misskey.*" を含む）。</param>
/// <param name="Prefs">行動の好み。キーは行動の定義 ID か "tag:行動タグ"。</param>
/// <param name="Vitals">状態変数の設定。</param>
/// <param name="Reflexes">反射のしきい値と行動。</param>
/// <param name="Disruption">予定の乱れへの反応の重み。</param>
/// <param name="MemoryHalfLife">記憶の半減期の倍率。</param>
/// <param name="FollowBack">フォローバックの方針。</param>
/// <param name="ChannelSettings">チャンネルごとの設定。中身はプラグインが読む。</param>
/// <param name="Schedule">固定予定。</param>
/// <param name="Source">出どころ。</param>
public sealed record PersonaDef(
    PersonaDefId Id,
    string Name,
    WorldDefId World,
    string Home,
    string Lore,
    ImmutableArray<string> Examples,
    ImmutableArray<string> Channels,
    ImmutableArray<string> Items,
    ImmutableArray<string> Tools,
    ImmutableArray<string> Use,
    ImmutableDictionary<string, Preference> Prefs,
    VitalsDef Vitals,
    ImmutableDictionary<VitalName, ReflexDef> Reflexes,
    DisruptionWeights Disruption,
    double MemoryHalfLife,
    FollowBackPolicy FollowBack,
    ImmutableDictionary<string, JsonElement> ChannelSettings,
    ScheduleDef Schedule,
    DefinitionSource Source);
