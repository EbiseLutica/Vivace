namespace Vivace.Core.Randomness;

/// <summary>
/// 乱数の列を開くインターフェース（ポート）。持ち主と用途ごとに独立した列になる。
/// </summary>
public interface IRandomSource
{
    /// <summary>列を開く。position から引き始める。</summary>
    /// <param name="worldSeed">ワールドのシード。</param>
    /// <param name="owner">持ち主（ペルソナのインスタンス ID。出来事の抽選は "world"）。</param>
    /// <param name="purpose">用途（"decide"、"sns"。出来事の抽選は "incident/{現地の日付}"）。</param>
    /// <param name="position">引き始める位置（引いた回数）。</param>
    IRandom Open(ulong worldSeed, string owner, string purpose, ulong position);
}
