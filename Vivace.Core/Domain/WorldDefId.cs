namespace Vivace.Core.Domain;

/// <summary>
/// ワールドの定義の ID（例: "tokyo"）。定義を引くキーで、インスタンスの ID（<see cref="WorldId"/>）とは型を分ける。
/// </summary>
public readonly record struct WorldDefId(string Value) : IEntityId
{
    /// <summary>ID の文字列を返す。</summary>
    public override string ToString() => Value;
}
