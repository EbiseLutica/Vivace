using System.Text.Json;
using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Serialization;
using Vivace.Core.Serialization.Converters;
using Vivace.Core.Storage;
using Vivace.Storage.Sqlite;
using Vivace.Testing;

namespace Vivace.Tests.Storage;

/// <summary>
/// テスト用の町を SQLite に書いて、開き直して読み戻すと、記録と状態が書く前と一致する（往復）。
/// レコードの等価は ImmutableArray と辞書で効かないので、比べるのは JSON の行にしたもの。
/// </summary>
public class SqliteRoundTripTests
{
    /// <summary>シナリオの記録と最後の状態は、SQLite に書いて開き直しても同じ。</summary>
    [Fact]
    public async Task SevenDaysReadBackEqualToTheEngine()
    {
        using var temp = new TempDatabase();
        var ct = TestContext.Current.CancellationToken;
        string[] expectedEvents;
        string[] expectedApplied;
        string[] expectedState;

        await using (var database = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct))
        await using (var storage = new SqliteStorage(database))
        await using (var engine = await Scenario.CreateAsync(storage: storage, llm: ScriptedPosts()))
        {
            await engine.RunUntilAsync(Scenario.Start.AddDays(7), ct);
            expectedEvents = engine.Log.OrderBy(logged => logged.Event.Seq).Select(EventLine).ToArray();
            expectedApplied = engine.Log.Where(logged => logged.Status == EventStatus.Applied).Select(EventLine).ToArray();
            expectedState = StateLines(engine.State);
        }

        await using var reopened = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct);
        await using var restored = new SqliteStorage(reopened);
        var events = await restored.ReadEventsAsync(ct);
        var state = await restored.LoadStateAsync(ct);

        Assert.NotEmpty(expectedEvents);
        Assert.Equal(expectedEvents, events.Select(EventLine).ToArray());
        Assert.Equal(expectedApplied, events.Where(logged => logged.Status == EventStatus.Applied).OrderBy(logged => logged.ApplyNo).Select(EventLine).ToArray());
        Assert.NotNull(state);
        Assert.Equal(expectedState, StateLines(state));
    }

    /// <summary>知らないイベントタイプの行を読むと、例外で止まる（握りつぶさない）。</summary>
    [Fact]
    public async Task UnknownEventTypeInTheLogStopsTheRead()
    {
        using var temp = new TempDatabase();
        var ct = TestContext.Current.CancellationToken;

        await using (var database = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct))
        {
            await using var command = database.Connection.CreateCommand();
            command.CommandText = "INSERT INTO events (seq, time_utc, event_type, status, payload, origin, enqueued_utc) VALUES (1, 0, 'future.thing', 'applied', '{\"v\":1}', '{\"Kind\":0}', 0)";
            await command.ExecuteNonQueryAsync(ct);
        }

        await using var reopened = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct);
        await using var storage = new SqliteStorage(reopened);

        var error = await Assert.ThrowsAsync<UnknownEventTypeException>(() => storage.ReadEventsAsync(ct));
        Assert.Equal("future.thing", error.EventType);
    }

    /// <summary>JSON の行にするときの設定（ID と時刻の変換は本番と同じ）。</summary>
    private static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>テスト用のシナリオと同じ、投稿の文面を返す LLM。</summary>
    private static ScriptedLlm ScriptedPosts() => new ScriptedLlm().Reply("post.compose", "今日の授業眠い");

    /// <summary>記録の1件を、比べるための1行にする（ペイロードは PayloadJson、ほかは JSON）。</summary>
    private static string EventLine(LoggedEvent logged)
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
    private static string[] StateLines(SimState state)
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

    /// <summary>JSON の行の設定。ID と時刻は、永続化の JSON と同じ変換を使う。</summary>
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
