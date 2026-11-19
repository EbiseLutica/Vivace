using Vivace.Core;
using Vivace.Core.Channels;

namespace Vivace.Testing;

/// <summary>送信の振る舞いを、渡した関数で決めるチャンネル。起動の回数を数える。</summary>
/// <param name="kind">チャンネルの種類。</param>
/// <param name="send">送信の振る舞い。</param>
public sealed class FakeChannel(string kind, Func<OutboundRequest, CancellationToken, Task<SendResult>> send) : IChannel
{
    /// <summary>起動の回数。</summary>
    private int _starts;

    /// <inheritdoc />
    public string Kind { get; } = kind;

    /// <summary>起動された回数。</summary>
    public int Starts => Volatile.Read(ref _starts);

    /// <inheritdoc />
    public Task StartAsync(VivaceEngine engine, CancellationToken ct)
    {
        Interlocked.Increment(ref _starts);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SendResult> SendAsync(OutboundRequest request, CancellationToken ct) => send(request, ct);
}
