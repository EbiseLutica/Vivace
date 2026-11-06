using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Vivace.Core.Engine;

/// <summary>
/// レーン（D1 1.5）。同じレーンの仕事は順番に、違うレーンの仕事は並列に動く。
/// レーンの鍵は "decide:{ペルソナ}"、"plan:{ペルソナ}"、"world:{ワールド}"。
/// 仕事は状態の写しと now だけを受け取り、結果はエンジンのコマンドのキューに返す。
/// </summary>
public sealed class Lanes
{
    private readonly ConcurrentDictionary<string, Channel<Func<Task>>> _lanes = new(StringComparer.Ordinal);

    /// <summary>レーンの末尾に仕事を積む。仕事は例外を投げないこと（エンジンが包む）。</summary>
    /// <param name="lane">レーンの鍵。</param>
    /// <param name="work">仕事。</param>
    public void Post(string lane, Func<Task> work)
    {
        var channel = _lanes.GetOrAdd(lane, laneKey =>
        {
            var created = Channel.CreateUnbounded<Func<Task>>();
            Task.Run(() => RunAsync(created));
            return created;
        });
        if (!channel.Writer.TryWrite(work))
        {
            throw new InvalidOperationException($"レーン {lane} が止まっていて、仕事を受けられない");
        }
    }

    /// <summary>これ以上の仕事を受けず、レーンを終わらせる。</summary>
    public void Complete()
    {
        foreach (var channel in _lanes.Values)
        {
            channel.Writer.TryComplete();
        }
    }

    private static async Task RunAsync(Channel<Func<Task>> channel)
    {
        await foreach (var work in channel.Reader.ReadAllAsync())
        {
            await work();
        }
    }
}
