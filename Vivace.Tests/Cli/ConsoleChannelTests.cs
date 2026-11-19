using System.Text.Json;
using Vivace.Cli;
using Vivace.Core.Channels;
using Vivace.Core.Domain;
using Vivace.Core.Time;
using Vivace.Testing;
using Vivace.Tests.Support;

namespace Vivace.Tests.Cli;

/// <summary>コンソールのチャンネル。投稿を、ペルソナの表示名と現地の時刻つきで1行に書き、送ったものとして返す。</summary>
public class ConsoleChannelTests
{
    /// <summary>送った投稿は、現地の時刻、チャンネルの種類、表示名、本文の1行になる。</summary>
    [Fact]
    public async Task SendWritesOneLineWithLocalTimeAndDisplayName()
    {
        var definitions = TestWorld.LoadOrThrow();
        var offsets = definitions.Worlds.Values.ToDictionary(world => new WorldId(world.Id.Value), world => world.UtcOffset);
        var output = new StringWriter { NewLine = "\n" };
        var channel = new ConsoleChannel(new DisplayNames(definitions), offsets, output);
        var request = new OutboundRequest(
            Seq: 22,
            Persona: new PersonaId("kaori"),
            At: new DateTimeOffset(2026, 11, 1, 14, 8, 0, TimeSpan.Zero),
            OperationKind: "post",
            IsSpeech: true,
            Payload: JsonDocument.Parse("""{"body":"今日の授業眠い"}""").RootElement.Clone());

        var result = await channel.SendAsync(request, CancellationToken.None);

        Assert.Equal("console", channel.Kind);
        Assert.Equal("11-01 23:08 [送信 console] 香織: 今日の授業眠い\n", output.ToString());
        Assert.Equal(new SendResult.Sent("console-22"), result);
    }

    /// <summary>run は投稿を送信の行として書く（文面の仮の LLM を使う）。</summary>
    [Fact]
    public async Task RunPrintsPostsAsChannelLines()
    {
        using var folder = new DefinitionFolder(TestWorld.Files().ToList());
        var output = new StringWriter { NewLine = "\n" };

        var code = await RunCommand.RunAsync(folder.Path, new VirtualClock(Scenario.Start), Scenario.WorldSeed, false, output, CancellationToken.None, Scenario.Start.AddDays(2));

        Assert.Equal(0, code);
        Assert.Contains("[送信 console] 香織: 今日の授業眠い\n", output.ToString(), StringComparison.Ordinal);
    }
}
