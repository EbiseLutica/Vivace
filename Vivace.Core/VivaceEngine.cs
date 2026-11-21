using Vivace.Core.Channels;
using Vivace.Core.Domain.State;
using Vivace.Core.Storage;

namespace Vivace.Core;

/// <summary>公開 API のエンジン。ビルダーで組み、投入、状態の読み取り、購読、実行を受け持つ。</summary>
public sealed class VivaceEngine : IAsyncDisposable
{
    /// <summary>中で動くシミュレーションのエンジン。</summary>
    private readonly SimEngine _engine;

    /// <summary>つないだチャンネル。ドライランでは空。</summary>
    private readonly IReadOnlyList<IChannel> _channels;

    /// <summary>組んだシミュレーションのエンジンと、つないだチャンネルを包む。</summary>
    /// <param name="engine">シミュレーションのエンジン。</param>
    /// <param name="channels">つないだチャンネル。</param>
    internal VivaceEngine(SimEngine engine, IReadOnlyList<IChannel> channels)
    {
        _engine = engine;
        _channels = channels;
    }

    /// <summary>エンジンを組むビルダーを作る。</summary>
    public static VivaceEngineBuilder Create() => new();

    /// <summary>いまの状態（不変）。</summary>
    public SimState View => _engine.View;

    /// <summary>この運転で適用、捨て、却下の結果が決まったイベントの記録（決まった順）。追いつきで進めた分も含む。</summary>
    public IReadOnlyList<LoggedEvent> Log => _engine.Log;

    /// <summary>投入 API。受け付けたら連番を、拒否したら理由を返す。</summary>
    /// <param name="request">投入の要求。</param>
    /// <param name="ct">結果を待つのをやめるキャンセル。</param>
    public Task<InjectResult> InjectAsync(InjectRequest request, CancellationToken ct) => _engine.InjectAsync(request, ct);

    /// <summary>適用したイベントを、コミットのあとに適用した順に流す。遅れて容量を超えた購読は、例外で切断する。</summary>
    /// <param name="ct">購読をやめるキャンセル。</param>
    public IAsyncEnumerable<LoggedEvent> Subscribe(CancellationToken ct) => _engine.Subscribe(ct);

    /// <summary>容量を指定して購読する（テストと CLI の内部用）。</summary>
    /// <param name="capacity">読まれずに溜めておける件数。</param>
    /// <param name="ct">購読をやめるキャンセル。</param>
    internal IAsyncEnumerable<LoggedEvent> Subscribe(int capacity, CancellationToken ct) => _engine.Subscribe(capacity, ct);

    /// <summary>つないだチャンネルを起動する。エンジンを回す前に呼ぶ。</summary>
    /// <param name="ct">キャンセル。</param>
    public async Task StartAsync(CancellationToken ct)
    {
        foreach (var channel in _channels)
        {
            await channel.StartAsync(this, ct);
        }
    }

    /// <summary>
    /// 保存先の記録から状態とキューを戻す。起動の直後に一度だけ呼ぶ（記録がなければ何もしない）。止めていた間が 1 分を超えていれば、追いつく。
    /// </summary>
    /// <param name="ct">キャンセル。</param>
    /// <returns>保存先に記録があったら true。</returns>
    public Task<bool> RecoverAsync(CancellationToken ct) => _engine.RecoverAsync(ct);

    /// <summary>時刻 until まで回す。</summary>
    /// <param name="until">進める先の時刻。</param>
    /// <param name="ct">キャンセル。</param>
    public Task RunUntilAsync(DateTimeOffset until, CancellationToken ct) => _engine.RunUntilAsync(until, ct);

    /// <summary>エンジンを止め、保存先を閉じる。</summary>
    public ValueTask DisposeAsync() => _engine.DisposeAsync();
}
