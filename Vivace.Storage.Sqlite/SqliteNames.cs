using System.Globalization;
using System.Text;

namespace Vivace.Storage.Sqlite;

/// <summary>
/// 列挙の値と、SQL の列の文字列（小文字の snake_case）の相互変換。書き込みと読み出しは、この変換だけを使う。
/// </summary>
internal static class SqliteNames
{
    /// <summary>列挙の値を、SQL の列の文字列にする（NoMemory は no_memory）。</summary>
    /// <param name="value">列挙の値。</param>
    public static string ToColumn(Enum value)
    {
        var name = value.ToString();
        var builder = new StringBuilder(name.Length + 4);
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (char.IsUpper(character) && index > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLower(character, CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    /// <summary>SQL の列の文字列を、列挙の値にする（no_memory は NoMemory）。知らない値なら例外。</summary>
    /// <param name="text">SQL の列の文字列。</param>
    public static T FromColumn<T>(string text)
        where T : struct, Enum
    {
        var builder = new StringBuilder(text.Length);
        foreach (var part in text.Split('_'))
        {
            if (part.Length == 0)
            {
                continue;
            }

            builder.Append(char.ToUpper(part[0], CultureInfo.InvariantCulture)).Append(part, 1, part.Length - 1);
        }

        return Enum.TryParse<T>(builder.ToString(), ignoreCase: false, out var value) && Enum.IsDefined(value)
            ? value
            : throw new InvalidDataException($"知らない {typeof(T).Name} の値です: {text}");
    }
}
