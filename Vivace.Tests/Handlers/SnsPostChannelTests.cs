using System.Collections.Immutable;
using Vivace.Core;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Handlers;
using Vivace.Core.Routes;
using Vivace.Core.Signals;
using Vivace.Testing;
using Vivace.Tests.Support;

namespace Vivace.Tests.Handlers;

/// <summary>投稿の送り先。ペルソナの channels の先頭を使い、channels がなければ投稿の行動を候補にしない。</summary>
public class SnsPostChannelTests
{
    /// <summary>発話の送り先は、ペルソナの channels の先頭。</summary>
    [Fact]
    public async Task SpeechGoesToTheFirstChannelOfThePersona()
    {
        var defs = WithChannels(["misskey", "console"]);

        var draft = await SnsPostHandler.RunAsync(new RuleContext(defs, RouteCache.Create(defs)), new ScriptedLlm().Reply("post.compose", "今日の授業眠い"), Fixture.WithKaori(), Effect());

        var speech = Assert.IsType<OutboundSpeechPayload>(draft.Payload);
        Assert.Equal("misskey", speech.Channel);
    }

    /// <summary>channels がないペルソナの投稿は、文面を作らずに、つながるチャンネルがない理由でスキップする。</summary>
    [Fact]
    public async Task PersonaWithoutChannelIsSkippedWithoutGenerating()
    {
        var defs = WithChannels([]);

        var draft = await SnsPostHandler.RunAsync(new RuleContext(defs, RouteCache.Create(defs)), new ScriptedLlm(), Fixture.WithKaori(), Effect());

        Assert.Equal(EventTypes.PostSkipped, draft.EventType);
        Assert.Equal(PostSkippedPayload.PostSkipReason.NoChannel, ((PostSkippedPayload)draft.Payload).Reason);
    }

    /// <summary>channels がないペルソナには、投稿の行動が候補にならない（主の行動も副行動も同じ）。</summary>
    [Fact]
    public void PostActionNeedsAChannel()
    {
        var defs = TestWorld.LoadOrThrow();
        var kaori = defs.Personas[new PersonaDefId("kaori")];
        var post = defs.Actions["post_sns"];
        var read = defs.Actions["read_book"];

        Assert.True(ActionRules.HasChannelFor(kaori, post));
        Assert.False(ActionRules.HasChannelFor(kaori with { Channels = [] }, post));
        Assert.True(ActionRules.HasChannelFor(kaori with { Channels = [] }, read));
    }

    /// <summary>テスト用の定義の香織の channels を差し替えた定義。</summary>
    private static DefinitionPack WithChannels(ImmutableArray<string> channels)
    {
        var defs = TestWorld.LoadOrThrow();
        var id = new PersonaDefId("kaori");
        return defs with { Personas = defs.Personas.SetItem(id, defs.Personas[id] with { Channels = channels }) };
    }

    /// <summary>香織の投稿の効果（行動のバージョン 0、所要時間 8 分）。</summary>
    private static StartEffectSignal Effect() =>
        new(10, Fixture.Kaori, Fixture.Now.AddMinutes(8), "post_sns", new PlaceId("kaori_room"), 0);
}
