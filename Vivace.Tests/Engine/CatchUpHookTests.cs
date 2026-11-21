using Vivace.Core;
using Vivace.Core.Decision;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Randomness;
using Vivace.Core.Time;
using Vivace.Testing;
using Vivace.Tests.Storage;
using Vivace.Tests.Support;

namespace Vivace.Tests.Engine;

/// <summary>
/// 追いつきの最中の投入と時計の扱い。追いつきの最中に届いた投入は保留し、追いつきが終わってから、いまの時計で処理する。
/// 追いつきが終わったら、時計は元に戻り、待ちは元の時計で起きる。
/// </summary>
public class CatchUpHookTests
{
    /// <summary>止めるまでの時刻。</summary>
    private static readonly DateTimeOffset StopAt = Scenario.Start.AddDays(1);

    /// <summary>いまに着く時刻（追いつきの target の初めの読み）。</summary>
    private static readonly DateTimeOffset Now = Scenario.Start.AddDays(2);

    /// <summary>
    /// 追いつきの最中の投入は、追いつきの後に、いまの時計の時刻で処理される（origin は投入のまま、catchup にならない）。
    /// 追いつきが終わったら時計は元に戻り、後の待ちは元の時計で起きる。
    /// </summary>
    [Fact]
    public async Task InjectionDuringCatchUpIsDeferredAndStampedWithTheLiveClock()
    {
        var ct = TestContext.Current.CancellationToken;
        using var temp = new TempDatabase();
        await CatchUpTests.FirstRunAsync(temp.FilePath, StopAt, ct);

        var live = new ProbeClock(Now, TimeSpan.FromMinutes(1));
        SimEngine? engine = null;
        Task<InjectResult>? injection = null;
        var hook = new HookDecider(context =>
        {
            if (injection is null && context.CatchingUp)
            {
                injection = engine!.InjectAsync(InjectRequest.WorldCreated(TestWorld.DefId, 7, Origin.Test), ct);
            }
        });
        engine = await CreateAsync(temp.FilePath, live, hook, ct);
        await using (engine)
        {
            await engine.RecoverAsync(ct);
            Assert.NotNull(injection);
            var result = await injection.WaitAsync(TimeSpan.FromSeconds(30), ct);
            Assert.True(result.Accepted, result.Reason);

            await engine.RunUntilAsync(Now.AddHours(2), ct);

            var logged = Assert.Single(engine.Log, entry => entry.Event.EventType == EventTypes.WorldCreated && entry.Event.Origin == Origin.Test);
            Assert.True(logged.Event.Time > live.Reads[1], "保留した投入は、追いつきの target より後の、いまの時計の時刻で処理される");
            Assert.Contains(live.Waits, t => t == Now.AddHours(2));
        }
    }

    /// <summary>エンジンを組む。決める側に <paramref name="decider"/> を使い、保存先は開く。</summary>
    /// <param name="path">保存先。</param>
    /// <param name="clock">時計。</param>
    /// <param name="decider">決める側。</param>
    /// <param name="ct">キャンセル。</param>
    private static async Task<SimEngine> CreateAsync(string path, IClock clock, IDecider decider, CancellationToken ct)
    {
        var database = await Vivace.Storage.Sqlite.SqliteDatabase.OpenAsync(path, ct: ct);
        return SimEngine.Create(TestWorld.LoadOrThrow(), new Vivace.Storage.Sqlite.SqliteStorage(database), new SplitMixRandomSource(), clock, decider, TestHolidays.Instance, null, []);
    }

    /// <summary>読むたびに時刻が進み、待ちを記録する時計。読んだ時刻を <see cref="Reads"/> に残す。</summary>
    private sealed class ProbeClock(DateTimeOffset start, TimeSpan step) : IClock
    {
        /// <summary>読み取りのロック。</summary>
        private readonly Lock _gate = new();

        /// <summary>いまの時刻。</summary>
        private DateTimeOffset _now = start;

        /// <summary>読んだ時刻（読んだ順）。</summary>
        public List<DateTimeOffset> Reads { get; } = [];

        /// <summary>待った先の時刻（待った順）。</summary>
        public List<DateTimeOffset> Waits { get; } = [];

        /// <inheritdoc />
        public DateTimeOffset UtcNow
        {
            get
            {
                lock (_gate)
                {
                    Reads.Add(_now);
                    _now += step;
                    return Reads[^1];
                }
            }
        }

        /// <inheritdoc />
        public ValueTask WaitUntilAsync(DateTimeOffset t, CancellationToken ct)
        {
            lock (_gate)
            {
                Waits.Add(t);
                if (t > _now)
                {
                    _now = t;
                }
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>決め方の差し込み。追いつきの最中の決定で、1度だけ <paramref name="onCatchUp"/> を呼ぶ。決めるのは UtilityDecider。</summary>
    private sealed class HookDecider(Action<DecisionContext> onCatchUp) : IDecider
    {
        /// <summary>決める本体。</summary>
        private readonly UtilityDecider _inner = new();

        /// <inheritdoc />
        public DeciderOutput Decide(DecisionContext context)
        {
            onCatchUp(context);
            return _inner.Decide(context);
        }
    }
}
