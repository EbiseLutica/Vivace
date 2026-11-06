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

/// <summary>
/// 乱数の列を開く口（ポート）。持ち主と用途ごとに独立した列になる（D5 5.9）。
/// </summary>
public interface IRandomSource
{
    /// <summary>列を開く。position から引き始める。</summary>
    /// <param name="worldSeed">ワールドの種。</param>
    /// <param name="owner">持ち主（ペルソナのインスタンス ID。出来事の抽選は "world"）。</param>
    /// <param name="purpose">用途（"decide"、"sns"。出来事の抽選は "incident/{現地の日付}"）。</param>
    /// <param name="position">引き始める位置（引いた回数）。</param>
    IRandom Open(ulong worldSeed, string owner, string purpose, ulong position);
}
