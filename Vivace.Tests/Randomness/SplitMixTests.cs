using Vivace.Core.Randomness;

namespace Vivace.Tests.Randomness;

/// <summary>乱数の列（SplitMix64、シードの導出、位置からの再開）を既知の値で固定する。</summary>
public class SplitMixTests
{
    /// <summary>SplitMix64 の本体は、シード 1234567 の最初の 5 値が既知の値になる。</summary>
    [Fact]
    public void SeedFromKnownValueProducesKnownSequence()
    {
        var random = new SplitMixRandom(1234567, 0);

        var values = Enumerable.Range(0, 5).Select(_ => random.NextUInt64()).ToArray();

        Assert.Equal(
            [6457827717110365317UL, 3203168211198807973UL, 9817491932198370423UL, 4593380528125082431UL, 16408922859458223821UL],
            values);
        Assert.Equal(5UL, random.Position);
    }

    /// <summary>シードの導出は、SHA-256("{ワールドのシード}:{持ち主}:{用途}") の先頭 8 バイトをリトルエンディアンで読んだ値（既知の値で固定）。</summary>
    [Theory]
    [InlineData(42UL, "taro@tokyo", "decide", 15112297623253681688UL)]
    [InlineData(1234567UL, "world", "incident", 35876087154524244UL)]
    public void DerivesSeedFromSha256OfWorldOwnerAndPurpose(ulong worldSeed, string owner, string purpose, ulong expected)
    {
        Assert.Equal(expected, SplitMixRandomSource.DeriveSeed(worldSeed, owner, purpose));
    }

    /// <summary>持ち主や用途が違えば、別の列になる。同じ持ち主と用途なら、同じ列になる。</summary>
    [Fact]
    public void DifferentOwnersAndPurposesGetDifferentStreams()
    {
        var source = new SplitMixRandomSource();

        var decide = source.Open(42, "taro@tokyo", "decide", 0).NextUInt64();
        var sameAgain = source.Open(42, "taro@tokyo", "decide", 0).NextUInt64();
        var incident = source.Open(42, "taro@tokyo", "incident", 0).NextUInt64();

        Assert.Equal(decide, sameAgain);
        Assert.NotEqual(decide, incident);
    }

    /// <summary>位置から開いた列は、最初から引いて位置まで空回しした列と同じ続きになる（カウンタ方式）。</summary>
    [Fact]
    public void OpeningAtPositionResumesTheSameSequence()
    {
        var source = new SplitMixRandomSource();
        var fromStart = source.Open(42, "taro@tokyo", "decide", 0);
        var all = Enumerable.Range(0, 10).Select(_ => fromStart.NextUInt64()).ToArray();

        var resumed = source.Open(42, "taro@tokyo", "decide", 6);

        Assert.Equal(all[6], resumed.NextUInt64());
        Assert.Equal(all[7], resumed.NextUInt64());
        Assert.Equal(8UL, resumed.Position);
    }

    /// <summary>実数は [0, 1) で、(値 &gt;&gt; 11) × 2^−53 の式どおりになる。</summary>
    [Fact]
    public void NextDoubleFollowsTheFormula()
    {
        var random = new SplitMixRandom(1234567, 0);
        var raw = SplitMix64.ValueAt(1234567, 0);

        var value = random.NextDouble();

        Assert.Equal((raw >> 11) * Math.Pow(2, -53), value);
        Assert.InRange(value, 0.0, 1.0);
    }

    /// <summary>整数は、範囲の中に収まり、同じ列なら同じ値になる。拒否が起きても位置は数えたとおり進む。</summary>
    [Fact]
    public void NextIntStaysInRangeAndIsReproducible()
    {
        var first = new SplitMixRandom(99, 0);
        var second = new SplitMixRandom(99, 0);

        for (var i = 0; i < 200; i++)
        {
            var value = first.NextInt(-5, 7);
            Assert.InRange(value, -5, 6);
            Assert.Equal(value, second.NextInt(-5, 7));
        }

        Assert.Equal(first.Position, second.Position);
        Assert.True(first.Position >= 200UL);
    }

    /// <summary>上限が下限以下なら、整数は引けない。</summary>
    [Fact]
    public void NextIntRejectsEmptyRange()
    {
        var random = new SplitMixRandom(1, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(3, 3));
    }

    /// <summary>シード 99 の NextInt(-5, 7) の既知の値（独立に計算した列）。</summary>
    [Fact]
    public void NextIntMatchesKnownSequence()
    {
        var random = new SplitMixRandom(99, 0);

        var values = Enumerable.Range(0, 8).Select(_ => random.NextInt(-5, 7)).ToArray();

        Assert.Equal(new[] { -2, -5, 5, -4, -3, -3, 3, 3 }, values);
    }

    /// <summary>シード 1234567 の NextDouble の既知の値（独立に計算した列）。</summary>
    [Fact]
    public void NextDoubleMatchesKnownSequence()
    {
        var random = new SplitMixRandom(1234567, 0);

        var values = Enumerable.Range(0, 3).Select(_ => random.NextDouble()).ToArray();

        Assert.Equal(new[] { 0.3500795420214081, 0.17364409667091263, 0.5322073040624192 }, values);
    }
}
