using Vivace.Core.Decision;
using Vivace.Core.Definitions;
using Vivace.Core.Domain;
using Vivace.Core;
using Vivace.Core.Events;
using Vivace.Core.Randomness;
using Vivace.Core.Time;

namespace Vivace.Testing;

/// <summary>
/// シナリオの実行。テスト用の小さな町に、2人のペルソナを仮想時計で数日回す。
/// 外に出さず、メモリの保存先を使う。
/// </summary>
public static class Scenario
{
    /// <summary>シナリオの開始。2026-11-01 の 23:00（現地、+09:00）。2人とも、23:30 前後の睡眠の少し前に生まれる。</summary>
    public static readonly DateTimeOffset Start = new(2026, 11, 1, 23, 0, 0, TimeSpan.FromHours(9));

    /// <summary>既定のワールドの種。</summary>
    public const ulong WorldSeed = 20261101;

    /// <summary>
    /// シナリオの初期状態を入れたエンジンを作る（まだ回さない）。呼び出し側が破棄する。
    /// </summary>
    /// <param name="seed">ワールドの種。</param>
    /// <param name="decider">決める側。省略すると効用による決定。</param>
    /// <param name="definitions">定義。省略するとテスト用の町。</param>
    public static SimEngine Create(ulong seed = WorldSeed, IDecider? decider = null, DefinitionPack? definitions = null)
    {
        var loaded = definitions ?? TestWorld.LoadOrThrow();
        var clock = new VirtualClock(Start);
        var engine = SimEngine.Create(loaded, new InMemoryStorage(), new SplitMixRandomSource(), clock, decider, TestHolidays.Instance);
        engine.Inject(EventDraft.WorldCreated(Start, TestWorld.DefId, seed), Origin.Test);
        engine.Inject(EventDraft.PersonaCreated(Start, new PersonaDefId("kaori"), new PlaceId("kaori_room")), Origin.Test);
        engine.Inject(EventDraft.PersonaCreated(Start, new PersonaDefId("shiori"), new PlaceId("home_living")), Origin.Test);
        return engine;
    }

    /// <summary>
    /// シナリオを days 日回し、エンジンを返す。呼び出し側が破棄する。
    /// </summary>
    /// <param name="days">回す日数。</param>
    /// <param name="seed">ワールドの種。</param>
    public static async Task<SimEngine> RunAsync(int days, ulong seed = WorldSeed)
    {
        var engine = Create(seed);
        await engine.RunUntilAsync(Start.AddDays(days), CancellationToken.None);
        return engine;
    }
}
