using System.Collections.Immutable;

namespace Vivace.Core.Domain;

/// <summary>プランのブロック。開始と終了は UTC の絶対時刻。自由時間は ID を持たない。電話の指定と同行は、プランを作ったときの値を持つので、固定予定を変えても次のプランまで変わらない。</summary>
/// <param name="BlockId">ブロックの ID（定義 ID@開始日）。自由時間だけ null。</param>
/// <param name="Kind">ブロックの種類。</param>
/// <param name="Start">開始。</param>
/// <param name="End">終了。</param>
/// <param name="ActionId">行動の定義 ID。自由時間は null。</param>
/// <param name="Place">場所。指定がなければ null。</param>
/// <param name="Phone">スマホを開けるか。</param>
/// <param name="PhoneEvery">長いブロックの途中の機会の間隔。</param>
/// <param name="With">同行するペルソナの定義 ID。</param>
/// <param name="OnPartnerAbsent">同行者が抜けたときの扱い。</param>
public sealed record PlanBlock(
    string? BlockId,
    BlockKind Kind,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? ActionId,
    PlaceId? Place,
    PhonePolicy Phone,
    TimeSpan? PhoneEvery,
    ImmutableArray<string> With,
    PartnerAbsentPolicy OnPartnerAbsent);
