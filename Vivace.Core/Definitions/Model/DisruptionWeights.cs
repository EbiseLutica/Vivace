namespace Vivace.Core.Definitions;

/// <summary>予定の乱れへの反応の重み。</summary>
/// <param name="Late">遅刻の重み。</param>
/// <param name="Absent">欠席の重み。</param>
/// <param name="Wait">待つの重み。</param>
public sealed record DisruptionWeights(double Late, double Absent, double Wait);
