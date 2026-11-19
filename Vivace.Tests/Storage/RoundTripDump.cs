using System.Text.Json;
using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Serialization;
using Vivace.Core.Serialization.Converters;
using Vivace.Core.Storage;

namespace Vivace.Tests.Storage;

/// <summary>
/// 往復のテストで比べる形。記録と状態を、JSON の行の列にする（レコードの等価は ImmutableArray と辞書で効かないため）。
/// </summary>
internal static class RoundTripDump
{
    /// <summary>行にするときの JSON の設定。ID と時刻は、永続化と同じ変換を使う。</summary>
    private static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>記録の1件を、比べるための1行にする（ペイロードは PayloadJson、ほかは JSON）。</summary>
    /// <param name="logged">記録の1件。</param>
    public static string EventLine(LoggedEvent logged)
    {
        var ev = logged.Event;
        return JsonSerializer.Serialize(new
        {
            ev.Seq,
            ev.Time,
            ev.EventType,
            World = ev.WorldId,
            Persona = ev.PersonaId,
            Payload = PayloadJson.Serialize(ev.EventType, ev.Payload),
            ev.Guard,
            ev.BatchSeq,
            ev.CauseSeq,
            ev.Origin,
            Trace = ev.Trace?.GetRawText(),
            ev.RealTime,
            logged.Status,
            logged.ApplyNo,
            logged.Reason,
        }, Options);
    }

    /// <summary>状態を、比べるための行の列にする。ワールドとペルソナは ID の序数の順、辞書はキーの序数の順。</summary>
    /// <param name="state">状態。</param>
    public static string[] StateLines(SimState state)
    {
        var lines = new List<string> { "cursor " + JsonSerializer.Serialize(state.Cursor, Options) };
        foreach (var world in state.Worlds.Values.OrderBy(world => world.Id.Value, StringComparer.Ordinal))
        {
            lines.Add("world " + JsonSerializer.Serialize(new { world.Id, world.DefId, world.Seed, world.Status, world.GraphVersion }, Options));
            foreach (var place in world.GeneratedPlaces.Values.OrderBy(place => place.Id.Value, StringComparer.Ordinal))
            {
                lines.Add("place " + JsonSerializer.Serialize(place, Options));
            }

            foreach (var term in world.Terms.Values.OrderBy(term => term.Term, StringComparer.Ordinal))
            {
                lines.Add("term " + JsonSerializer.Serialize(term, Options));
            }

            foreach (var incident in world.Incidents.Values.OrderBy(incident => incident.Seq))
            {
                lines.Add("incident " + JsonSerializer.Serialize(incident, Options));
            }
        }

        foreach (var persona in state.Personas.Values.OrderBy(persona => persona.Id.Value, StringComparer.Ordinal))
        {
            lines.Add("persona " + JsonSerializer.Serialize(new
            {
                persona.Id,
                persona.DefId,
                persona.World,
                persona.Status,
                persona.Location,
                persona.Activity,
                persona.ActivityVersion,
                persona.Overlay,
                persona.Vitals,
                persona.Conditions,
                persona.Plan,
                persona.PlanVersion,
                Usage = persona.Usage.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => new { entry.Key, entry.Value }),
                Rng = persona.Rng.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => new { entry.Key, entry.Value }),
                persona.Posting,
                persona.Inbox,
                Outbound = persona.Outbound.Select(operation => new { operation.Seq, operation.Channel, operation.OperationKind, operation.IsSpeech, Payload = operation.Payload.GetRawText() }),
                persona.LastWake,
                persona.Deciding,
                persona.LastBlockId,
                persona.PlannedFor,
            }, Options));
        }

        return lines.ToArray();
    }

    /// <summary>行の設定を作る。</summary>
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new PlaceIdConverter());
        options.Converters.Add(new PersonaIdConverter());
        options.Converters.Add(new WorldIdConverter());
        options.Converters.Add(new PersonaDefIdConverter());
        options.Converters.Add(new WorldDefIdConverter());
        options.Converters.Add(new UtcDateTimeOffsetConverter());
        return options;
    }
}
