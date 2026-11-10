namespace Vivace.Core.Definitions;

/// <summary>睡眠の効果。</summary>
/// <param name="Consolidate">記憶の整理を起こすか。</param>
/// <param name="Debt">寝不足の判定をするか。</param>
public sealed record SleepEffect(bool Consolidate, bool Debt);
