namespace Vivace.Core.Domain;

/// <summary>
/// ペルソナの定義の ID（例: "kaori"）。定義を引く鍵で、インスタンスの ID（<see cref="PersonaId"/>）とは型を分ける。
/// </summary>
public readonly record struct PersonaDefId(string Value) : IEntityId
{
    /// <summary>ID の文字列を返す。</summary>
    public override string ToString() => Value;
}
