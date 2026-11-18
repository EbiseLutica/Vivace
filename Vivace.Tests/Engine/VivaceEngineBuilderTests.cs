using Vivace.Core.Decision;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Time;
using Vivace.Core;
using Vivace.Testing;
using Vivace.Tests.Support;

namespace Vivace.Tests.Engine;

/// <summary>公開 API のビルダー。必須のもの（定義、保存先）が欠けると例外になり、そろえば組んだエンジンが動く。時計は省略できる。</summary>
public class VivaceEngineBuilderTests
{
    /// <summary>時計を省略すると、実時間の SystemClock が使われ、例外にならない。渡せば、そのまま使われる。</summary>
    [Fact]
    public async Task ClockDefaultsToSystemClockAndUsesGivenClock()
    {
        Assert.IsType<SystemClock>(VivaceEngine.Create().ResolveClock());

        var clock = new VirtualClock(Scenario.Start);
        Assert.Same(clock, VivaceEngine.Create().UseClock(clock).ResolveClock());

        await using var engine = VivaceEngine.Create().UseDefinitions(TestWorld.LoadOrThrow()).UseStorage(new InMemoryStorage()).Build();
        Assert.NotNull(engine);
    }

    /// <summary>保存先がなければ、例外になる。</summary>
    [Fact]
    public void BuildWithoutStorageThrows()
    {
        var builder = VivaceEngine.Create().UseDefinitions(TestWorld.LoadOrThrow()).UseClock(new VirtualClock(Scenario.Start));

        var error = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Equal("ストレージが渡されていません。 UseStorage で指定してください。", error.Message);
    }

    /// <summary>定義がなければ、例外になる。</summary>
    [Fact]
    public void BuildWithoutDefinitionsThrows()
    {
        var builder = VivaceEngine.Create().UseStorage(new InMemoryStorage()).UseClock(new VirtualClock(Scenario.Start));

        var error = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Equal("定義がありません。 UseDefinitions で指定してください。", error.Message);
    }

    /// <summary>定義のフォルダにエラーがあれば、例外になり、エラーの位置（ファイル）が分かる。</summary>
    [Fact]
    public void BuildWithBrokenDefinitionsThrows()
    {
        var directory = WriteDefinitions(("worlds/broken.toml", "[[world]\nid = \n"));
        try
        {
            var builder = VivaceEngine.Create()
                .UseDefinitions(directory)
                .UseStorage(new InMemoryStorage())
                .UseClock(new VirtualClock(Scenario.Start));

            var error = Assert.Throws<InvalidOperationException>(builder.Build);

            Assert.StartsWith("定義エラーです: ", error.Message, StringComparison.Ordinal);
            Assert.Contains("worlds/broken.toml", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// テスト用の定義のフォルダから組んだエンジンで、ワールドと香織を投入して 1 時間回すと、両方が生きていて、
    /// 適用の位置は回した範囲（開始から 1 時間）に収まる。DryRun のフラグも残る。
    /// </summary>
    [Fact]
    public async Task BuiltEngineFromDirectoryRuns()
    {
        var directory = WriteDefinitions([.. TestWorld.Files().Select(file => (file.Path, file.Text))]);
        try
        {
            var builder = VivaceEngine.Create()
                .UseDefinitions(directory)
                .UseStorage(new InMemoryStorage())
                .UseClock(new VirtualClock(Scenario.Start))
                .UseHolidays(TestHolidays.Instance)
                .DryRun();
            await using var engine = builder.Build();

            var world = await engine.InjectAsync(InjectRequest.WorldCreated(TestWorld.DefId, Scenario.WorldSeed, Origin.Test), CancellationToken.None);
            var kaori = await engine.InjectAsync(InjectRequest.PersonaCreated(new PersonaDefId("kaori"), new PlaceId("kaori_room"), Origin.Test), CancellationToken.None);
            await engine.RunUntilAsync(Scenario.Start.AddHours(1), CancellationToken.None);

            Assert.True(builder.IsDryRun);
            Assert.Equal(InjectResult.Accept(1), world);
            Assert.Equal(InjectResult.Accept(2), kaori);
            Assert.True(engine.View.Worlds.ContainsKey(new WorldId("testville")));
            Assert.True(engine.View.Personas.ContainsKey(new PersonaId("kaori")));
            Assert.InRange(engine.View.Cursor.Time, Scenario.Start, Scenario.Start.AddHours(1));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>UseDecider で渡した決定を使う。決定が例外を投げても、代わりの決定でエンジンは止まらずに返る。</summary>
    [Fact]
    public async Task UseDeciderIsWiredAndFailuresFallBack()
    {
        var decider = new CountingThrowingDecider();
        await using var engine = VivaceEngine.Create()
            .UseDefinitions(TestWorld.LoadOrThrow())
            .UseStorage(new InMemoryStorage())
            .UseClock(new VirtualClock(Scenario.Start))
            .UseDecider(decider)
            .Build();
        await engine.InjectAsync(InjectRequest.WorldCreated(TestWorld.DefId, Scenario.WorldSeed, Origin.Test), CancellationToken.None);
        await engine.InjectAsync(InjectRequest.PersonaCreated(new PersonaDefId("kaori"), new PlaceId("kaori_room"), Origin.Test), CancellationToken.None);

        await engine.RunUntilAsync(Scenario.Start.AddDays(1), CancellationToken.None);

        Assert.True(decider.Calls > 0);
    }

    /// <summary>呼ばれた回数を数えてから、例外を投げる決定。</summary>
    private sealed class CountingThrowingDecider : IDecider
    {
        /// <summary>呼ばれた回数。</summary>
        private int _calls;

        /// <summary>呼ばれた回数。</summary>
        public int Calls => _calls;

        /// <summary>数えてから例外を投げる。</summary>
        public DeciderOutput Decide(DecisionContext context)
        {
            Interlocked.Increment(ref _calls);
            return new ThrowingDecider().Decide(context);
        }
    }

    /// <summary>一時フォルダに定義のファイルを書き出し、そのフォルダを返す。</summary>
    private static string WriteDefinitions(params (string Path, string Content)[] files)
    {
        var directory = Path.Combine(Path.GetTempPath(), "vivace-tests", Path.GetRandomFileName());
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(directory, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        return directory;
    }
}
