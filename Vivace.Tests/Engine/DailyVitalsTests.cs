using System.Globalization;
using System.Text;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Rules;
using Vivace.Core.Storage;
using Vivace.Tests.Support;
using Vivace.Testing;

namespace Vivace.Tests.Engine;

/// <summary>
/// 毎日 0:00（現地）の状態変数を、シナリオの途中で読んで固定する。事実のイベントの列は状態変数を見ないので、これで率の誤りを捕まえる。
/// また、決める側の出力が、契機の出所を引き継ぐことを確かめる。
/// </summary>
public class DailyVitalsTests
{
    /// <summary>7 日分の 0:00 の状態変数（気力、健康度、退屈度、社交欲求）が、保存された期待値と一致する。</summary>
    [Fact]
    public async Task DailyVitalsMatchSnapshot()
    {
        await using var engine = Scenario.Create();
        var definitions = TestWorld.LoadOrThrow();
        var text = new StringBuilder();
        for (var day = 1; day <= 7; day++)
        {
            var midnight = Scenario.Start.AddHours(1).AddDays(day - 1);
            await engine.RunUntilAsync(midnight, CancellationToken.None);
            foreach (var persona in engine.State.Personas.Values.OrderBy(p => p.Id.Value, StringComparer.Ordinal))
            {
                var vitals = persona.Vitals;
                var social = VitalRules.Range(VitalName.Social, definitions.Personas[persona.DefId].Vitals);
                text.Append(midnight.ToUniversalTime().ToString("yyyy-MM-ddTHH:mmZ", CultureInfo.InvariantCulture))
                    .Append(' ').Append(persona.Id.Value)
                    .Append(" energy=").Append(F(VitalRules.Read(vitals.Energy, midnight, (0, 100))))
                    .Append(" health=").Append(F(VitalRules.Read(vitals.Health, midnight, (0, 100))))
                    .Append(" boredom=").Append(F(VitalRules.Read(vitals.Boredom, midnight, (0, 100))))
                    .Append(" social=").Append(F(VitalRules.Read(vitals.Social, midnight, social)))
                    .Append('\n');
            }
        }

        await Snapshot.Text(text.ToString());
    }

    /// <summary>決める側の出力は、契機の出所を引き継ぐ。テストが注入した生成から始まった計画は、テストの出所を持つ。</summary>
    [Fact]
    public async Task HandlerOutputsInheritTheOriginOfTheirTrigger()
    {
        await using var engine = await Scenario.RunAsync(1);

        var plans = engine.Log.Where(entry => entry.Status == EventStatus.Applied && entry.Event.EventType == EventTypes.PlanCommitted).ToList();
        var fromCreation = plans.Where(entry => entry.Event.CauseSeq is { } cause && engine.Log.Any(other => other.Event.Seq == cause && other.Event.EventType == EventTypes.PersonaCreated)).ToList();

        Assert.Equal(2, fromCreation.Count);
        Assert.All(fromCreation, entry => Assert.Equal(Origin.Test, entry.Event.Origin));
    }

    /// <summary>1週間分の 0:00 の状態変数の表記。小数は 3 桁に丸める（文化に依らない）。</summary>
    private static string F(double value) => value.ToString("F3", CultureInfo.InvariantCulture);
}
