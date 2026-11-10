namespace Vivace.Core.Definitions.Toml;

/// <summary>読んだ時間の長さ（固定か範囲か）。</summary>
/// <param name="Min">最短。</param>
/// <param name="Max">最長。</param>
/// <param name="IsRange">範囲で書いたかどうか。</param>
internal readonly record struct ActionDurationReadResult(TimeSpan Min, TimeSpan Max, bool IsRange)
{
    /// <summary>行動の定義の所要時間にする。</summary>
    public ActionDuration ToDefinition() => new(Min, Max, IsRange);
}
