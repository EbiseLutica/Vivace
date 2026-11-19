using Vivace.Core.Domain;

namespace Vivace.Core.Serialization.Converters;

/// <summary>PersonaId を文字列の JSON にする。</summary>
public sealed class PersonaIdConverter : EntityIdConverter<PersonaId>
{
    /// <inheritdoc />
    protected override PersonaId Create(string value) => new(value);
}
