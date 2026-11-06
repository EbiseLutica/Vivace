using System.Collections.Immutable;
using System.Text.Json;
using Vivace.Core.Domain;
using Vivace.Core.Time;

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

/// <summary>生成する場所の扱い（D3 3.3）。</summary>
public enum GeneratedPlacesMode
{
    /// <summary>自動で採用する。</summary>
    Auto,

    /// <summary>候補にして、管理者が採用する。</summary>
    Review,
}

/// <summary>ワールドの定義（D3 3.3）。</summary>
/// <param name="Id">定義 ID。</param>
/// <param name="Name">名前。</param>
/// <param name="Lore">世界観の説明。</param>
/// <param name="UtcOffset">時差。</param>
/// <param name="GeneratedPlaces">生成する場所の扱い。</param>
/// <param name="NotExist">存在しないものの一覧（任意）。</param>
/// <param name="HolidayCalendar">祝日の暦の提供元（P-18）。なければ祝日なし。</param>
/// <param name="Events">年中行事（月日 → 名前）。</param>
/// <param name="Terms">変換の辞書の初期値。</param>
/// <param name="Places">定義の場所。</param>
/// <param name="Source">出どころ。</param>
public sealed record WorldDef(
    string Id,
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

/// <summary>変換の辞書の初期値の1件。</summary>
/// <param name="From">語。</param>
/// <param name="Verdict">判定。</param>
/// <param name="To">変換先（convert のとき）。</param>
/// <param name="Source">出どころ。</param>
public sealed record TermDef(string From, TermVerdict Verdict, string? To, DefinitionSource Source);

/// <summary>定義の場所。</summary>
/// <param name="Id">場所の ID。</param>
/// <param name="Name">表示名。</param>
/// <param name="Tags">タグ。</param>
/// <param name="Group">所属のまとまり（家など）。</param>
/// <param name="Links">つながる場所。</param>
/// <param name="Source">出どころ。</param>
public sealed record PlaceDef(
    string Id,
    string Name,
    ImmutableArray<string> Tags,
    string? Group,
    ImmutableArray<LinkDef> Links,
    DefinitionSource Source);

/// <summary>場所のつながり（片方に書けば逆向きにも張る）。</summary>
/// <param name="To">つながる場所の ID。</param>
/// <param name="Minutes">移動時間（分）。</param>
/// <param name="Source">位置。</param>
public sealed record LinkDef(string To, int Minutes, SourceLocation Source);

/// <summary>ペルソナの定義（D3 3.4）。</summary>
/// <param name="Id">定義 ID。</param>
/// <param name="Name">名前。</param>
/// <param name="World">居住するワールドの定義 ID。</param>
/// <param name="Home">早退して帰る場所（P-19）。</param>
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
/// <param name="MemoryHalfLife">記憶の半減期の倍率（P-47）。</param>
/// <param name="FollowBack">フォローバックの方針（P-44）。</param>
/// <param name="ChannelSettings">チャンネルごとの設定。中身はプラグインが読む。</param>
/// <param name="Schedule">固定予定。</param>
/// <param name="Source">出どころ。</param>
public sealed record PersonaDef(
    string Id,
    string Name,
    string World,
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

/// <summary>行動の好みの種類。</summary>
public enum PreferenceKind
{
    /// <summary>好き（"like"）。</summary>
    Like,

    /// <summary>嫌い（"dislike"）。</summary>
    Dislike,

    /// <summary>しない（"forbid"。数値の 0 もこれ）。</summary>
    Forbid,

    /// <summary>数値の倍率。</summary>
    Factor,
}

/// <summary>行動の好み1件。</summary>
/// <param name="Kind">種類。</param>
/// <param name="Factor">倍率（Factor のとき）。</param>
public sealed record Preference(PreferenceKind Kind, double Factor);

/// <summary>状態変数の設定。省略したものは既定値（D5 5.6）。</summary>
/// <param name="EnergyInitial">気力の初期値。</param>
/// <param name="SocialFloor">社交欲求の下限。</param>
/// <param name="SocialCeiling">社交欲求の上限。</param>
/// <param name="SocialRateAlone">一人のときの社交欲求の率（1時間あたり）。</param>
/// <param name="SocialRateTogether">同席のときの社交欲求の率（1時間あたり）。</param>
/// <param name="SleepTarget">睡眠の目標の長さ。</param>
public sealed record VitalsDef(
    double EnergyInitial,
    double SocialFloor,
    double SocialCeiling,
    double SocialRateAlone,
    double SocialRateTogether,
    TimeSpan SleepTarget);

/// <summary>反射のしきい値と行動（D5 5.7）。</summary>
/// <param name="Threshold">しきい値。越えた瞬間に反射する。</param>
/// <param name="ActionId">反射で始める行動の定義 ID。</param>
/// <param name="Source">位置。</param>
public sealed record ReflexDef(double Threshold, string ActionId, SourceLocation Source);

/// <summary>予定の乱れへの反応の重み（D5 5.4、P-31）。</summary>
/// <param name="Late">遅刻の重み。</param>
/// <param name="Absent">欠席の重み。</param>
/// <param name="Wait">待つの重み。</param>
public sealed record DisruptionWeights(double Late, double Absent, double Wait);

/// <summary>フォローバックの方針（P-44）。</summary>
public enum FollowBackPolicy
{
    /// <summary>フォローされたら必ずフォローし返す。</summary>
    Always,

    /// <summary>フォローし返さない。</summary>
    Never,
}

/// <summary>固定予定（D3 3.5）。</summary>
/// <param name="Periods">期間。</param>
/// <param name="Blocks">ブロック。</param>
/// <param name="Exceptions">例外日。</param>
public sealed record ScheduleDef(
    ImmutableArray<PeriodDef> Periods,
    ImmutableArray<BlockDef> Blocks,
    ImmutableArray<ExceptionDef> Exceptions);

/// <summary>期間（毎年繰り返す）。</summary>
/// <param name="Id">期間の ID。</param>
/// <param name="From">開始の月日。</param>
/// <param name="To">終了の月日。From より小さければ年をまたぐ。</param>
/// <param name="Source">出どころ。</param>
public sealed record PeriodDef(string Id, MonthDay From, MonthDay To, DefinitionSource Source);

/// <summary>現地の時刻の範囲。End が Start より小さければ日をまたぐ。</summary>
/// <param name="Start">開始。</param>
/// <param name="End">終了。</param>
public readonly record struct TimeRange(TimeOnly Start, TimeOnly End);

/// <summary>固定予定のブロック（D3 3.5）。</summary>
/// <param name="Id">ブロックの定義 ID。</param>
/// <param name="Within">親のブロックの ID（入れ子のとき）。</param>
/// <param name="In">所属する期間の ID。空なら、すべての期間で使う。</param>
/// <param name="Days">曜日。空なら、毎日。</param>
/// <param name="Ranges">時間の範囲（from と to、または times）。</param>
/// <param name="Action">行動の定義 ID。親から引き継ぐことがある。</param>
/// <param name="Place">場所の ID。</param>
/// <param name="SkipHoliday">祝日に休むか。</param>
/// <param name="Phone">スマホを開けるか。</param>
/// <param name="With">同行するペルソナの定義 ID。</param>
/// <param name="OnPartnerAbsent">同行者が抜けたときの扱い。</param>
/// <param name="PhoneEvery">長いブロックの途中で、スマホを開く機会を出す間隔（P-32）。書いたときだけ。</param>
/// <param name="Source">出どころ。</param>
public sealed record BlockDef(
    string Id,
    string? Within,
    ImmutableArray<string> In,
    ImmutableArray<DayOfWeek> Days,
    ImmutableArray<TimeRange> Ranges,
    string? Action,
    string? Place,
    bool? SkipHoliday,
    PhonePolicy? Phone,
    ImmutableArray<string> With,
    PartnerAbsentPolicy? OnPartnerAbsent,
    TimeSpan? PhoneEvery,
    DefinitionSource Source);

/// <summary>例外日（D3 3.5）。</summary>
/// <param name="Date">日付。</param>
/// <param name="Skip">この日は外すブロックの定義 ID。</param>
/// <param name="Blocks">この日だけ足すブロック。</param>
/// <param name="Source">出どころ。</param>
public sealed record ExceptionDef(DateOnly Date, ImmutableArray<string> Skip, ImmutableArray<BlockDef> Blocks, DefinitionSource Source);

/// <summary>行動の所要時間。範囲か固定。</summary>
/// <param name="Min">最短。</param>
/// <param name="Max">最長。固定のときは Min と同じ。</param>
/// <param name="IsRange">範囲で書いたかどうか。</param>
public sealed record ActionDuration(TimeSpan Min, TimeSpan Max, bool IsRange);

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

/// <summary>行動の実行の条件（D2 2.6）。</summary>
/// <param name="PlaceTags">場所が持つべきタグ。</param>
/// <param name="Items">ペルソナが持つべき携行品のタグ。</param>
/// <param name="ChannelCapability">つながるべきチャンネルの能力。</param>
public sealed record ActionRequires(ImmutableArray<string> PlaceTags, ImmutableArray<string> Items, string? ChannelCapability);

/// <summary>行動の制限（D2 2.6）。</summary>
/// <param name="Cooldown">前回の開始から、この時間が経つまで候補にしない。</param>
/// <param name="MaxPerDay">現地の1日に、この回数まで。</param>
/// <param name="Hours">現地の時刻がこの範囲にあるときだけ。</param>
public sealed record ActionLimits(TimeSpan? Cooldown, int? MaxPerDay, TimeRange? Hours);

/// <summary>睡眠の効果（D2 2.5）。</summary>
/// <param name="Consolidate">記憶の整理を起こすか。</param>
/// <param name="Debt">寝不足の判定をするか。</param>
public sealed record SleepEffect(bool Consolidate, bool Debt);

/// <summary>行動の効果（D2 2.5）。</summary>
/// <param name="Rates">行動の間の率（置き換え）。</param>
/// <param name="OnStart">開始時の増減。</param>
/// <param name="OnEnd">終了時の増減。</param>
/// <param name="Restore">終了時の設定値。</param>
/// <param name="RepeatDecay">同じ行動が続いたときの退屈度の減り方。</param>
/// <param name="Sleep">睡眠としての扱い。</param>
/// <param name="Handler">起こすハンドラ（"sns.inbox" など）。</param>
public sealed record ActionEffects(
    ImmutableDictionary<VitalName, double> Rates,
    ImmutableDictionary<VitalName, double> OnStart,
    ImmutableDictionary<VitalName, double> OnEnd,
    ImmutableDictionary<VitalName, double> Restore,
    double? RepeatDecay,
    SleepEffect? Sleep,
    string? Handler);

/// <summary>出来事の対象。</summary>
public enum IncidentTarget
{
    /// <summary>ワールド向け。</summary>
    World,

    /// <summary>ペルソナ向け。</summary>
    Persona,
}

/// <summary>出来事の定義（D3 3.6）。</summary>
/// <param name="Id">定義 ID。</param>
/// <param name="Display">表示名。</param>
/// <param name="Target">対象。</param>
/// <param name="Chance">1日あたりの発生確率（0 から 1）。定義ごとに必ず書く（P-37）。</param>
/// <param name="Hours">発生する時刻の範囲。</param>
/// <param name="Duration">持続時間。</param>
/// <param name="Cooldown">再発までの間隔。</param>
/// <param name="Effects">効果。</param>
/// <param name="Source">出どころ。</param>
public sealed record IncidentDef(
    string Id,
    string Display,
    IncidentTarget Target,
    double Chance,
    TimeRange? Hours,
    ActionDuration? Duration,
    TimeSpan? Cooldown,
    IncidentEffects Effects,
    DefinitionSource Source);

/// <summary>出来事の効果（D2 2.7）。書いたものだけが入る。</summary>
/// <param name="SlowPlace">辺の移動時間を増やす。</param>
/// <param name="ClosePlace">場所を閉鎖する。</param>
/// <param name="Vitals">状態変数の増減。</param>
/// <param name="Condition">付ける状態の定義 ID。</param>
public sealed record IncidentEffects(
    SlowPlaceEffect? SlowPlace,
    ClosePlaceEffect? ClosePlace,
    ImmutableDictionary<VitalName, double> Vitals,
    string? Condition);

/// <summary>移動時間を増やす効果。場所 ID か場所のタグで決める。</summary>
/// <param name="Place">場所の ID。</param>
/// <param name="PlaceTag">場所のタグ。</param>
/// <param name="Add">増やす時間。</param>
/// <param name="Factor">掛ける倍率。</param>
public sealed record SlowPlaceEffect(string? Place, string? PlaceTag, TimeSpan? Add, double? Factor);

/// <summary>場所を閉鎖する効果。場所 ID か場所のタグで決める。</summary>
/// <param name="Place">場所の ID。</param>
/// <param name="PlaceTag">場所のタグ。</param>
public sealed record ClosePlaceEffect(string? Place, string? PlaceTag);

/// <summary>状態（コンディション）の定義（D2 2.7）。</summary>
/// <param name="Id">定義 ID。</param>
/// <param name="Display">表示名。</param>
/// <param name="Rates">既定の率に足す率。</param>
/// <param name="Weights">Decider の重みに掛ける係数。キーは行動 ID か "tag:行動タグ"。</param>
/// <param name="Forbid">候補から外す行動 ID か行動タグ。</param>
/// <param name="Thresholds">反射のしきい値のずれ。</param>
/// <param name="Source">出どころ。</param>
public sealed record ConditionDef(
    string Id,
    string Display,
    ImmutableDictionary<VitalName, double> Rates,
    ImmutableDictionary<string, double> Weights,
    ImmutableArray<string> Forbid,
    ImmutableDictionary<VitalName, double> Thresholds,
    DefinitionSource Source);
