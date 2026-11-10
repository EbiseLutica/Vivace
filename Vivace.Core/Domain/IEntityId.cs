namespace Vivace.Core.Domain;

/// <summary>
/// ID の型の共通のインターフェース。ID の中身の文字列を、型によらず取り出せるようにする。
/// </summary>
public interface IEntityId
{
    /// <summary>ID の中身の文字列。</summary>
    string Value { get; }
}
