using System.Buffers.Binary;

namespace Vivace.Core.Randomness;

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

    /// <summary>偏りなく整数を引く。</summary>
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
