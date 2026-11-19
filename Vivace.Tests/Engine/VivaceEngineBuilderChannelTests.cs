using Vivace.Core;
using Vivace.Core.Channels;
using Vivace.Core.Time;
using Vivace.Testing;

namespace Vivace.Tests.Engine;

/// <summary>ビルダーのチャンネルと、ドライランの時計の検査。</summary>
public class VivaceEngineBuilderChannelTests
{
    /// <summary>ドライランで時計を省くと、実時間になるので Build が例外を投げる。</summary>
    [Fact]
    public void DryRunWithoutVirtualClockThrows()
    {
        var builder = VivaceEngine.Create().UseDefinitions(TestWorld.LoadOrThrow()).UseStorage(new InMemoryStorage()).DryRun();

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("VirtualClock", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>ドライランで SystemClock を渡しても、同じく例外を投げる。</summary>
    [Fact]
    public void DryRunWithSystemClockThrows()
    {
        var builder = VivaceEngine.Create().UseDefinitions(TestWorld.LoadOrThrow()).UseStorage(new InMemoryStorage()).UseClock(new SystemClock()).DryRun();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    /// <summary>ドライランでは、エンジンの StartAsync がチャンネルを1つも起動しない（対照として、通常の運転では1回起動する）。</summary>
    [Fact]
    public async Task DryRunDoesNotStartChannels()
    {
        var dry = new FakeChannel("console", (_, _) => Task.FromResult<SendResult>(new SendResult.Sent("x")));
        var normal = new FakeChannel("console", (_, _) => Task.FromResult<SendResult>(new SendResult.Sent("x")));
        await using var dryEngine = VivaceEngine.Create()
            .UseDefinitions(TestWorld.LoadOrThrow())
            .UseStorage(new InMemoryStorage())
            .UseClock(new VirtualClock(Scenario.Start))
            .AddChannel(dry)
            .DryRun()
            .Build();
        await using var normalEngine = VivaceEngine.Create()
            .UseDefinitions(TestWorld.LoadOrThrow())
            .UseStorage(new InMemoryStorage())
            .UseClock(new VirtualClock(Scenario.Start))
            .AddChannel(normal)
            .Build();

        await dryEngine.StartAsync(CancellationToken.None);
        await normalEngine.StartAsync(CancellationToken.None);

        Assert.Equal(0, dry.Starts);
        Assert.Equal(1, normal.Starts);
    }

    /// <summary>同じ種類のチャンネルを2つつなぐと、Build が例外を投げる。</summary>
    [Fact]
    public void DuplicateChannelKindThrows()
    {
        var builder = VivaceEngine.Create()
            .UseDefinitions(TestWorld.LoadOrThrow())
            .UseStorage(new InMemoryStorage())
            .AddChannel(new NullChannel("console"))
            .AddChannel(new NullChannel("console"));

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("console", ex.Message, StringComparison.Ordinal);
    }
}
