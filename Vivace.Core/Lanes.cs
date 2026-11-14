using System.Runtime.ExceptionServices;
using System.Threading.Channels;

namespace Vivace.Core;

/// <summary>
/// 決める側のジョブキュー。同じレーンのジョブは順番に、違うレーンのジョブは並列に動く。キーは "decide:{ペルソナ}"、"plan:{ペルソナ}"、"world:{ワールド}"。
/// ジョブが例外を投げたレーンは閉じ、残りのジョブを実行しない。
/// </summary>
public sealed class Lanes
{
    /// <summary>レーンの作成と、終了のフラグを守るロック。</summary>
    private readonly Lock _gate = new();
    /// <summary>キーごとのレーン。</summary>
    private readonly Dictionary<string, Lane> _lanes = new(StringComparer.Ordinal);
    /// <summary>CompleteAsync を呼んだか。</summary>
    private bool _completed;
    /// <summary>最初にレーンを閉じたジョブの例外を伝える TaskCompletionSource。</summary>
    private readonly TaskCompletionSource<Exception> _failure = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>どれかのレーンでジョブが初めて例外を投げたときに、その例外を結果として完了する。失敗がなければ完了しない。</summary>
    public Task<Exception> Failure => _failure.Task;

    /// <summary>レーンの末尾にジョブを積む。閉じたレーンと、終わらせた後の Lanes には積めない。</summary>
    /// <param name="lane">レーンのキー。</param>
    /// <param name="work">ジョブ。</param>
    /// <exception cref="InvalidOperationException">レーンが閉じているか、終わらせた後に新しいレーンへ積もうとした。ジョブの例外で閉じたときは、その例外を内側に持つ。</exception>
    public void Post(string lane, Func<Task> work)
    {
        lock (_gate)
        {
            if (_lanes.TryGetValue(lane, out var target))
            {
                if (!target.Jobs.Writer.TryWrite(work))
                {
                    throw new InvalidOperationException($"レーン {lane} が止まっていて、ジョブを受けられません。", target.Fault);
                }

                return;
            }

            if (_completed)
            {
                throw new InvalidOperationException($"レーンは終わっていて、レーン {lane} を作れません。");
            }

            target = new Lane(ex => _failure.TrySetResult(ex));
            _lanes.Add(lane, target);
            target.Jobs.Writer.TryWrite(work);
        }
    }

    /// <summary>
    /// これ以上のジョブを受けず、積んであるジョブが終わるまで待つ。
    /// ジョブの例外で閉じたレーンがあれば、キーの序数の順で最初のレーンの例外を投げる。
    /// </summary>
    public async Task CompleteAsync()
    {
        List<Lane> lanes;
        lock (_gate)
        {
            _completed = true;
            lanes = _lanes.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value).ToList();
            foreach (var lane in lanes)
            {
                lane.Jobs.Writer.TryComplete();
            }
        }

        await Task.WhenAll(lanes.Select(lane => lane.Loop)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (lanes.Select(lane => lane.Fault).FirstOrDefault(fault => fault is not null) is { } first)
        {
            ExceptionDispatchInfo.Throw(first);
        }
    }

    /// <summary>1本のレーン。ジョブキューと、それを順に実行するループを持つ。</summary>
    private sealed class Lane
    {
        /// <summary>レーンを閉じたジョブの例外。</summary>
        private volatile Exception? _fault;

        /// <summary>レーンを閉じたことの知らせ先。</summary>
        private readonly Action<Exception> _onFault;

        /// <summary>ジョブキューを作り、コンシューマーのループを起動する。</summary>
        /// <param name="onFault">ジョブの例外でレーンを閉じたときに、その例外を受け取る。</param>
        public Lane(Action<Exception> onFault)
        {
            _onFault = onFault;
            Loop = Task.Run(RunAsync);
        }

        /// <summary>ジョブキュー。</summary>
        public Channel<Func<Task>> Jobs { get; } = Channel.CreateUnbounded<Func<Task>>();

        /// <summary>コンシューマーのループ。ジョブが例外を投げると、その例外で失敗する。</summary>
        public Task Loop { get; }

        /// <summary>レーンを閉じたジョブの例外。閉じていなければ null。</summary>
        public Exception? Fault => _fault;

        /// <summary>ジョブを順番に実行する。ジョブが例外を投げたら、レーンを閉じて知らせ、例外を投げ直す。</summary>
        private async Task RunAsync()
        {
            await foreach (var work in Jobs.Reader.ReadAllAsync())
            {
                try
                {
                    await work();
                }
                catch (Exception ex)
                {
                    _fault = ex;
                    Jobs.Writer.TryComplete(ex);
                    _onFault(ex);
                    throw;
                }
            }
        }
    }
}
