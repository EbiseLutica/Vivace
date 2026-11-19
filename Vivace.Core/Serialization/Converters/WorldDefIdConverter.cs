using Vivace.Core.Domain;

namespace Vivace.Core.Serialization.Converters;

/// <summary>WorldDefId を文字列の JSON にする。</summary>
public sealed class WorldDefIdConverter : EntityIdConverter<WorldDefId>
{
    /// <inheritdoc />
    protected override WorldDefId Create(string value) => new(value);
}
