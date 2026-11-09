namespace Vivace.Core.Domain;

/// <summary>送り主。</summary>
/// <param name="UserId">チャンネル上のユーザー ID。</param>
/// <param name="DisplayName">表示名。</param>
/// <param name="Handle">ハンドル名（あれば）。</param>
public sealed record Sender(string UserId, string DisplayName, string? Handle);
