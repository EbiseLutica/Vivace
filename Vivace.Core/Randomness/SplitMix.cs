using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Vivace.Core.Randomness;

/// <summary>
/// SplitMix64 の混ぜ関数と、i 番目の値の直接計算（D5 5.9、P-38）。
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

/// <summary>
/// 生のシードから始まる SplitMix64 の列。カウンタ方式なので、位置から続きを引ける。
/// </summary>
public sealed class SplitMixRandom : IRandom
{
    private readonly ulong _seed;

    /// <summary>列を作る。</summary>
    /// <param name="seed">シード。</param>
    /// <param name="position">引き始める位置（引いた回数）。</param>
    public SplitMixRandom(ulong seed, ulong position)
    {
        _seed = seed;
        Position = position;
    }

    /// <inheritdoc />
    public ulong Position { get; private set; }

    /// <inheritdoc />
    public ulong NextUInt64()
    {
        var value = SplitMix64.ValueAt(_seed, Position);
        Position++;
        return value;
    }

    /// <inheritdoc />
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>
    /// 乗算と棄却による、偏りのない方法（Lemire）で整数を引く。
    /// </summary>
    /// <param name="minInclusive">下限（含む）。</param>
    /// <param name="maxExclusive">上限（含まない）。下限より大きいこと。</param>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "上限は下限より大きくする必要があります。");
        }

        var range = (ulong)((long)maxExclusive - minInclusive);
        var x = NextUInt64();
        var high = Math.BigMul(x, range, out var low);
        if (low < range)
        {
            var threshold = (0UL - range) % range;
            while (low < threshold)
            {
                x = NextUInt64();
                high = Math.BigMul(x, range, out low);
            }
        }

        return minInclusive + (int)high;
    }
}

/// <summary>
/// 持ち主と用途からシードを導き、SplitMix64 の列を開く（D5 5.9）。
/// </summary>
public sealed class SplitMixRandomSource : IRandomSource
{
    /// <summary>
    /// 列を開く。シードは SHA-256("{ワールドの種}:{持ち主}:{用途}") の先頭 8 バイトを、リトルエンディアンで読んだもの。
    /// </summary>
    /// <param name="worldSeed">ワールドの種。</param>
    /// <param name="owner">持ち主。</param>
    /// <param name="purpose">用途。</param>
    /// <param name="position">引き始める位置。</param>
    public IRandom Open(ulong worldSeed, string owner, string purpose, ulong position) =>
        new SplitMixRandom(DeriveSeed(worldSeed, owner, purpose), position);

    /// <summary>持ち主と用途から、シードを導く。</summary>
    /// <param name="worldSeed">ワールドの種。</param>
    /// <param name="owner">持ち主。</param>
    /// <param name="purpose">用途。</param>
    public static ulong DeriveSeed(ulong worldSeed, string owner, string purpose)
    {
        var bytes = Encoding.UTF8.GetBytes($"{worldSeed}:{owner}:{purpose}");
        var hash = SHA256.HashData(bytes);
        return BinaryPrimitives.ReadUInt64LittleEndian(hash.AsSpan(0, 8));
    }
}
