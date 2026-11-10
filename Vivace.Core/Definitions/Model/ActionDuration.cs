namespace Vivace.Core.Definitions;

/// <summary>行動の所要時間。範囲か固定。</summary>
/// <param name="Min">最短。</param>
/// <param name="Max">最長。固定のときは Min と同じ。</param>
/// <param name="IsRange">範囲で書いたかどうか。</param>
public sealed record ActionDuration(TimeSpan Min, TimeSpan Max, bool IsRange);
