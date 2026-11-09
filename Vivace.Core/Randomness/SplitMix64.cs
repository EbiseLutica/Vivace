using System.Buffers.Binary;

namespace Vivace.Core.Randomness;

/// <summary>
/// SplitMix64 の混ぜ関数と、i 番目の値の直接計算。
/// </summary>
public static class SplitMix64
{
    /// <summary>黄金比に基づく増分。</summary>
    public const ulong Gamma = 0x9E3779B97F4A7C15;

    /// <summary>SplitMix64 の混ぜ関数。</summary>
    /// <param name="z">混ぜる値。</param>
    public static ulong Mix(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
        return z ^ (z >> 31);
    }

    /// <summary>
    /// シード s の i 番目（0 から数える）の値を、i から直接計算する。
    /// </summary>
    /// <param name="seed">シード。</param>
    /// <param name="index">番号。</param>
    public static ulong ValueAt(ulong seed, ulong index) => Mix(seed + (index + 1) * Gamma);
}
