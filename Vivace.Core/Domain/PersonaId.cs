namespace Vivace.Core.Domain;

/// <summary>
/// ペルソナインスタンスの ID。定義 ID と同じ文字列（例: "kaori"）。1つの定義から生成するインスタンスは1つだけ。
/// </summary>
public readonly record struct PersonaId(string Value) : IEntityId
{
    /// <summary>ID の文字列を返す。</summary>
    public override string ToString() => Value;
}
