using System.Globalization;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Storage;
using Vivace.Core.Time;

namespace Vivace.Cli;

/// <summary>流れる出力の1行を作る。形は "MM-dd HH:mm 文"。時刻はワールドの現地の時刻にする。文の中の人、場所、行動は表示名にする。</summary>
public static class EventLineFormatter
{
    /// <summary>
    /// 適用されたイベントを1行にする。既定では行動と移動の開始と終わりだけを文にし、それ以外は null を返す。
    /// all のときは、人が読んで意味のあるイベントも文にし、ほかは人（またはワールド）とイベントタイプの名前を括弧で書く。
    /// </summary>
    /// <param name="logged">適用の記録。</param>
    /// <param name="names">表示名を引く表。</param>
    /// <param name="utcOffsets">ワールド ID ごとの現地の時差。記録にないワールドは UTC とみなす。</param>
    /// <param name="all">すべてのイベントを出すかどうか。</param>
    public static string? Format(LoggedEvent logged, DisplayNames names, IReadOnlyDictionary<WorldId, TimeSpan> utcOffsets, bool all)
    {
        var ev = logged.Event;
        var sentence = Sentence(ev, names, all);
        if (sentence is null)
        {
            return null;
        }

        var offset = WorldOf(ev, names) is { } world && utcOffsets.TryGetValue(world, out var found) ? found : TimeSpan.Zero;
        var (date, time) = LocalTimeHelper.ToLocal(ev.Time, offset);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{date:MM-dd} {time:HH:mm} {sentence}");
    }

    /// <summary>イベントの文。出さないイベントは null。</summary>
    /// <param name="ev">イベント。</param>
    /// <param name="names">表示名を引く表。</param>
    /// <param name="all">すべてのイベントを出すかどうか。</param>
    private static string? Sentence(SimEvent ev, DisplayNames names, bool all)
    {
        var core = ActionOrTravel(ev, names);
        if (core is not null)
        {
            return core;
        }

        if (!all)
        {
            return null;
        }

        return Readable(ev, names) ?? UntypedLabel(ev, names);
    }

    /// <summary>行動と移動の文。ほかのイベントは null。</summary>
    /// <param name="ev">イベント。</param>
    /// <param name="names">表示名を引く表。</param>
    private static string? ActionOrTravel(SimEvent ev, DisplayNames names)
    {
        var who = PersonaOrWorld(ev, names);
        switch (ev.Payload)
        {
            case ActionStartedPayload started:
                return $"{who}は{names.Place(started.Place, WorldOf(ev, names))}で「{names.Action(started.ActionId)}」（{ReportText.ReasonLabel(started.Reason)}）";
            case ActionEndedPayload ended:
                return $"{who}は「{names.Action(ended.ActionId)}」を終えた";
            case TravelStartedPayload travel:
                return $"{who}は{names.Place(travel.From, WorldOf(ev, names))}から{names.Place(travel.To, WorldOf(ev, names))}へ向かった";
            case TravelArrivedPayload arrived:
                return $"{who}は{names.Place(arrived.Place, WorldOf(ev, names))}に着いた";
            default:
                return null;
        }
    }

    /// <summary>人が読んで意味のあるイベントの文。文にできないものは null。</summary>
    /// <param name="ev">イベント。</param>
    /// <param name="names">表示名を引く表。</param>
    private static string? Readable(SimEvent ev, DisplayNames names)
    {
        var who = PersonaOrWorld(ev, names);
        switch (ev.Payload)
        {
            case DayStartedPayload day when WorldOf(ev, names) is { } world:
                return $"{names.World(world)}で {DateLabel(day.Date)}が始まった";
            case WorldCreatedPayload created:
                return $"{names.World(new WorldId(created.DefId.Value))}ができた";
            case PersonaCreatedPayload created:
                return $"{names.Persona(new PersonaId(created.DefId.Value))}が{names.Place(created.Home, WorldOf(ev, names))}に現れた";
            case PlanCommittedPayload:
                return $"{who}の予定が決まった";
            case ActivityChosenPayload chosen:
                return $"{who}は「{names.Action(chosen.ActionId)}」を選んだ";
            default:
                return null;
        }
    }

    /// <summary>文にできないイベントの表記。人（またはワールド）があれば前に付け、イベントタイプの名前を括弧で書く。</summary>
    /// <param name="ev">イベント。</param>
    /// <param name="names">表示名を引く表。</param>
    private static string UntypedLabel(SimEvent ev, DisplayNames names) =>
        ev.PersonaId is null && ev.WorldId is null
            ? $"（{ev.EventType}）"
            : $"{PersonaOrWorld(ev, names)}（{ev.EventType}）";

    /// <summary>
    /// イベントのワールド。記録になければ、人（ID か生成のペイロードの定義）のワールド、ワールドの生成ならペイロードのワールド。時差と場所の名前に使う。
    /// </summary>
    /// <param name="ev">イベント。</param>
    /// <param name="names">表示名を引く表。</param>
    private static WorldId? WorldOf(SimEvent ev, DisplayNames names) =>
        ev.WorldId
        ?? (ev.PersonaId is { } persona ? names.WorldOf(persona) : null)
        ?? ev.Payload switch
        {
            WorldCreatedPayload created => new WorldId(created.DefId.Value),
            PersonaCreatedPayload created => names.WorldOf(new PersonaId(created.DefId.Value)),
            _ => null,
        };

    /// <summary>人の表示名。人がなければワールドの表示名。どちらもなければ "-"。</summary>
    /// <param name="ev">イベント。</param>
    /// <param name="names">表示名を引く表。</param>
    private static string PersonaOrWorld(SimEvent ev, DisplayNames names) =>
        ev.PersonaId is { } persona ? names.Persona(persona)
        : ev.WorldId is { } world ? names.World(world)
        : "-";

    /// <summary>日付の表記。"11月16日（月）" の形。</summary>
    /// <param name="date">日付。</param>
    private static string DateLabel(DateOnly date) =>
        string.Create(CultureInfo.InvariantCulture, $"{date.Month:00}月{date.Day:00}日（{ReportText.Weekday(date)}）");
}
