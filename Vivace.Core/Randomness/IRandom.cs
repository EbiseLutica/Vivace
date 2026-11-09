namespace Vivace.Core.Randomness;

/// <summary>
/// 1本の乱数の列。位置（引いた回数）だけを状態として保存すれば、同じ列を再現できる（D5 5.9）。
/// </summary>
public interface IRandom
{
    /// <summary>引いた回数。次に引く値の番号でもある。</summary>
    ulong Position { get; }

    /// <summary>64 ビットの値を1つ引く。</summary>
    ulong NextUInt64();

    /// <summary>[0, 1) の実数を1つ引く。</summary>
    double NextDouble();

    /// <summary>[minInclusive, maxExclusive) の整数を、偏りなく1つ引く。</summary>
    /// <param name="minInclusive">下限（含む）。</param>
    /// <param name="maxExclusive">上限（含まない）。</param>
    int NextInt(int minInclusive, int maxExclusive);
}
