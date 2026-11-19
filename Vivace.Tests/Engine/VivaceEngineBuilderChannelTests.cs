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
