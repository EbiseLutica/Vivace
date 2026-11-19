using Vivace.Core;
using Vivace.Core.Channels;

namespace Vivace.Testing;

/// <summary>何も送らないチャンネル。送ったものとして結果を返す。</summary>
public sealed class NullChannel(string kind) : IChannel
{
    /// <inheritdoc />
    public string Kind { get; } = kind;

    /// <inheritdoc />
    public Task StartAsync(VivaceEngine engine, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<SendResult> SendAsync(OutboundRequest request, CancellationToken ct) =>
        Task.FromResult<SendResult>(new SendResult.Sent($"null-{request.Seq}"));
}
