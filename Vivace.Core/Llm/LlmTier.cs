namespace Vivace.Core.Llm;

/// <summary>モデルの段。Main は判断の質が結果に直結するもの、Light は短い分類と変換。段から実際のモデルへの対応は LLM の実装が持つ。</summary>
public enum LlmTier
{
    /// <summary>判断の質が要るもの（文面、計画など）。</summary>
    Main,

    /// <summary>短い分類と変換。</summary>
    Light,
}
