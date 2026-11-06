using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vivace.Core.Definitions.Toml;
using Vivace.Core.Domain;
using Vivace.Core.Time;

namespace Vivace.Core.Definitions;

/// <summary>
/// TOML の表を、定義の型に写す。型と範囲の検査はここで行い、参照の検査は DefinitionValidator が行う。
/// 問題があっても、既定値で型を作り続けて、全件のエラーを集める。
/// </summary>
internal static partial class DefinitionMapper
{
    private static readonly Dictionary<string, VitalName> VitalKeys = new()
    {
        ["energy"] = VitalName.Energy,
        ["health"] = VitalName.Health,
        ["boredom"] = VitalName.Boredom,
        ["social"] = VitalName.Social,
    };

    private static readonly Dictionary<string, DayOfWeek> DayKeys = new()
    {
        ["mon"] = DayOfWeek.Monday,
        ["tue"] = DayOfWeek.Tuesday,
        ["wed"] = DayOfWeek.Wednesday,
        ["thu"] = DayOfWeek.Thursday,
        ["fri"] = DayOfWeek.Friday,
        ["sat"] = DayOfWeek.Saturday,
        ["sun"] = DayOfWeek.Sunday,
    };

    [GeneratedRegex(@"^[a-z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex UserIdPattern();

    [GeneratedRegex(@"^core\.[a-z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CoreIdPattern();

    /// <summary>ID を読み、名前空間と形式を検査する。</summary>
    /// <param name="r">読む表。</param>
    /// <param name="builtin">組み込みのファイルかどうか。</param>
    public static string ReadId(TableReader r, bool builtin)
    {
        var id = r.RequiredString("id");
        if (id is null)
        {
            return string.Empty;
        }

        if (id.Length == 0)
        {
            r.InvalidAt("id", "ID は空にできません。");
            return string.Empty;
        }

        if (builtin)
        {
            if (!CoreIdPattern().IsMatch(id))
            {
                r.Error(DefinitionIssueCode.Namespace, r.LocationOf("id"), $"組み込みの ID は core. で始めてください: {id}");
            }
        }
        else if (id.Contains('.'))
        {
            r.Error(DefinitionIssueCode.Namespace, r.LocationOf("id"), $"ユーザーの定義は名前空間（.）を使えません: {id}");
        }
        else if (!UserIdPattern().IsMatch(id))
        {
            r.InvalidAt("id", $"ID は英小文字、数字、_ だけで書いてください: {id}");
        }

        return id;
    }

    /// <summary>ワールドの定義を読む。</summary>
    /// <param name="table">ファイルのルートの表。</param>
    /// <param name="issues">問題の集め先。</param>
    /// <param name="builtin">組み込みかどうか。</param>
    public static WorldDef MapWorld(TomlTable table, List<DefinitionIssue> issues, bool builtin)
    {
        var r = new TableReader(table, issues);
        var id = ReadId(r, builtin);
        var name = r.RequiredString("name") ?? string.Empty;
        var lore = r.RequiredString("lore") ?? string.Empty;
        var offset = r.RequiredParsed<TimeSpan>("utc_offset", TimeText.TryParseOffset, "+09:00 の形の時差") ?? TimeSpan.Zero;
        var generated = ReadGeneratedPlaces(r);
        var notExist = r.String("not_exist");

        string? holidays = null;
        var events = ImmutableDictionary.CreateBuilder<MonthDay, string>();
        var calendar = r.Table("calendar");
        if (calendar is not null)
        {
            var cr = new TableReader(calendar, issues);
            holidays = cr.String("holidays");
            var eventTable = cr.Table("events");
            if (eventTable is not null)
            {
                var er = new TableReader(eventTable, issues);
                foreach (var (key, node) in eventTable.Entries)
                {
                    if (!TimeText.TryParseMonthDay(key, out var monthDay))
                    {
                        er.Invalid(node, $"年中行事の日付は \"MM-DD\" で書いてください: {key}");
                        continue;
                    }

                    if (node is TomlScalar { Value: string eventName })
                    {
                        events[monthDay] = eventName;
                    }
                    else
                    {
                        er.Invalid(node, $"年中行事の名前は文字列で書いてください: {key}");
                    }
                }
            }

            cr.Done();
        }

        var terms = ImmutableArray.CreateBuilder<TermDef>();
        foreach (var termTable in r.TableArray("term"))
        {
            terms.Add(MapTerm(termTable, issues));
        }

        var places = ImmutableArray.CreateBuilder<PlaceDef>();
        foreach (var placeTable in r.TableArray("place"))
        {
            places.Add(MapPlace(placeTable, issues));
        }

        r.Done();
        return new WorldDef(
            id,
            name,
            lore,
            offset,
            generated,
            notExist,
            holidays,
            events.ToImmutable(),
            terms.ToImmutable(),
            places.ToImmutable(),
            Src(table));
    }

    /// <summary>ペルソナの定義を読む。</summary>
    /// <param name="table">ファイルのルートの表。</param>
    /// <param name="issues">問題の集め先。</param>
    public static PersonaDef MapPersona(TomlTable table, List<DefinitionIssue> issues)
    {
        var r = new TableReader(table, issues);
        var id = ReadId(r, false);
        var name = r.RequiredString("name") ?? string.Empty;
        var world = r.RequiredString("world") ?? string.Empty;
        var home = r.RequiredString("home") ?? string.Empty;
        var lore = r.RequiredString("lore") ?? string.Empty;
        var examples = r.Strings("examples");
        var channels = r.Strings("channels");
        var items = r.Strings("items");
        var tools = r.Strings("tools");

        var use = ImmutableArray<string>.Empty;
        var prefs = ImmutableDictionary.CreateBuilder<string, Preference>();
        var actions = r.Table("actions");
        if (actions is not null)
        {
            var ar = new TableReader(actions, issues);
            use = ar.Strings("use");
            var prefTable = ar.Table("prefs");
            if (prefTable is not null)
            {
                var pr = new TableReader(prefTable, issues);
                foreach (var (key, node) in prefTable.Entries)
                {
                    var preference = ReadPreference(node, pr, key);
                    if (preference is not null)
                    {
                        prefs[key] = preference;
                    }
                }
            }

            ar.Done();
        }

        var vitals = MapVitals(r);
        var reflexes = MapReflexes(r);
        var disruption = MapDisruption(r);
        var memory = r.Table("memory");
        var halfLife = 1.0;
        if (memory is not null)
        {
            var mr = new TableReader(memory, issues);
            halfLife = mr.Number("half_life") ?? 1.0;
            if (!(halfLife > 0 && double.IsFinite(halfLife)))
            {
                mr.InvalidAt("half_life", "half_life は 0 より大きい数で書いてください。");
            }

            mr.Done();
        }

        var followBack = FollowBackPolicy.Always;
        var sns = r.Table("sns");
        if (sns is not null)
        {
            var sr = new TableReader(sns, issues);
            var policy = sr.String("follow_back");
            followBack = policy switch
            {
                null or "always" => FollowBackPolicy.Always,
                "never" => FollowBackPolicy.Never,
                _ => FollowBackPolicy.Always,
            };
            if (policy is not null && policy is not ("always" or "never"))
            {
                sr.InvalidAt("follow_back", $"follow_back は always か never で書いてください: {policy}");
            }

            sr.Done();
        }

        var channelSettings = ImmutableDictionary.CreateBuilder<string, JsonElement>();
        var channelTable = r.Table("channel");
        if (channelTable is not null)
        {
            var cr = new TableReader(channelTable, issues);
            foreach (var (key, node) in channelTable.Entries)
            {
                if (node is TomlTable settings)
                {
                    channelSettings[key] = settings.ToJson();
                }
                else
                {
                    cr.Invalid(node, $"チャンネルの設定は表で書いてください: {key}");
                }
            }
        }

        var scheduleTable = r.Table("schedule");
        var schedule = scheduleTable is null
            ? new ScheduleDef(ImmutableArray<PeriodDef>.Empty, ImmutableArray<BlockDef>.Empty, ImmutableArray<ExceptionDef>.Empty)
            : MapSchedule(scheduleTable, issues);

        r.Done();
        return new PersonaDef(
            id,
            name,
            world,
            home,
            lore,
            examples,
            channels,
            items,
            tools,
            use,
            prefs.ToImmutable(),
            vitals,
            reflexes,
            disruption,
            halfLife,
            followBack,
            channelSettings.ToImmutable(),
            schedule,
            Src(table));
    }

    /// <summary>行動の定義を読む（[[action]] の1件）。</summary>
    /// <param name="table">行動の表。</param>
    /// <param name="issues">問題の集め先。</param>
    /// <param name="builtin">組み込みかどうか。</param>
    public static ActionDef MapAction(TomlTable table, List<DefinitionIssue> issues, bool builtin)
    {
        var r = new TableReader(table, issues);
        var id = ReadId(r, builtin);
        var display = r.RequiredString("display") ?? string.Empty;
        var description = r.String("description") ?? string.Empty;
        var tags = r.Strings("tags");
        // 所要時間のキーはあるが読めなかったときは、エラーは読みの段階で出ているので、欠落の検査を重ねない。
        var duration = r.Duration("duration")?.ToDefinition()
            ?? (table.ContainsKey("duration") ? new ActionDuration(TimeSpan.Zero, TimeSpan.Zero, false) : null);
        var weight = ReadNonNegative(r, "weight", 1.0);

        var phone = ReadPhone(r, "phone") ?? PhonePolicy.Closed;
        var scheduleOnly = r.Bool("schedule_only") ?? false;

        var requiresTable = r.Table("requires");
        var requires = new ActionRequires(ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, null);
        if (requiresTable is not null)
        {
            var rr = new TableReader(requiresTable, issues);
            string? capability = null;
            var channel = rr.Table("channel");
            if (channel is not null)
            {
                var cr = new TableReader(channel, issues);
                capability = cr.String("capability");
                cr.Done();
            }

            requires = new ActionRequires(rr.Strings("place_tags"), rr.Strings("items"), capability);
            rr.Done();
        }

        var limitsTable = r.Table("limits");
        var limits = new ActionLimits(null, null, null);
        if (limitsTable is not null)
        {
            var lr = new TableReader(limitsTable, issues);
            var cooldown = lr.Span("cooldown");
            var maxPerDay = lr.Integer("max_per_day");
            if (maxPerDay is < 1 or > int.MaxValue)
            {
                lr.InvalidAt("max_per_day", "max_per_day は 1 以上 int の範囲以内で書いてください。");
            }

            limits = new ActionLimits(cooldown, maxPerDay is null or < 1 or > int.MaxValue ? null : (int)maxPerDay.Value, lr.Hours("hours"));
            lr.Done();
        }

        var effects = MapActionEffects(r);
        r.Done();
        return new ActionDef(
            id,
            display,
            description,
            tags,
            duration,
            weight,
            phone,
            scheduleOnly,
            requires,
            limits,
            effects,
            Src(table));
    }

    /// <summary>出来事の定義を読む（[[incident]] の1件）。</summary>
    /// <param name="table">出来事の表。</param>
    /// <param name="issues">問題の集め先。</param>
    public static IncidentDef MapIncident(TomlTable table, List<DefinitionIssue> issues)
    {
        var r = new TableReader(table, issues);
        var id = ReadId(r, false);
        var display = r.RequiredString("display") ?? string.Empty;
        var target = IncidentTarget.World;
        var targetText = r.RequiredString("target");
        switch (targetText)
        {
            case "world":
                target = IncidentTarget.World;
                break;
            case "persona":
                target = IncidentTarget.Persona;
                break;
            case not null:
                r.InvalidAt("target", $"target は world か persona で書いてください: {targetText}");
                break;
        }

        var chance = r.Number("chance");
        if (chance is null && !table.ContainsKey("chance"))
        {
            r.Missing("chance");
        }
        else if (chance is not null && !(chance >= 0 && chance <= 1))
        {
            r.InvalidAt("chance", "chance は 0 から 1 の間の数で書いてください。");
        }

        var hours = r.Hours("hours");
        var duration = r.Duration("duration")?.ToDefinition();
        var cooldown = r.Span("cooldown");
        var effects = MapIncidentEffects(r);
        if (target == IncidentTarget.World && effects.Condition is not null)
        {
            r.InvalidAt("effects", "ワールド向けの出来事は、状態（condition）を付けられません。");
        }
        if (target == IncidentTarget.World && effects.Vitals.Count > 0)
        {
            r.InvalidAt("effects", "ワールド向けの出来事は、状態変数（vital）を変えられません。");
        }
        if (target == IncidentTarget.Persona && (effects.SlowPlace is not null || effects.ClosePlace is not null))
        {
            r.InvalidAt("effects", "ペルソナ向けの出来事は、移動時間や閉鎖（slow_place、close_place）を変えられません。");
        }
        r.Done();
        return new IncidentDef(
            id,
            display,
            target,
            chance ?? 0,
            hours,
            duration,
            cooldown,
            effects,
            Src(table));
    }

    /// <summary>状態の定義を読む（[[condition]] の1件）。</summary>
    /// <param name="table">状態の表。</param>
    /// <param name="issues">問題の集め先。</param>
    public static ConditionDef MapCondition(TomlTable table, List<DefinitionIssue> issues)
    {
        var r = new TableReader(table, issues);
        var id = ReadId(r, false);
        var display = r.RequiredString("display") ?? string.Empty;
        var rates = ReadVitalTable(r, "rate", -1000, 1000);
        var thresholds = ReadVitalTable(r, "threshold", 0, 100);
        if (thresholds.ContainsKey(VitalName.Social))
        {
            r.InvalidAt("threshold", "社交欲求にはしきい値がありません（D5 5.6）。");
        }
        var weights = ImmutableDictionary.CreateBuilder<string, double>();
        var weightTable = r.Table("weight");
        if (weightTable is not null)
        {
            var wr = new TableReader(weightTable, issues);
            foreach (var (key, node) in weightTable.Entries)
            {
                var value = node is TomlScalar { Value: double or long } scalar
                    ? (scalar.Value is long integer ? integer : (double)scalar.Value)
                    : double.NaN;
                if (node is not TomlScalar { Value: double or long })
                {
                    wr.Invalid(node, $"重みは数で書いてください: {key}");
                }
                else if (!(double.IsFinite(value) && value >= 0))
                {
                    wr.Invalid(node, $"重みは 0 以上の有限の数で書いてください: {key}");
                }
                else
                {
                    weights[key] = value;
                }
            }
        }

        var forbid = r.Strings("forbid");
        r.Done();
        return new ConditionDef(
            id,
            display,
            rates,
            weights.ToImmutable(),
            forbid,
            thresholds,
            Src(table));
    }

    private static DefinitionSource Src(TomlTable table)
    {
        var keys = ImmutableDictionary.CreateBuilder<string, SourceLocation>();
        AddKeys(table, string.Empty, keys);
        return new DefinitionSource(table.Location, keys.ToImmutable());
    }

    /// <summary>表の中の各キーの位置を、入れ子は「親.子」の形で集める（配列の表の中には入らない）。</summary>
    private static void AddKeys(TomlTable table, string prefix, ImmutableDictionary<string, SourceLocation>.Builder keys)
    {
        foreach (var (key, node) in table.Entries)
        {
            var path = prefix + key;
            keys[path] = node.Location;
            if (node is TomlTable child)
            {
                AddKeys(child, path + ".", keys);
            }
        }
    }

    private static GeneratedPlacesMode ReadGeneratedPlaces(TableReader r)
    {
        var text = r.String("generated_places");
        return text switch
        {
            null or "auto" => GeneratedPlacesMode.Auto,
            "review" => GeneratedPlacesMode.Review,
            _ => ReportGeneratedPlaces(r, text),
        };
    }

    private static GeneratedPlacesMode ReportGeneratedPlaces(TableReader r, string text)
    {
        r.InvalidAt("generated_places", $"generated_places は auto か review で書いてください: {text}");
        return GeneratedPlacesMode.Auto;
    }

    private static TermDef MapTerm(TomlTable table, List<DefinitionIssue> issues)
    {
        var r = new TableReader(table, issues);
        var from = r.RequiredString("from") ?? string.Empty;
        var verdict = TermVerdict.Pass;
        var verdictText = r.String("verdict");
        switch (verdictText)
        {
            case null or "pass":
                verdict = TermVerdict.Pass;
                break;
            case "convert":
                verdict = TermVerdict.Convert;
                break;
            case "reject":
                verdict = TermVerdict.Reject;
                break;
            default:
                r.InvalidAt("verdict", $"verdict は pass、convert、reject のどれかで書いてください: {verdictText}");
                break;
        }

        var to = r.String("to");
        r.Done();
        return new TermDef(from, verdict, to, Src(table));
    }

    private static PlaceDef MapPlace(TomlTable table, List<DefinitionIssue> issues)
    {
        var r = new TableReader(table, issues);
        var id = ReadPlainId(r, "id");
        var name = r.RequiredString("name") ?? string.Empty;
        var tags = r.Strings("tags");
        var group = r.String("group");
        var links = ImmutableArray.CreateBuilder<LinkDef>();
        var linkTable = r.Table("links");
        if (linkTable is not null)
        {
            var lr = new TableReader(linkTable, issues);
            foreach (var (key, node) in linkTable.Entries)
            {
                if (node is TomlScalar { Value: long minutes } && minutes > 0 && minutes <= int.MaxValue)
                {
                    links.Add(new LinkDef(key, (int)minutes, node.Location));
                }
                else
                {
                    lr.Invalid(node, $"移動時間は 1 以上の分の整数で書いてください: {key}");
                }
            }
        }

        r.Done();
        return new PlaceDef(
            id,
            name,
            tags,
            group,
            links.ToImmutable().OrderBy(link => link.To, StringComparer.Ordinal).ToImmutableArray(),
            Src(table));
    }

    private static Preference? ReadPreference(TomlNode node, TableReader r, string key)
    {
        if (node is TomlScalar { Value: string text })
        {
            return text switch
            {
                "like" => new Preference(PreferenceKind.Like, 1.0),
                "dislike" => new Preference(PreferenceKind.Dislike, 1.0),
                "forbid" => new Preference(PreferenceKind.Forbid, 0),
                _ => ReportPreference(r, node, key),
            };
        }

        if (node is TomlScalar { Value: long or double } number)
        {
            var factor = number.Value is long integer ? integer : (double)number.Value;
            if (!(factor >= 0 && double.IsFinite(factor)))
            {
                r.Invalid(node, $"好みの数は 0 以上の有限の数で書いてください: {key}");
                return null;
            }

            return factor == 0
                ? new Preference(PreferenceKind.Forbid, 0)
                : new Preference(PreferenceKind.Factor, factor);
        }

        r.Invalid(node, $"好みは like、dislike、forbid、数のどれかで書いてください: {key}");
        return null;
    }

    private static Preference? ReportPreference(TableReader r, TomlNode node, string key)
    {
        r.Invalid(node, $"好みは like、dislike、forbid、数のどれかで書いてください: {key}");
        return null;
    }

    private static VitalsDef MapVitals(TableReader r)
    {
        var energyInitial = 80.0;
        var floor = -50.0;
        var ceiling = 100.0;
        var rateAlone = 2.0;
        var rateTogether = -3.0;
        var sleepTarget = TimeSpan.FromHours(7);

        var vitals = r.Table("vitals");
        if (vitals is not null)
        {
            var vr = new TableReader(vitals, issues: r.Issues);
            var energy = vr.Table("energy");
            if (energy is not null)
            {
                var er = new TableReader(energy, r.Issues);
                energyInitial = ReadRanged(er, "initial", 0, 100, energyInitial);
                er.Done();
            }

            var social = vr.Table("social");
            if (social is not null)
            {
                var sr = new TableReader(social, r.Issues);
                rateAlone = ReadRanged(sr, "rate_alone", -1000, 1000, rateAlone);
                rateTogether = ReadRanged(sr, "rate_together", -1000, 1000, rateTogether);
                floor = ReadRanged(sr, "floor", -100, 100, floor);
                ceiling = ReadRanged(sr, "ceiling", -100, 100, ceiling);
                sr.Done();
            }

            var health = vr.Table("health");
            if (health is not null)
            {
                var hr = new TableReader(health, r.Issues);
                sleepTarget = hr.Span("sleep_target") ?? sleepTarget;
                hr.Done();
            }

            vr.Done();
        }

        if (!(floor < ceiling))
        {
            r.InvalidAt("vitals", "社交欲求の下限は上限より小さく書いてください。");
        }

        return new VitalsDef(energyInitial, floor, ceiling, rateAlone, rateTogether, sleepTarget);
    }

    private static ImmutableDictionary<VitalName, ReflexDef> MapReflexes(TableReader r)
    {
        var reflexes = ImmutableDictionary.CreateBuilder<VitalName, ReflexDef>();
        var reflexTable = r.Table("reflexes");
        if (reflexTable is not null)
        {
            var rr = new TableReader(reflexTable, r.Issues);
            foreach (var (key, node) in reflexTable.Entries)
            {
                if (!VitalKeys.TryGetValue(key, out var vital) || node is not TomlTable reflex)
                {
                    rr.Invalid(node, $"反射は状態変数の表で書いてください: {key}");
                    continue;
                }

                if (vital == VitalName.Social)
                {
                    rr.Invalid(node, "社交欲求にはしきい値がありません（D5 5.6）。");
                    continue;
                }

                var fr = new TableReader(reflex, r.Issues);
                var threshold = fr.Number("threshold");
                if (threshold is not null && !(double.IsFinite(threshold.Value) && threshold.Value >= 0 && threshold.Value <= 100))
                {
                    fr.InvalidAt("threshold", "threshold は 0 から 100 の間の有限の数で書いてください。");
                    threshold = 0;
                }
                var action = fr.String("action") ?? DefaultReflexAction(vital);
                if (threshold is null)
                {
                    fr.Missing("threshold");
                }

                reflexes[vital] = new ReflexDef(threshold ?? 0, action, node.Location);
                fr.Done();
            }
        }

        foreach (var (vital, threshold) in new[] { (VitalName.Energy, 0.0), (VitalName.Health, 20.0) })
        {
            if (!reflexes.ContainsKey(vital))
            {
                reflexes[vital] = new ReflexDef(threshold, DefaultReflexAction(vital), r.Location);
            }
        }

        return reflexes.ToImmutable();
    }

    private static string DefaultReflexAction(VitalName vital) => vital switch
    {
        VitalName.Health => "core.go_home_rest",
        _ => "core.faint",
    };

    private static DisruptionWeights MapDisruption(TableReader r)
    {
        var late = 1.0;
        var absent = 0.3;
        var wait = 0.5;
        var table = r.Table("disruption");
        if (table is not null)
        {
            var dr = new TableReader(table, r.Issues);
            late = ReadNonNegative(dr, "late", late);
            absent = ReadNonNegative(dr, "absent", absent);
            wait = ReadNonNegative(dr, "wait", wait);
            dr.Done();
        }

        return new DisruptionWeights(late, absent, wait);
    }

    private static ActionEffects MapActionEffects(TableReader r)
    {
        var effectsTable = r.Table("effects");
        if (effectsTable is null)
        {
            return EmptyEffects();
        }

        var er = new TableReader(effectsTable, r.Issues);
        var rates = ReadVitalTable(er, "rate", -1000, 1000);
        var onStart = ReadVitalTable(er, "on_start", -1000, 1000);
        var onEnd = ReadVitalTable(er, "on_end", -1000, 1000);
        var restore = ReadVitalTable(er, "restore", RestoreRange);
        var repeatDecay = er.Number("repeat_decay");
        if (repeatDecay is not null && !(repeatDecay > 0 && repeatDecay <= 1))
        {
            er.InvalidAt("repeat_decay", "repeat_decay は 0 より大きく 1 以下の数で書いてください。");
        }
        SleepEffect? sleep = null;
        var sleepTable = er.Table("sleep");
        if (sleepTable is not null)
        {
            var sr = new TableReader(sleepTable, r.Issues);
            sleep = new SleepEffect(sr.Bool("consolidate") ?? false, sr.Bool("debt") ?? false);
            sr.Done();
        }

        var handler = er.String("handler");
        er.Done();
        return new ActionEffects(rates, onStart, onEnd, restore, repeatDecay, sleep, handler);
    }

    private static ActionEffects EmptyEffects() => new(
        ImmutableDictionary<VitalName, double>.Empty,
        ImmutableDictionary<VitalName, double>.Empty,
        ImmutableDictionary<VitalName, double>.Empty,
        ImmutableDictionary<VitalName, double>.Empty,
        null,
        null,
        null);

    private static IncidentEffects MapIncidentEffects(TableReader r)
    {
        var effectsTable = r.Table("effects");
        if (effectsTable is null)
        {
            return new IncidentEffects(null, null, ImmutableDictionary<VitalName, double>.Empty, null);
        }

        var er = new TableReader(effectsTable, r.Issues);
        SlowPlaceEffect? slow = null;
        var slowTable = er.Table("slow_place");
        if (slowTable is not null)
        {
            var sr = new TableReader(slowTable, r.Issues);
            var place = sr.String("place");
            var placeTag = sr.String("place_tag");
            var add = sr.Span("add");
            var factor = sr.Number("factor");
            CheckPlaceChoice(sr, place, placeTag);
            if (add is null && factor is null)
            {
                sr.Error(DefinitionIssueCode.InvalidValue, slowTable.Location, "slow_place には add か factor の少なくとも一方が必要です。");
            }

            if (factor is not null && !(factor > 0 && double.IsFinite(factor.Value)))
            {
                sr.InvalidAt("factor", "factor は 0 より大きい有限の数で書いてください。");
            }

            slow = new SlowPlaceEffect(place, placeTag, add, factor);
            sr.Done();
        }

        ClosePlaceEffect? close = null;
        var closeTable = er.Table("close_place");
        if (closeTable is not null)
        {
            var cr = new TableReader(closeTable, r.Issues);
            var place = cr.String("place");
            var placeTag = cr.String("place_tag");
            CheckPlaceChoice(cr, place, placeTag);
            close = new ClosePlaceEffect(place, placeTag);
            cr.Done();
        }

        var vitals = ReadVitalTable(er, "vital", -1000, 1000);
        var condition = er.String("condition");
        er.Done();
        return new IncidentEffects(slow, close, vitals, condition);
    }

    /// <summary>有限で、範囲の中の数を読む。外れていれば位置つきのエラーにして、既定値を返す。</summary>
    private static double ReadRanged(TableReader r, string key, double min, double max, double fallback)
    {
        var value = r.Number(key);
        if (value is null)
        {
            return fallback;
        }

        if (!(double.IsFinite(value.Value) && value.Value >= min && value.Value <= max))
        {
            r.InvalidAt(key, $"{key} は {FormatBound(min)} から {FormatBound(max)} の間の有限の数で書いてください。");
            return fallback;
        }

        return value.Value;
    }

    private static double ReadNonNegative(TableReader r, string key, double fallback)
    {
        var value = r.Number(key);
        if (value is null)
        {
            return fallback;
        }

        if (!(value >= 0 && double.IsFinite(value.Value)))
        {
            r.InvalidAt(key, $"{key} は 0 以上の有限の数で書いてください。");
            return fallback;
        }

        return value.Value;
    }

    private static void CheckPlaceChoice(TableReader r, string? place, string? placeTag)
    {
        if ((place is null) == (placeTag is null))
        {
            r.Error(DefinitionIssueCode.InvalidValue, r.Location, "place と place_tag は、どちらか一方だけを書いてください。");
        }
    }

    private static ImmutableDictionary<VitalName, double> ReadVitalTable(TableReader parent, string key, double min, double max) =>
        ReadVitalTable(parent, key, _ => (min, max));

    /// <summary>復元の範囲（D2 2.5）。気力、健康度、退屈度は 0〜100、社交欲求は −100〜100。</summary>
    private static (double Min, double Max) RestoreRange(VitalName vital) =>
        vital == VitalName.Social ? (-100, 100) : (0, 100);

    private static string FormatBound(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static ImmutableDictionary<VitalName, double> ReadVitalTable(TableReader parent, string key, Func<VitalName, (double Min, double Max)> range)
    {
        var builder = ImmutableDictionary.CreateBuilder<VitalName, double>();
        var table = parent.Table(key);
        if (table is null)
        {
            return builder.ToImmutable();
        }

        var tr = new TableReader(table, parent.Issues);
        foreach (var (name, node) in table.Entries)
        {
            if (!VitalKeys.TryGetValue(name, out var vital))
            {
                tr.Invalid(node, $"知らない状態変数です: {name}");
                continue;
            }

            if (node is TomlScalar { Value: long or double } number)
            {
                var value = number.Value is long integer ? integer : (double)number.Value;
                var (min, max) = range(vital);
                if (double.IsFinite(value) && value >= min && value <= max)
                {
                    builder[vital] = value;
                }
                else
                {
                    tr.Invalid(node, $"{name} は {FormatBound(min)} から {FormatBound(max)} の間の有限の数で書いてください。");
                }
            }
            else
            {
                tr.Invalid(node, $"{name} は数で書いてください。");
            }
        }

        return builder.ToImmutable();
    }

    private static PhonePolicy? ReadPhone(TableReader r, string key)
    {
        var text = r.String(key);
        return text switch
        {
            null => null,
            "open" => PhonePolicy.Open,
            "closed" => PhonePolicy.Closed,
            _ => ReportPhone(r, key, text),
        };
    }

    private static PhonePolicy? ReportPhone(TableReader r, string key, string text)
    {
        r.InvalidAt(key, $"phone は open か closed で書いてください: {text}");
        return null;
    }

    private static ScheduleDef MapSchedule(TomlTable table, List<DefinitionIssue> issues)
    {
        var r = new TableReader(table, issues);
        var periods = ImmutableArray.CreateBuilder<PeriodDef>();
        foreach (var periodTable in r.TableArray("period"))
        {
            var period = MapPeriod(periodTable, issues);
            if (period is not null)
            {
                periods.Add(period);
            }
        }

        var blocks = ImmutableArray.CreateBuilder<BlockDef>();
        foreach (var blockTable in r.TableArray("block"))
        {
            blocks.Add(MapBlock(blockTable, issues, isException: false));
        }

        var exceptions = ImmutableArray.CreateBuilder<ExceptionDef>();
        foreach (var exceptionTable in r.TableArray("exception"))
        {
            var exception = MapException(exceptionTable, issues);
            if (exception is not null)
            {
                exceptions.Add(exception);
            }
        }

        r.Done();
        return new ScheduleDef(periods.ToImmutable(), blocks.ToImmutable(), exceptions.ToImmutable());
    }

    private static PeriodDef? MapPeriod(TomlTable table, List<DefinitionIssue> issues)
    {
        var r = new TableReader(table, issues);
        var id = ReadPlainId(r, "id");
        var from = r.RequiredParsed<MonthDay>("from", TimeText.TryParseMonthDay, "\"MM-DD\" の形の日付");
        var to = r.RequiredParsed<MonthDay>("to", TimeText.TryParseMonthDay, "\"MM-DD\" の形の日付");
        r.Done();
        if (from is null || to is null)
        {
            return null;
        }

        return new PeriodDef(id, from.Value, to.Value, Src(table));
    }

    /// <summary>
    /// 場所、ブロック、期間の ID を読む。英小文字、数字、_ だけで、空にはできない（名前空間は使えない）。
    /// </summary>
    /// <param name="r">読む表。</param>
    /// <param name="key">キー。</param>
    public static string ReadPlainId(TableReader r, string key)
    {
        var id = r.RequiredString(key);
        if (id is null)
        {
            return string.Empty;
        }

        if (id.Length == 0)
        {
            r.InvalidAt(key, $"{key} は空にできません。");
            return string.Empty;
        }

        if (!UserIdPattern().IsMatch(id))
        {
            r.InvalidAt(key, $"ID は英小文字、数字、_ だけで書いてください: {id}");
        }

        return id;
    }

    private static ExceptionDef? MapException(TomlTable table, List<DefinitionIssue> issues)
    {
        var r = new TableReader(table, issues);
        var dateText = r.RequiredString("date");
        var date = default(DateOnly);
        var valid = dateText is null || DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        if (!valid)
        {
            r.InvalidAt("date", "date は \"yyyy-MM-dd\" の形で書いてください: " + dateText);
        }

        var skip = r.Strings("skip");
        var blocks = ImmutableArray.CreateBuilder<BlockDef>();
        foreach (var blockTable in r.TableArray("block"))
        {
            blocks.Add(MapBlock(blockTable, issues, isException: true));
        }

        r.Done();
        if (dateText is null || !valid)
        {
            return null;
        }

        return new ExceptionDef(
            date,
            skip,
            blocks.ToImmutable(),
            Src(table));
    }

    private static BlockDef MapBlock(TomlTable table, List<DefinitionIssue> issues, bool isException)
    {
        var r = new TableReader(table, issues);
        var id = ReadPlainId(r, "id");
        var within = r.String("within");
        var inPeriods = r.Strings("in");
        var days = ReadDays(r);
        var ranges = ReadRanges(r, table);
        var action = r.String("action");
        var place = r.String("place");
        var skipHoliday = r.Bool("skip_holiday");
        var phone = ReadPhone(r, "phone");
        var phoneEvery = r.Span("phone_every");
        var with = r.Strings("with");
        var onPartnerAbsent = ReadPartnerAbsent(r);

        if (isException)
        {
            if (within is not null)
            {
                r.InvalidAt("within", "例外日のブロックは within を使えません。");
            }

            if (inPeriods.Length > 0 || days.Length > 0)
            {
                r.InvalidAt("id", "例外日のブロックは in と days を使えません（その日だけに足すため）。");
            }
        }

        r.Done();
        return new BlockDef(
            id,
            within,
            inPeriods,
            days,
            ranges,
            action,
            place,
            skipHoliday,
            phone,
            with,
            onPartnerAbsent,
            phoneEvery,
            Src(table));
    }

    private static ImmutableArray<DayOfWeek> ReadDays(TableReader r)
    {
        var builder = ImmutableArray.CreateBuilder<DayOfWeek>();
        foreach (var text in r.Strings("days"))
        {
            if (DayKeys.TryGetValue(text, out var day))
            {
                builder.Add(day);
            }
            else
            {
                r.InvalidAt("days", $"曜日は mon から sun で書いてください: {text}");
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<TimeRange> ReadRanges(TableReader r, TomlTable table)
    {
        var builder = ImmutableArray.CreateBuilder<TimeRange>();
        var hasFromTo = table.ContainsKey("from") || table.ContainsKey("to");
        var hasTimes = table.ContainsKey("times");
        if (hasFromTo && hasTimes)
        {
            r.Error(DefinitionIssueCode.InvalidValue, r.LocationOf("times"), "from と to と times は一緒に書けません。");
        }

        if (hasFromTo)
        {
            var from = r.RequiredParsed<TimeOnly>("from", TimeText.TryParseTimeOfDay, "\"HH:mm\" の形の時刻");
            var to = r.RequiredParsed<TimeOnly>("to", TimeText.TryParseTimeOfDay, "\"HH:mm\" の形の時刻");
            if (from is not null && to is not null)
            {
                builder.Add(new TimeRange(from.Value, to.Value));
            }
        }
        else if (!hasTimes)
        {
            r.Error(DefinitionIssueCode.MissingRequired, r.Location, "from と to、または times が必要です。");
        }

        if (hasTimes)
        {
            foreach (var pair in r.Array("times"))
            {
                if (pair is TomlArray { Items.Count: 2 } array
                    && array.Items[0] is TomlScalar { Value: string fromText }
                    && array.Items[1] is TomlScalar { Value: string toText }
                    && TimeText.TryParseTimeOfDay(fromText, out var start)
                    && TimeText.TryParseTimeOfDay(toText, out var end))
                {
                    builder.Add(new TimeRange(start, end));
                }
                else
                {
                    r.Error(DefinitionIssueCode.InvalidValue, pair.Location, "times は [\"HH:mm\", \"HH:mm\"] の並びで書いてください。");
                }
            }
        }

        return builder.ToImmutable();
    }

    private static PartnerAbsentPolicy? ReadPartnerAbsent(TableReader r)
    {
        var text = r.String("on_partner_absent");
        return text switch
        {
            null => null,
            "continue" => PartnerAbsentPolicy.Continue,
            "cancel" => PartnerAbsentPolicy.Cancel,
            _ => ReportPartnerAbsent(r, text),
        };
    }

    private static PartnerAbsentPolicy? ReportPartnerAbsent(TableReader r, string text)
    {
        r.InvalidAt("on_partner_absent", $"on_partner_absent は continue か cancel で書いてください: {text}");
        return null;
    }
}
