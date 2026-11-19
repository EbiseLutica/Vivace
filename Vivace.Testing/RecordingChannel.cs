using Vivace.Core;
using Vivace.Core.Channels;

namespace Vivace.Testing;

/// <summary>
/// 送った操作を記録するチャンネル。結果は、省略すると送れたもの。失敗させるときは respond で作る。
/// </summary>
public sealed class RecordingChannel(string kind, Func<OutboundRequest, SendResult>? respond = null) : IChannel
{
    /// <summary>送った操作を記録する列を守るロック。</summary>
    private readonly Lock _gate = new();

    /// <summary>送った操作（送られた順）。</summary>
    private readonly List<OutboundRequest> _sent = [];

    /// <inheritdoc />
    public string Kind { get; } = kind;

    /// <summary>送った操作（送られた順）。</summary>
    public IReadOnlyList<OutboundRequest> Sent
    {
        get
        {
            lock (_gate)
            {
                return _sent.ToList();
            }
        }
    }

    /// <inheritdoc />
    public Task StartAsync(VivaceEngine engine, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<SendResult> SendAsync(OutboundRequest request, CancellationToken ct)
    {
        lock (_gate)
        {
            _sent.Add(request);
        }

        var result = respond is null ? new SendResult.Sent($"rec-{request.Seq}") : respond(request);
        return Task.FromResult(result);
    }
}
