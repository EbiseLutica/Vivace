using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Vivace.Core.Definitions.Toml;
using Vivace.Core.Domain;

namespace Vivace.Core.Definitions;

/// <summary>
/// 定義を読む。TOML の文字列やフォルダから Definitions を作り、定義の検証を通す。
/// エラーがあれば Definitions は作らない（起動しない、読み直しは古い定義で動き続ける）。
/// </summary>
public static class DefinitionLoader
{
    private const string BuiltinActionsPath = "builtin/actions/core.toml";

    /// <summary>
    /// 定義フォルダを読む。フォルダの下の .toml をすべて読み、組み込みの行動を加える。
    /// </summary>
    /// <param name="directory">定義フォルダ（worlds、personas、actions、incidents を含む）。</param>
    public static DefinitionLoadResult LoadDirectory(string directory)
    {
        var files = Directory.EnumerateFiles(directory, "*.toml", SearchOption.AllDirectories)
            .Select(path => new DefinitionFile(
                Path.GetRelativePath(directory, path).Replace('\\', '/'),
                File.ReadAllText(path)))
            .ToList();
        return LoadFiles(files);
    }

    /// <summary>
    /// 定義の文字列を読む。組み込みの行動（core.*）は、アセンブリに埋め込んだものを加える。
    /// </summary>
    /// <param name="files">ユーザーの定義ファイル。</param>
    public static DefinitionLoadResult LoadFiles(IEnumerable<DefinitionFile> files)
    {
        var issues = new List<DefinitionIssue>();
        var all = files.OrderBy(file => file.Path, StringComparer.Ordinal).Concat(BuiltinFiles()).ToList();

        var worlds = new Dictionary<string, WorldDef>();
        var personas = new Dictionary<string, PersonaDef>();
        var actions = new Dictionary<string, ActionDef>();
        var incidents = new Dictionary<string, IncidentDef>();
        var conditions = new Dictionary<string, ConditionDef>();

        foreach (var file in all)
        {
            var root = TomlTreeBuilder.Build(file.Text, file.Path, issues);
            if (root is null)
            {
                continue;
            }

            switch (FolderOf(file.Path))
            {
                case "worlds":
                    var world = DefinitionMapper.MapWorld(root, issues, file.IsBuiltin);
                    Register(worlds, world.Id.Value, world, world.Source.Location, issues);
                    break;
                case "personas":
                    var persona = DefinitionMapper.MapPersona(root, issues);
                    Register(personas, persona.Id.Value, persona, persona.Source.Location, issues);
                    break;
                case "actions":
                    var reader = new Toml.TableReader(root, issues);
                    foreach (var table in reader.TableArray("action"))
                    {
                        var action = DefinitionMapper.MapAction(table, issues, file.IsBuiltin);
                        Register(actions, action.Id, action, action.Source.Location, issues);
                    }

                    reader.Done();
                    break;
                case "incidents":
                    var incidentReader = new Toml.TableReader(root, issues);
                    foreach (var table in incidentReader.TableArray("incident"))
                    {
                        var incident = DefinitionMapper.MapIncident(table, issues);
                        Register(incidents, incident.Id, incident, incident.Source.Location, issues);
                    }

                    foreach (var table in incidentReader.TableArray("condition"))
                    {
                        var condition = DefinitionMapper.MapCondition(table, issues);
                        Register(conditions, condition.Id, condition, condition.Source.Location, issues);
                    }

                    incidentReader.Done();
                    break;
                default:
                    issues.Add(new DefinitionIssue(
                        IssueSeverity.Error,
                        DefinitionIssueCode.InvalidValue,
                        new SourceLocation(file.Path, 1, 1),
                        "定義のフォルダが知らない種類です（worlds、personas、actions、incidents のどれか）。"));
                    break;
            }
        }

        var definitions = new Definitions(
            worlds.ToImmutableDictionary(pair => new WorldDefId(pair.Key), pair => pair.Value),
            personas.ToImmutableDictionary(pair => new PersonaDefId(pair.Key), pair => pair.Value),
            actions.ToImmutableDictionary(),
            incidents.ToImmutableDictionary(),
            conditions.ToImmutableDictionary(),
            HashOf(all));

        issues.AddRange(DefinitionValidator.Validate(definitions));
        var ordered = issues.OrderBy(issue => issue.Location.Path, StringComparer.Ordinal)
            .ThenBy(issue => issue.Location.Line)
            .ThenBy(issue => issue.Location.Column)
            .ThenBy(issue => issue.Code)
            .ThenBy(issue => issue.Message, StringComparer.Ordinal)
            .ToImmutableArray();
        var hasErrors = ordered.Any(issue => issue.Severity == IssueSeverity.Error);
        return new DefinitionLoadResult(hasErrors ? null : definitions, ordered);
    }

    /// <summary>組み込みの定義ファイル（アセンブリに埋め込んだ core.* の行動）。</summary>
    public static ImmutableArray<DefinitionFile> BuiltinFiles()
    {
        using var stream = typeof(DefinitionLoader).Assembly.GetManifestResourceStream(BuiltinActionsPath)
            ?? throw new InvalidOperationException($"組み込みの定義が見つかりません: {BuiltinActionsPath}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return [new DefinitionFile(BuiltinActionsPath, reader.ReadToEnd(), IsBuiltin: true)];
    }

    /// <summary>相対パスの、ファイルの直前のフォルダ名を返す。定義の種類の判定に使う。</summary>
    private static string FolderOf(string path)
    {
        var parts = path.Split('/');
        return parts.Length >= 2 ? parts[^2] : string.Empty;
    }

    /// <summary>定義を ID で登録する。同じ ID が二度あれば、重複のエラーにする。</summary>
    private static void Register<T>(Dictionary<string, T> map, string id, T value, SourceLocation location, List<DefinitionIssue> issues)
    {
        if (id.Length == 0)
        {
            return;
        }

        if (map.ContainsKey(id))
        {
            issues.Add(new DefinitionIssue(
                IssueSeverity.Error,
                DefinitionIssueCode.DuplicateId,
                location,
                $"同じ ID が2つあります: {id}"));
            return;
        }

        map[id] = value;
    }

    /// <summary>定義ファイルの内容のハッシュを、ファイルの順に依らず求める。</summary>
    private static string HashOf(IEnumerable<DefinitionFile> files)
    {
        var builder = new StringBuilder();
        foreach (var file in files.OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            builder.Append(file.Path).Append('\n').Append(file.Text).Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
