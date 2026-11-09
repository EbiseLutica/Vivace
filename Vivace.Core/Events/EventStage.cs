namespace Vivace.Core.Events;

/// <summary>
/// 段。同じ時刻のイベントを並べる順番（D2 2.2）。数が小さいほど先に適用する。
/// </summary>
public enum EventStage
{
    /// <summary>1: 日付が変わる。</summary>
    DayTurn = 1,

    /// <summary>2: 終わることが終わる。</summary>
    Ending = 2,

    /// <summary>3: 世界が変わる。</summary>
    World = 3,

    /// <summary>4: 体が反応する。</summary>
    Reaction = 4,

    /// <summary>5: 外から届く。</summary>
    External = 5,

    /// <summary>6: 予定の時刻になる。</summary>
    Schedule = 6,

    /// <summary>7: 決めて始める。</summary>
    Decision = 7,
}
