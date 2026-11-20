using Vivace.Core.Events;
using Vivace.Core.Storage;

namespace Vivace.Tests.Support;

/// <summary>
/// 保存先の前に挟み、コミットの回数と性質を記録する。stopAfter 回目のコミットが済んだあとで <see cref="CommitStopped"/> を投げ、停止を模擬する。
/// </summary>
internal sealed class CommitProbe(IStorage inner, int? stopAfter) : IStorage
{
    /// <summary>ハンドラを起こすイベントタイプ（Decider、Planner、効果のハンドラの起動元）。実装の行の状態とは別に、種類で決める。</summary>
    private static readonly HashSet<string> TriggerTypes = new(StringComparer.Ordinal)
    {
        EventTypes.DecisionRequested,
        EventTypes.PersonaCreated,
        EventTypes.BlockStarted,
        EventTypes.PlanDue,
        EventTypes.ActionStarted,
    };

    /// <summary>積んだイベントのタイプ（連番から引く）。</summary>
    private readonly Dictionary<long, string> _types = [];

    /// <summary>コミットごとの性質（コミットの順）。</summary>
    private readonly List<Shape> _shapes = [];

    /// <summary>コミットごとの性質（コミットの順）。</summary>
    public IReadOnlyList<Shape> Shapes => _shapes;

    /// <summary>コミットを中の保存先に書き、性質を記録する。停止の回なら、そのあとで投げる。</summary>
    public async Task CommitAsync(StorageBatch batch, CancellationToken ct)
    {
        await inner.CommitAsync(batch, ct);
        foreach (var ev in batch.Enqueued)
        {
            _types[ev.Seq] = ev.EventType;
        }

        var trigger = batch.Updates.Any(update => update.ApplyNo is not null && TriggerTypes.Contains(_types[update.Seq]));
        _shapes.Add(new Shape(trigger, batch.State.Personas.Values.Any(persona => persona.Outbound.Count > 0)));
        if (_shapes.Count == stopAfter)
        {
            throw new CommitStopped();
        }
    }

    /// <summary>中の保存先から読む。</summary>
    public Task<RecoveredRecords> LoadAsync(CancellationToken ct) => inner.LoadAsync(ct);

    /// <summary>中の保存先を閉じる。</summary>
    public ValueTask DisposeAsync() => inner.DisposeAsync();

    /// <summary>1回のコミットの性質。</summary>
    /// <param name="Trigger">ハンドラを起こすイベントを適用したコミットか（その後、出力を待つ間に止まる）。</param>
    /// <param name="InFlight">未送信の操作が残っているか（配信の途中。ここでは止めない）。</param>
    public sealed record Shape(bool Trigger, bool InFlight);
}
