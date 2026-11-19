using System.Globalization;
using Vivace.Core;
using Vivace.Core.Channels;
using Vivace.Core.Domain;
using Vivace.Core.Time;

namespace Vivace.Cli;

/// <summary>投稿を標準出力に書くチャンネル（種類は console）。流れる出力の行と見分けられるよう、送信の行の形にする。</summary>
public sealed class ConsoleChannel(DisplayNames names, IReadOnlyDictionary<WorldId, TimeSpan> offsets, TextWriter output) : IChannel
{
    /// <summary>チャンネルの種類。</summary>
    public const string ChannelKind = "console";

    /// <inheritdoc />
    public string Kind => ChannelKind;

    /// <inheritdoc />
    public Task StartAsync(VivaceEngine engine, CancellationToken ct) => Task.CompletedTask;

    /// <summary>投稿の本文を、ペルソナの表示名と時刻つきで1行に書く。送ったものとして返す。</summary>
    /// <param name="request">送る操作。本文は中身の "body"。</param>
    /// <param name="ct">キャンセル（使わない）。</param>
    public Task<SendResult> SendAsync(OutboundRequest request, CancellationToken ct)
    {
        var body = request.Payload.TryGetProperty("body", out var text) ? text.GetString() ?? string.Empty : string.Empty;
        var offset = names.WorldOf(request.Persona) is { } world && offsets.TryGetValue(world, out var found) ? found : TimeSpan.Zero;
        var (date, time) = LocalTimeHelper.ToLocal(request.At, offset);
        var line = string.Create(CultureInfo.InvariantCulture, $"{date:MM-dd} {time:HH:mm} [送信 {ChannelKind}] {names.Persona(request.Persona)}: {body}");
        output.WriteLine(line);
        return Task.FromResult<SendResult>(new SendResult.Sent($"{ChannelKind}-{request.Seq}"));
    }
}
