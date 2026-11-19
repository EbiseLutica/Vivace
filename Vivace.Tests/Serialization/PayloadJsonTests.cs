using System.IO;
using System.Text.Json;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Serialization;

namespace Vivace.Tests.Serialization;

/// <summary>ペイロードの JSON の往復と、知らないイベントタイプ・バージョンでの停止。</summary>
public class PayloadJsonTests
{
    /// <summary>手で作った例が、すべてのイベントタイプをちょうど1つずつ覆う。</summary>
    [Fact]
    public void SamplesCoverEveryEventType()
    {
        var names = EventTypes.All.Select(info => info.Name).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(names, PayloadSamples.ByEventType.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(36, names.Count);
    }

    /// <summary>各イベントタイプのペイロードを JSON にして読み戻すと、型と JSON の中身が一致する。</summary>
    /// <param name="eventType">イベントタイプの名前。</param>
    [Theory]
    [MemberData(nameof(EventTypeNames))]
    public void PayloadRoundTripsThroughJson(string eventType)
    {
        var payload = PayloadSamples.ByEventType[eventType];

        var json = PayloadJson.Serialize(eventType, payload);
        var restored = PayloadJson.Deserialize(eventType, json);

        Assert.Equal(payload.GetType(), restored.GetType());
        Assert.Equal(json, PayloadJson.Serialize(eventType, restored));
    }

    /// <summary>Theory の引数に使う、イベントタイプの名前の列。</summary>
    public static TheoryData<string> EventTypeNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in PayloadSamples.ByEventType.Keys.Order(StringComparer.Ordinal))
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>小さな例の JSON は、手で書いた形と一致する（項目名は PascalCase、版は "v"）。</summary>
    [Fact]
    public void ActionEndedPayloadHasHandWrittenShape()
    {
        var json = PayloadJson.Serialize(EventTypes.ActionEnded, new ActionEndedPayload("read", 3));

        Assert.Equal("{\"v\":1,\"ActionId\":\"read\",\"ActivityVersion\":3,\"IsSubAction\":false}", json);
    }

    /// <summary>知らないイベントタイプは、読むときに例外になる（握りつぶさない）。</summary>
    [Fact]
    public void UnknownEventTypeThrows()
    {
        var error = Assert.Throws<UnknownEventTypeException>(() => PayloadJson.Deserialize("future.thing", "{\"v\":1}"));

        Assert.Equal("future.thing", error.EventType);
    }

    /// <summary>このコードが知らないバージョンは、読むときに例外になる。</summary>
    [Fact]
    public void UnknownPayloadVersionThrows()
    {
        Assert.Throws<InvalidDataException>(() => PayloadJson.Deserialize(EventTypes.ActionEnded, "{\"v\":2,\"ActionId\":\"read\",\"ActivityVersion\":3,\"IsSubAction\":false}"));
    }

    /// <summary>イベントタイプと型が合わないペイロードは、書けない。</summary>
    [Fact]
    public void MismatchedPayloadTypeThrows()
    {
        Assert.Throws<ArgumentException>(() => PayloadJson.Serialize(EventTypes.PlaceAdopted, new ActionEndedPayload("read", 3)));
    }

    /// <summary>居場所は、型の印（$type）で場所にいる形と移動中の形を区別して往復する。</summary>
    [Fact]
    public void LocationRoundTripsWithTypeMarker()
    {
        var home = new PlaceId("home");
        var station = new PlaceId("station");
        var traveling = new Traveling(home, station, [new RouteStep(home, station, 5)], new DateTimeOffset(2026, 11, 1, 14, 5, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 1, 14, 10, 0, TimeSpan.Zero));
        Location[] locations = [new AtPlace(home), traveling];

        foreach (var location in locations)
        {
            var json = JsonSerializer.Serialize(location, VivaceJsonContext.Default.Location);
            var restored = JsonSerializer.Deserialize(json, VivaceJsonContext.Default.Location);

            Assert.Equal(json, JsonSerializer.Serialize(restored, VivaceJsonContext.Default.Location));
        }

        Assert.Equal("{\"$type\":\"atPlace\",\"Place\":\"home\"}", JsonSerializer.Serialize(locations[0], VivaceJsonContext.Default.Location));
    }

    /// <summary>guard の検査の列は、型の印で往復する。</summary>
    [Fact]
    public void GuardRoundTripsWithChecks()
    {
        var home = new PlaceId("home");
        var guard = new Guard([new ActivityVersionCheck(4), new FeasibleCheck("read", home), new RngCursorCheck("taro", "decide", 9)], RejectAction.Redecide);

        var json = JsonSerializer.Serialize(guard, VivaceJsonContext.Default.Guard);
        var restored = JsonSerializer.Deserialize(json, VivaceJsonContext.Default.Guard);

        Assert.Equal(json, JsonSerializer.Serialize(restored, VivaceJsonContext.Default.Guard));
        Assert.Equal("{\"Checks\":[{\"$type\":\"activityVersion\",\"Expected\":4},{\"$type\":\"feasible\",\"ActionId\":\"read\",\"Place\":\"home\"},{\"$type\":\"rngCursor\",\"Owner\":\"taro\",\"Purpose\":\"decide\",\"Position\":9}],\"OnReject\":0}", json);
    }
}
