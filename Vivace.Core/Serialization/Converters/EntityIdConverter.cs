using System.Text.Json;
using System.Text.Json.Serialization;
using Vivace.Core.Domain;

namespace Vivace.Core.Serialization.Converters;

/// <summary>ID の型を、文字列の JSON として読み書きする。</summary>
/// <typeparam name="T">ID の型。</typeparam>
public abstract class EntityIdConverter<T> : JsonConverter<T>
    where T : struct, IEntityId
{
    /// <summary>文字列から ID を作る。</summary>
    /// <param name="value">ID の文字列。</param>
    protected abstract T Create(string value);

    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Create(reader.GetString() ?? throw new JsonException("ID が null です。"));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
