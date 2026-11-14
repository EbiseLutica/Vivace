using Vivace.Core.Domain.State;
using Vivace.Core.Storage;

namespace Vivace.Core;

/// <summary>公開 API のエンジン。ビルダーで組み、投入、状態の読み取り、購読、実行を受け持つ。</summary>
public sealed class VivaceEngine : IAsyncDisposable
{
    /// <summary>中で動くシミュレーションのエンジン。</summary>
    private readonly SimEngine _engine;

    /// <summary>組んだシミュレーションのエンジンを包む。</summary>
    /// <param name="engine">シミュレーションのエンジン。</param>
    internal VivaceEngine(SimEngine engine)
    {
        _engine = engine;
    }

    /// <summary>エンジンを組むビルダーを作る。</summary>
    public static VivaceEngineBuilder Create() => new();

    /// <summary>いまの状態（不変）。</summary>
    public SimState View => _engine.View;

    /// <summary>投入の窓口。受け付けたら連番を、拒否したら理由を返す。</summary>
    /// <param name="request">投入の要求。</param>
    /// <param name="ct">結果を待つのをやめるキャンセル。</param>
    public Task<InjectResult> InjectAsync(InjectRequest request, CancellationToken ct) => _engine.InjectAsync(request, ct);

    /// <summary>適用したイベントを、コミットのあとに適用した順に流す。遅れて容量を超えた購読は、例外で切断する。</summary>
    /// <param name="ct">購読をやめるキャンセル。</param>
    public IAsyncEnumerable<LoggedEvent> Subscribe(CancellationToken ct) => _engine.Subscribe(ct);

    /// <summary>時刻 until まで回す。</summary>
    /// <param name="until">進める先の時刻。</param>
    /// <param name="ct">キャンセル。</param>
    public Task RunUntilAsync(DateTimeOffset until, CancellationToken ct) => _engine.RunUntilAsync(until, ct);

    /// <summary>エンジンを止め、保存先を閉じる。</summary>
    public ValueTask DisposeAsync() => _engine.DisposeAsync();
}
