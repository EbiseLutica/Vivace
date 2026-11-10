using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Vivace.Core.Randomness;

/// <summary>
/// 持ち主と用途からシードを導き、SplitMix64 の列を開く。
/// </summary>
public sealed class SplitMixRandomSource : IRandomSource
{
    /// <summary>列を開く。持ち主と用途が同じなら、同じ列になる。</summary>
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
