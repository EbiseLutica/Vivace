using Vivace.Core.Decision;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Time;
using Vivace.Core;
using Vivace.Testing;

namespace Vivace.Tests.Engine;

/// <summary>公開 API のビルダー。必須のもの（定義、保存先、時計）が欠けると例外になり、そろえば組んだエンジンが動く。</summary>
public class VivaceEngineBuilderTests
{
    /// <summary>時計がなければ、例外になる。</summary>
    [Fact]
    public void BuildWithoutClockThrows()
    {
        var builder = VivaceEngine.Create().UseDefinitions(TestWorld.LoadOrThrow()).UseStorage(new InMemoryStorage());

        var error = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Equal("時計がない（UseClock で指定する）", error.Message);
    }

    /// <summary>保存先がなければ、例外になる。</summary>
    [Fact]
    public void BuildWithoutStorageThrows()
    {
        var builder = VivaceEngine.Create().UseDefinitions(TestWorld.LoadOrThrow()).UseClock(new VirtualClock(Scenario.Start));

        var error = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Equal("保存先がない（UseStorage で指定する）", error.Message);
    }

    /// <summary>定義がなければ、例外になる。</summary>
    [Fact]
    public void BuildWithoutDefinitionsThrows()
    {
        var builder = VivaceEngine.Create().UseStorage(new InMemoryStorage()).UseClock(new VirtualClock(Scenario.Start));

        var error = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Equal("定義がない（UseDefinitions で指定する）", error.Message);
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

            Assert.StartsWith("定義にエラーがある: ", error.Message, StringComparison.Ordinal);
            Assert.Contains("worlds/broken.toml", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// テスト用の定義のフォルダから組んだエンジンで、ワールドと香織を投入して 1 時間回すと、両方が生きていて、
    /// 適用の位置は回した範囲（開始から 1 時間）に収まる。DryRun の印も残る。
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

    /// <summary>UseDecider で渡した決定を使う（例外を投げる決定で、回すと止まる）。</summary>
    [Fact]
    public async Task UseDeciderIsWired()
    {
        await using var engine = VivaceEngine.Create()
            .UseDefinitions(TestWorld.LoadOrThrow())
            .UseStorage(new InMemoryStorage())
            .UseClock(new VirtualClock(Scenario.Start))
            .UseDecider(new ThrowingDecider())
            .Build();
        await engine.InjectAsync(InjectRequest.WorldCreated(TestWorld.DefId, Scenario.WorldSeed, Origin.Test), CancellationToken.None);
        await engine.InjectAsync(InjectRequest.PersonaCreated(new PersonaDefId("kaori"), new PlaceId("kaori_room"), Origin.Test), CancellationToken.None);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RunUntilAsync(Scenario.Start.AddDays(1), CancellationToken.None));

        Assert.Contains("わざと失敗させた決定", error.Message, StringComparison.Ordinal);
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

    /// <summary>必ず例外を投げる決定。</summary>
    private sealed class ThrowingDecider : IDecider
    {
        /// <summary>例外を投げる。</summary>
        public DeciderOutput Decide(DecisionContext context) => throw new InvalidOperationException("わざと失敗させた決定");
    }
}
