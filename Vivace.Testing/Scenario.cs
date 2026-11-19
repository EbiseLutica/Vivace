using Vivace.Core.Channels;
using Vivace.Core.Decision;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Domain;
using Vivace.Core;
using Vivace.Core.Events;
using Vivace.Core.Llm;
using Vivace.Core.Randomness;
using Vivace.Core.Storage;
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

    /// <summary>既定のワールドのシード。</summary>
    public const ulong WorldSeed = 20261101;

    /// <summary>既定のチャンネルの種類（テスト用の町のペルソナの channels と同じ）。</summary>
    public const string ChannelKind = "console";

    /// <summary>既定で生成するペルソナ。香織（家は kaori_room）と詩織（家は home_living）。</summary>
    private static readonly (PersonaDefId DefId, PlaceId Home)[] DefaultPersonas =
    [
        (new PersonaDefId("kaori"), new PlaceId("kaori_room")),
        (new PersonaDefId("shiori"), new PlaceId("home_living")),
    ];

    /// <summary>
    /// シナリオの初期状態を入れたエンジンを作る（まだ回さない）。呼び出し側が破棄する。
    /// </summary>
    /// <param name="seed">ワールドのシード。</param>
    /// <param name="decider">決める側。省略すると効用による決定。</param>
    /// <param name="definitions">定義。省略するとテスト用の町。</param>
    /// <param name="personas">生成するペルソナ（定義 ID と家）。省略すると香織と詩織。</param>
    /// <param name="clock">時計。開始の時刻を指していること。省略すると開始の時刻の仮想時計。</param>
    /// <param name="storage">保存先。省略するとメモリの保存先。</param>
    /// <param name="llm">LLM。省略すると、呼ぶと失敗する実装（投稿はスキップになる）。</param>
    /// <param name="channels">送信に使うチャンネル。省略すると、何も送らない console のチャンネル。</param>
    /// <param name="sendTimeout">送信の時間切れ（実時間）。省略すると既定の 30 秒。</param>
    public static async Task<SimEngine> CreateAsync(
        ulong seed = WorldSeed,
        IDecider? decider = null,
        DefinitionPack? definitions = null,
        IReadOnlyList<(PersonaDefId DefId, PlaceId Home)>? personas = null,
        IClock? clock = null,
        IStorage? storage = null,
        ILlmClient? llm = null,
        IReadOnlyList<IChannel>? channels = null,
        TimeSpan? sendTimeout = null)
    {
        var loaded = definitions ?? TestWorld.LoadOrThrow();
        clock ??= new VirtualClock(Start);
        var engine = SimEngine.Create(loaded, storage ?? new InMemoryStorage(), new SplitMixRandomSource(), clock, decider, TestHolidays.Instance, llm, channels ?? [new NullChannel(ChannelKind)], sendTimeout: sendTimeout);
        await InjectOrThrowAsync(engine, InjectRequest.WorldCreated(TestWorld.DefId, seed, Origin.Test, Start));
        foreach (var (defId, home) in personas ?? DefaultPersonas)
        {
            await InjectOrThrowAsync(engine, InjectRequest.PersonaCreated(defId, home, Origin.Test, Start));
        }

        return engine;
    }

    /// <summary>
    /// シナリオを days 日回し、エンジンを返す。呼び出し側が破棄する。
    /// </summary>
    /// <param name="days">回す日数。</param>
    /// <param name="seed">ワールドのシード。</param>
    /// <param name="llm">LLM。省略すると、呼ぶと失敗する実装。</param>
    /// <param name="channels">送信に使うチャンネル。省略すると、何も送らない console のチャンネル。</param>
    /// <param name="sendTimeout">送信の時間切れ（実時間）。省略すると既定の 30 秒。</param>
    public static async Task<SimEngine> RunAsync(int days, ulong seed = WorldSeed, ILlmClient? llm = null, IReadOnlyList<IChannel>? channels = null, TimeSpan? sendTimeout = null)
    {
        var engine = await CreateAsync(seed, llm: llm, channels: channels, sendTimeout: sendTimeout);
        await engine.RunUntilAsync(Start.AddDays(days), CancellationToken.None);
        return engine;
    }

    /// <summary>投入し、拒否されたら例外にする。</summary>
    private static async Task InjectOrThrowAsync(SimEngine engine, InjectRequest request)
    {
        var result = await engine.InjectAsync(request, CancellationToken.None);
        if (!result.Accepted)
        {
            throw new InvalidOperationException($"シナリオの投入が拒否されました（{request.EventType}）: {result.Reason}");
        }
    }
}
