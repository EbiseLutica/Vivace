using Vivace.Core.Definitions.Model;
using Vivace.Core.Definitions;
using Vivace.Core.Domain.State;
using Vivace.Core.Domain;
using Vivace.Testing;

namespace Vivace.Tests.Definitions;

/// <summary>テスト用の定義が、型の形で正しく読めることを確かめる。</summary>
public class DefinitionLoaderTests
{
    /// <summary>テスト用の世界とペルソナが、ワールド、場所、ペルソナの型に写る。</summary>
    [Fact]
    public void ReadsWorldPlacesAndPersonas()
    {
        var definitions = TestWorld.LoadOrThrow();

        var world = definitions.Worlds[TestWorld.DefId];
        Assert.Equal(TimeSpan.FromHours(9), world.UtcOffset);
        Assert.Equal(6, world.Places.Length);
        Assert.Equal("クリスマスイブ", world.Events[new Vivace.Core.Time.MonthDay(12, 24)]);
        Assert.Equal(TermVerdict.Pass, world.Terms.Single().Verdict);

        var kaori = definitions.Personas[new PersonaDefId("kaori")];
        Assert.Equal("kaori_room", kaori.Home);
        Assert.Equal(TestWorld.DefId, kaori.World);
        Assert.Equal(60, kaori.Vitals.SocialCeiling);
        Assert.Equal(TimeSpan.FromHours(7), kaori.Vitals.SleepTarget);
        Assert.Equal(PreferenceKind.Factor, kaori.Prefs["cafe"].Kind);
        Assert.Equal(PreferenceKind.Dislike, kaori.Prefs["tag:outdoor"].Kind);
        Assert.Equal(FollowBackPolicy.Always, kaori.FollowBack);
    }

    /// <summary>省略した状態変数と反射と乱れの重みは、既定値になる。</summary>
    [Fact]
    public void OmittedValuesTakeDefaults()
    {
        var shiori = TestWorld.LoadOrThrow().Personas[new PersonaDefId("shiori")];

        Assert.Equal(80, shiori.Vitals.EnergyInitial);
        Assert.Equal(-50, shiori.Vitals.SocialFloor);
        Assert.Equal(100, shiori.Vitals.SocialCeiling);
        Assert.Equal(TimeSpan.FromHours(7), shiori.Vitals.SleepTarget);
        Assert.Equal(0, shiori.Reflexes[Vivace.Core.Domain.State.VitalName.Energy].Threshold);
        Assert.Equal("core.faint", shiori.Reflexes[Vivace.Core.Domain.State.VitalName.Energy].ActionId);
        Assert.Equal(20, shiori.Reflexes[Vivace.Core.Domain.State.VitalName.Health].Threshold);
        Assert.Equal("core.go_home_rest", shiori.Reflexes[Vivace.Core.Domain.State.VitalName.Health].ActionId);
        Assert.Equal(0.3, shiori.Disruption.Absent);
        Assert.Equal(1.0, shiori.MemoryHalfLife);
    }

    /// <summary>組み込みの行動（core.*）が埋め込みから読め、効果と固定の長さが正しい。</summary>
    [Fact]
    public void BuiltinActionsAreLoadedFromEmbeddedResource()
    {
        var definitions = TestWorld.LoadOrThrow();

        var sleep = definitions.Actions["core.sleep"];
        Assert.True(sleep.ScheduleOnly);
        Assert.Equal(0, sleep.Effects.Rates[Vivace.Core.Domain.State.VitalName.Energy]);
        Assert.Equal(100, sleep.Effects.Restore[Vivace.Core.Domain.State.VitalName.Energy]);
        Assert.Equal(new SleepEffect(Consolidate: true, Debt: true), sleep.Effects.Sleep);

        var faint = definitions.Actions["core.faint"];
        Assert.NotNull(faint.Duration);
        Assert.False(faint.Duration!.IsRange);
        Assert.Equal(TimeSpan.FromHours(2), faint.Duration.Min);
        Assert.Equal(new SleepEffect(Consolidate: true, Debt: false), faint.Effects.Sleep);

        var goToNext = definitions.Actions["core.go_to_next"];
        Assert.Null(goToNext.Duration);
        Assert.False(goToNext.ScheduleOnly);
    }

    /// <summary>読み込んだ定義の全体にハッシュが付き、同じ入力なら同じハッシュになる。</summary>
    [Fact]
    public void HashIsStableForSameInput()
    {
        var first = TestWorld.LoadOrThrow();
        var second = TestWorld.LoadOrThrow();

        Assert.False(string.IsNullOrEmpty(first.Hash));
        Assert.Equal(first.Hash, second.Hash);
    }

    /// <summary>フォルダを読む入口（LoadDirectory）も、埋め込みと同じ結果になる。</summary>
    [Fact]
    public void LoadsFromDirectoryWithTheSameResult()
    {
        var directory = Path.Combine(Path.GetTempPath(), "vivace-defs-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var file in TestWorld.Files())
            {
                var path = Path.Combine(directory, file.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, file.Text);
            }

            var result = DefinitionLoader.LoadDirectory(directory);

            Assert.Empty(result.Issues);
            Assert.Equal(TestWorld.LoadOrThrow().Hash, result.Definitions!.Hash);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
