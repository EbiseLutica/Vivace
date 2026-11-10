using System.Runtime.ExceptionServices;
using System.Threading.Channels;

namespace Vivace.Core.Engine;

/// <summary>
/// 決める側の仕事の実行の列。同じレーンの仕事は順番に、違うレーンの仕事は並列に動く。鍵は "decide:{ペルソナ}"、"plan:{ペルソナ}"、"world:{ワールド}"。
/// 仕事が例外を投げたレーンは閉じ、残りの仕事を実行しない。
/// </summary>
public sealed class Lanes
{
    /// <summary>レーンの作成と、終わりの印を守る錠。</summary>
    private readonly object _gate = new();
    /// <summary>鍵ごとのレーン。</summary>
    private readonly Dictionary<string, Lane> _lanes = new(StringComparer.Ordinal);
    /// <summary>CompleteAsync を呼んだか。</summary>
    private bool _completed;

    /// <summary>レーンの末尾に仕事を積む。閉じたレーンと、終わらせた後の Lanes には積めない。</summary>
    /// <param name="lane">レーンの鍵。</param>
    /// <param name="work">仕事。</param>
    /// <exception cref="InvalidOperationException">レーンが閉じているか、終わらせた後に新しいレーンへ積もうとした。仕事の例外で閉じたときは、その例外を内側に持つ。</exception>
    public void Post(string lane, Func<Task> work)
    {
        lock (_gate)
        {
            if (_lanes.TryGetValue(lane, out var target))
            {
                if (!target.Jobs.Writer.TryWrite(work))
                {
                    throw new InvalidOperationException($"レーン {lane} が止まっていて、仕事を受けられない", target.Fault);
                }

                return;
            }

            if (_completed)
            {
                throw new InvalidOperationException($"レーンは終わっていて、レーン {lane} を作れない");
            }

            target = new Lane();
            _lanes.Add(lane, target);
            target.Jobs.Writer.TryWrite(work);
        }
    }

    /// <summary>
    /// これ以上の仕事を受けず、積んである仕事が終わるまで待つ。
    /// 仕事の例外で閉じたレーンがあれば、鍵の序数の順で最初のレーンの例外を投げる。
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

    /// <summary>1本のレーン。仕事の列と、それを順に実行するループを持つ。</summary>
    private sealed class Lane
    {
        /// <summary>レーンを閉じた仕事の例外。</summary>
        private volatile Exception? _fault;

        /// <summary>仕事の列を作り、読み手のループを起動する。</summary>
        public Lane()
        {
            Loop = Task.Run(RunAsync);
        }

        /// <summary>仕事の列。</summary>
        public Channel<Func<Task>> Jobs { get; } = Channel.CreateUnbounded<Func<Task>>();

        /// <summary>読み手のループ。仕事が例外を投げると、その例外で失敗する。</summary>
        public Task Loop { get; }

        /// <summary>レーンを閉じた仕事の例外。閉じていなければ null。</summary>
        public Exception? Fault => _fault;

        /// <summary>仕事を順番に実行する。仕事が例外を投げたら、レーンを閉じて例外を投げ直す。</summary>
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
                    throw;
                }
            }
        }
    }
}
