using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using Vivace.Core.Domain;
using Vivace.Core.Storage;

namespace Vivace.Testing;

/// <summary>イベントの列を、比較できる決まった形の文字に直す。</summary>
public static class EventText
{
    /// <summary>適用されたイベントの1行を返す。捨てたものと却下したものは含めない（事実だけ）。</summary>
    /// <param name="logged">適用の記録。</param>
    public static string Line(LoggedEvent logged)
    {
        var ev = logged.Event;
        var builder = new StringBuilder();
        builder.Append(ev.Seq.ToString(CultureInfo.InvariantCulture)).Append(' ');
        builder.Append(ev.Time.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)).Append(" ");
        builder.Append(ev.EventType).Append(" world=").Append(ev.WorldId?.Value ?? "-");
        builder.Append(" persona=").Append(ev.PersonaId?.Value ?? "-");
        builder.Append(" origin=").Append(ev.Origin);
        builder.Append(" cause=").Append(ev.CauseSeq?.ToString(CultureInfo.InvariantCulture) ?? "-");
        builder.Append(" batch=").Append(ev.BatchSeq?.ToString(CultureInfo.InvariantCulture) ?? "-");
        builder.Append(" apply=").Append(logged.ApplyNo?.ToString(CultureInfo.InvariantCulture) ?? "-");
        builder.Append(' ');
        Write(builder, ev.Payload);
        return builder.ToString();
    }

    /// <summary>適用されたイベントを、適用された順に、1行ずつ並べる。末尾に改行。</summary>
    /// <param name="log">適用の記録の列。</param>
    public static string Lines(IEnumerable<LoggedEvent> log)
    {
        var builder = new StringBuilder();
        foreach (var logged in log.Where(entry => entry.Status == EventStatus.Applied))
        {
            builder.Append(Line(logged)).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>値を文字にして足す。</summary>
    private static void Write(StringBuilder builder, object? value)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                return;
            case string text:
                builder.Append('"').Append(text).Append('"');
                return;
            case IEntityId id:
                builder.Append('"').Append(id.Value).Append('"');
                return;
            case bool flag:
                builder.Append(flag ? "true" : "false");
                return;
            case double number:
                builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
                return;
            case float number:
                builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
                return;
            case IFormattable formattable when value is int or long or ulong or uint or short or byte:
                builder.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                return;
            case Enum enumValue:
                builder.Append(enumValue.ToString());
                return;
            case DateTimeOffset time:
                builder.Append(time.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
                return;
            case DateOnly date:
                builder.Append(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                return;
            case TimeOnly clock:
                builder.Append(clock.ToString("HH:mm", CultureInfo.InvariantCulture));
                return;
            case TimeSpan span:
                builder.Append(span.ToString("c", CultureInfo.InvariantCulture));
                return;
            case IEnumerable sequence:
                WriteSequence(builder, sequence, IsUnordered(value.GetType()));
                return;
            default:
                WriteObject(builder, value);
                return;
        }
    }

    /// <summary>順序を持たない集まり（辞書、集合）は、書いた文字の序数順に並べる。</summary>
    private static bool IsUnordered(Type type) =>
        type.IsGenericType
        && (type.Name.StartsWith("Dictionary", StringComparison.Ordinal)
            || type.Name.StartsWith("ImmutableDictionary", StringComparison.Ordinal)
            || type.Name.StartsWith("HashSet", StringComparison.Ordinal)
            || type.Name.StartsWith("ImmutableHashSet", StringComparison.Ordinal));

    /// <summary>列を文字にして足す。順序のない列は整列する。</summary>
    private static void WriteSequence(StringBuilder builder, IEnumerable sequence, bool unordered)
    {
        var items = new List<string>();
        foreach (var item in sequence)
        {
            var itemText = new StringBuilder();
            Write(itemText, item);
            items.Add(itemText.ToString());
        }

        if (unordered)
        {
            items.Sort(StringComparer.Ordinal);
        }

        builder.Append('[').Append(string.Join(", ", items)).Append(']');
    }

    /// <summary>オブジェクトのプロパティを、名前の順に文字にして足す。</summary>
    private static void WriteObject(StringBuilder builder, object value)
    {
        var type = value.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToList();
        if (properties.Count == 0)
        {
            builder.Append(value);
            return;
        }

        builder.Append(type.Name).Append('{');
        var first = true;
        foreach (var property in properties)
        {
            if (!first)
            {
                builder.Append(", ");
            }

            first = false;
            builder.Append(property.Name).Append(": ");
            Write(builder, property.GetValue(value));
        }

        builder.Append('}');
    }

}
