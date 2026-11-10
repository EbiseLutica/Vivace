namespace Vivace.Core.Domain;

/// <summary>
/// ペルソナインスタンスの ID。「定義 ID@ワールド ID」の形（例: "kaori@tokyo"）。
/// </summary>
public readonly record struct PersonaId(string Value)
{
    /// <summary>ID の文字列を返す。</summary>
    public override string ToString() => Value;
}
