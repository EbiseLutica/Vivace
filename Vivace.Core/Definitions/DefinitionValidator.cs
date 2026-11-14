using Vivace.Core.Definitions.Model;
using Vivace.Core.Domain.State;
using Vivace.Core.Routes;
using System.Collections.Immutable;
using System.Globalization;
using Vivace.Core.Domain;
using Vivace.Core.Schedule;
using Vivace.Core.Time;

namespace Vivace.Core.Definitions;

/// <summary>読み込んだ定義の参照と整合性を検査し、位置つきのエラーと警告を返す。</summary>
public static class DefinitionValidator
{
    /// <summary>固定予定の重なりを見る最初の年。28 年周期（うるう年と曜日の組み合わせを一巡する）で見る。</summary>
    private const int ScheduleCheckFirstYear = 2028;

    /// <summary>固定予定の重なりを見る年の数（28 年）。</summary>
    private const int ScheduleCheckYears = 28;

    private const int MinutesPerDay = 24 * 60;

    /// <summary>
    /// 定義の全体を検査する。型と範囲は読み込みのときに検査済みで、ここでは参照と組み合わせを見る。
    /// </summary>
    /// <param name="definitions">検査する定義。</param>
    public static ImmutableArray<DefinitionIssue> Validate(DefinitionPack definitions)
    {
        var issues = new List<DefinitionIssue>();
        foreach (var world in definitions.Worlds.Values)
        {
            ValidateWorld(world, issues);
        }

        var reflexActions = definitions.Personas.Values.SelectMany(persona => persona.Reflexes.Values)
            .Select(reflex => reflex.ActionId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var action in definitions.Actions.Values)
        {
            ValidateAction(action, reflexActions, issues);
        }

        foreach (var persona in definitions.Personas.Values)
        {
            ValidatePersona(persona, definitions, issues);
        }

        foreach (var incident in definitions.Incidents.Values)
        {
            ValidateIncident(incident, definitions, issues);
        }

        ValidateUnusedPlaceTags(definitions, issues);
        return issues.ToImmutableArray();
    }

    /// <summary>ワールドの定義の参照と整合性を見る。</summary>
    private static void ValidateWorld(WorldDef world, List<DefinitionIssue> issues)
    {
        var placeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var place in world.Places)
        {
            if (!placeIds.Add(place.Id))
            {
                Add(issues, DefinitionIssueCode.DuplicateId, place.Source.Location, $"同じ場所の ID が2つあります: {place.Id}");
            }
        }

        var links = new Dictionary<(string From, string To), LinkDef>();
        foreach (var place in world.Places)
        {
            foreach (var link in place.Links)
            {
                if (!placeIds.Contains(link.To))
                {
                    Add(issues, DefinitionIssueCode.MissingReference, link.Source, $"つながる場所がありません: {link.To}");
                    continue;
                }

                links[(place.Id, link.To)] = link;
            }
        }

        foreach (var ((from, to), link) in links)
        {
            if (string.CompareOrdinal(from, to) <= 0 || !links.TryGetValue((to, from), out var reverse))
            {
                continue;
            }

            if (reverse.Minutes != link.Minutes)
            {
                Add(issues, DefinitionIssueCode.EdgeMismatch, reverse.Source,
                    $"両側に書いた辺の移動時間が食い違っています: {from} と {to}（{link.Minutes} 分と {reverse.Minutes} 分）");
            }
        }

        foreach (var term in world.Terms)
        {
            if (term.Verdict == TermVerdict.Convert && term.To is null)
            {
                Add(issues, DefinitionIssueCode.MissingRequired, term.Source.At("to"), $"convert の変換先（to）がありません: {term.From}");
            }
        }
    }

    /// <summary>行動の定義の整合性を見る。</summary>
    private static void ValidateAction(ActionDef action, HashSet<string> reflexActions, List<DefinitionIssue> issues)
    {
        // core.go_to_next は所要時間を持たない。着いたあとの待ちは次の予定で決まる。
        if (action.Duration is null && !action.ScheduleOnly && !reflexActions.Contains(action.Id) && action.Id != "core.go_to_next")
        {
            Add(issues, DefinitionIssueCode.MissingRequired, action.Source.Location,
                $"schedule_only でない行動には所要時間（duration）が必要です: {action.Id}");
        }
    }

    /// <summary>ペルソナの定義の参照と整合性を見る。</summary>
    private static void ValidatePersona(PersonaDef persona, DefinitionPack definitions, List<DefinitionIssue> issues)
    {
        var hasWorld = definitions.Worlds.TryGetValue(persona.World, out var world);
        if (!hasWorld)
        {
            Add(issues, DefinitionIssueCode.MissingReference, persona.Source.At("world"), $"居住するワールドがありません: {persona.World}");
        }

        if (hasWorld && !world!.Places.Any(place => place.Id == persona.Home))
        {
            Add(issues, DefinitionIssueCode.MissingReference, persona.Source.At("home"), $"家の場所がワールドにありません: {persona.Home}");
        }

        foreach (var entry in persona.Use)
        {
            if (IsWildcard(entry))
            {
                var prefix = WildcardPrefix(entry);
                if (!definitions.Actions.Keys.Any(id => id.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    Add(issues, DefinitionIssueCode.UnmatchedWildcard, persona.Source.At("actions.use"),
                        $"使う行動のワイルドカードに当たる行動がありません（プラグインが足す行動かもしれません）: {entry}");
                }
            }
            else if (!definitions.Actions.ContainsKey(entry))
            {
                Add(issues, DefinitionIssueCode.MissingReference, persona.Source.At("actions.use"), $"使う行動がありません: {entry}");
            }
        }

        foreach (var (key, preference) in persona.Prefs)
        {
            var location = persona.Source.At($"actions.prefs.{key}");
            if (key == "core.go_to_next" && preference.Kind == PreferenceKind.Forbid)
            {
                Add(issues, DefinitionIssueCode.ForbidGoToNext, location,
                    "core.go_to_next は forbid できません（次の予定への移動は必ず候補に入ります）。");
            }
            else if (!key.StartsWith("tag:", StringComparison.Ordinal) && !definitions.Actions.ContainsKey(key))
            {
                Add(issues, DefinitionIssueCode.MissingReference, location, $"好みに書いた行動がありません: {key}");
            }
        }

        foreach (var reflex in persona.Reflexes.Values)
        {
            if (!definitions.Actions.TryGetValue(reflex.ActionId, out var reflexAction))
            {
                Add(issues, DefinitionIssueCode.MissingReference, reflex.Source, $"反射で始める行動がありません: {reflex.ActionId}");
                continue;
            }

            if (reflexAction.Duration is null)
            {
                Add(issues, DefinitionIssueCode.MissingRequired, reflexAction.Source.Location, $"反射の行動には所要時間が必要です: {reflexAction.Id}");
            }
            else if (reflexAction.Duration.IsRange)
            {
                Add(issues, DefinitionIssueCode.ReflexRange, reflexAction.Source.At("duration"), $"反射の行動の所要時間は固定で書いてください: {reflexAction.Id}");
            }
        }

        if (hasWorld)
        {
            CheckRequirements(persona, world!, definitions, issues);
        }

        ValidateSchedule(persona, definitions, issues);
    }

    /// <summary>ペルソナが一度も実行できない行動を警告にする。</summary>
    private static void CheckRequirements(PersonaDef persona, WorldDef world, DefinitionPack definitions, List<DefinitionIssue> issues)
    {
        var worldTags = world.Places.SelectMany(place => place.Tags).ToHashSet(StringComparer.Ordinal);
        var items = persona.Items.ToHashSet(StringComparer.Ordinal);
        foreach (var action in ResolveUse(persona, definitions))
        {
            foreach (var tag in action.Requires.PlaceTags)
            {
                if (!worldTags.Contains(tag))
                {
                    Add(issues, DefinitionIssueCode.UnsatisfiableRequirement, persona.Source.At("actions.use"),
                        $"行動 {action.Id} の requires のタグ \"{tag}\" を、ワールド {world.Id} のどの場所も持っていません。");
                }
            }

            foreach (var item in action.Requires.Items)
            {
                if (!items.Contains(item))
                {
                    Add(issues, DefinitionIssueCode.UnsatisfiableRequirement, persona.Source.At("actions.use"),
                        $"行動 {action.Id} の requires の携行品 \"{item}\" を、ペルソナ {persona.Id} は持っていないため、一度も実行できません。");
                }
            }
        }
    }

    /// <summary>ペルソナの使う行動の一覧を、ワイルドカードを開いて返す。</summary>
    private static IEnumerable<ActionDef> ResolveUse(PersonaDef persona, DefinitionPack definitions)
    {
        foreach (var entry in persona.Use)
        {
            if (IsWildcard(entry))
            {
                var prefix = WildcardPrefix(entry);
                foreach (var candidate in definitions.Actions.Values)
                {
                    if (candidate.Id.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (definitions.Actions.TryGetValue(entry, out var action))
            {
                yield return action;
            }
        }
    }

    /// <summary>出来事の定義の参照を見る。</summary>
    private static void ValidateIncident(IncidentDef incident, DefinitionPack definitions, List<DefinitionIssue> issues)
    {
        var condition = incident.Effects.Condition;
        if (condition is not null && !definitions.Conditions.ContainsKey(condition))
        {
            Add(issues, DefinitionIssueCode.MissingReference, incident.Source.At("effects.condition"), $"付ける状態がありません: {condition}");
        }

        var placeIds = definitions.Worlds.Values.SelectMany(world => world.Places).Select(place => place.Id).ToHashSet(StringComparer.Ordinal);
        if (incident.Effects.SlowPlace?.Place is { } slowPlace && !placeIds.Contains(slowPlace))
        {
            Add(issues, DefinitionIssueCode.MissingReference, incident.Source.At("effects.slow_place.place"), $"場所がありません: {slowPlace}");
        }

        if (incident.Effects.ClosePlace?.Place is { } closedPlace && !placeIds.Contains(closedPlace))
        {
            Add(issues, DefinitionIssueCode.MissingReference, incident.Source.At("effects.close_place.place"), $"場所がありません: {closedPlace}");
        }
    }

    /// <summary>どの行動も出来事も要求しない場所のタグを警告にする。</summary>
    private static void ValidateUnusedPlaceTags(DefinitionPack definitions, List<DefinitionIssue> issues)
    {
        var usedTags = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in definitions.Actions.Values)
        {
            usedTags.UnionWith(action.Requires.PlaceTags);
        }

        foreach (var incident in definitions.Incidents.Values)
        {
            foreach (var tag in new[] { incident.Effects.SlowPlace?.PlaceTag, incident.Effects.ClosePlace?.PlaceTag })
            {
                if (tag is not null)
                {
                    usedTags.Add(tag);
                }
            }
        }

        foreach (var world in definitions.Worlds.Values)
        {
            foreach (var place in world.Places)
            {
                foreach (var tag in place.Tags)
                {
                    if (!usedTags.Contains(tag))
                    {
                        Add(issues, DefinitionIssueCode.UnusedPlaceTag, place.Source.At("tags"),
                            $"どの行動の requires にも出てこない場所のタグです（効果がありません）: {tag}");
                    }
                }
            }
        }
    }

    /// <summary>固定予定の参照と入れ子を見る。</summary>
    private static void ValidateSchedule(PersonaDef persona, DefinitionPack definitions, List<DefinitionIssue> issues)
    {
        var schedule = persona.Schedule;
        var periodIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var period in schedule.Periods)
        {
            if (period.Id == "default")
            {
                Add(issues, DefinitionIssueCode.InvalidValue, period.Source.At("id"), "期間の ID に default は使えません（期間の外の日に使う名前です）。");
            }
            else if (!periodIds.Add(period.Id))
            {
                Add(issues, DefinitionIssueCode.DuplicateId, period.Source.Location, $"同じ期間の ID が2つあります: {period.Id}");
            }
        }

        periodIds.Add("default");
        var blockIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var block in schedule.Blocks)
        {
            if (!blockIds.Add(block.Id))
            {
                Add(issues, DefinitionIssueCode.DuplicateId, block.Source.Location, $"同じブロックの ID が2つあります: {block.Id}");
            }
        }

        foreach (var block in schedule.Blocks)
        {
            CheckBlock(block, schedule, persona, definitions, periodIds, issues);
        }

        var exceptionDates = new HashSet<DateOnly>();
        foreach (var exception in schedule.Exceptions)
        {
            if (!exceptionDates.Add(exception.Date))
            {
                Add(issues, DefinitionIssueCode.InvalidValue, exception.Source.Location, $"同じ日付の例外が2つあります: {exception.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}");
            }

            foreach (var skipped in exception.Skip)
            {
                if (!blockIds.Contains(skipped))
                {
                    Add(issues, DefinitionIssueCode.MissingReference, exception.Source.At("skip"), $"外すブロックがありません: {skipped}");
                }
            }

            foreach (var block in exception.Blocks)
            {
                CheckBlock(block, schedule, persona, definitions, periodIds, issues);
            }
        }

        CheckScheduleOverlaps(persona, definitions, issues);
    }

    /// <summary>ブロック1つの参照と組み合わせを見る。</summary>
    private static void CheckBlock(BlockDef block, ScheduleDef schedule, PersonaDef persona, DefinitionPack definitions, HashSet<string> periodIds, List<DefinitionIssue> issues)
    {
        foreach (var period in block.In)
        {
            if (!periodIds.Contains(period))
            {
                Add(issues, DefinitionIssueCode.MissingReference, block.Source.At("in"), $"期間がありません: {period}");
            }
        }

        if (block.Within is not null)
        {
            if (!schedule.Blocks.Any(parent => parent.Id == block.Within))
            {
                Add(issues, DefinitionIssueCode.MissingReference, block.Source.At("within"), $"親のブロックがありません: {block.Within}");
            }
            else if (IsWithinCycle(block, schedule))
            {
                Add(issues, DefinitionIssueCode.InvalidValue, block.Source.At("within"),
                    $"within が循環しています（自分自身を含む親の連鎖）: {block.Id}");
            }
        }

        if (block.Ranges.Length == 0)
        {
            Add(issues, DefinitionIssueCode.MissingRequired, block.Source.Location, $"時間の範囲がありません: {block.Id}");
        }

        foreach (var range in block.Ranges)
        {
            if (range.Start == range.End)
            {
                Add(issues, DefinitionIssueCode.InvalidValue, block.Source.At("to"), $"開始と終了が同じ時刻です: {range.Start:HH\\:mm}");
            }
        }

        var action = EffectiveValue(block, schedule, item => item.Action);
        if (action is null)
        {
            Add(issues, DefinitionIssueCode.MissingRequired, block.Source.At("action"), $"行動がありません（親からも引き継げません）: {block.Id}");
        }
        else if (!definitions.Actions.ContainsKey(action))
        {
            Add(issues, DefinitionIssueCode.MissingReference, block.Source.At("action"), $"行動がありません: {action}");
        }

        var place = EffectiveValue(block, schedule, item => item.Place);
        if (place is not null
            && (!definitions.Worlds.TryGetValue(persona.World, out var world) || !world.Places.Any(item => item.Id == place)))
        {
            Add(issues, DefinitionIssueCode.MissingReference, block.Source.At("place"), $"場所がワールドにありません: {place}");
        }

        foreach (var partnerId in block.With)
        {
            if (!definitions.Personas.TryGetValue(new PersonaDefId(partnerId), out var partner))
            {
                Add(issues, DefinitionIssueCode.MissingReference, block.Source.At("with"), $"同行するペルソナがありません: {partnerId}");
                continue;
            }

            if (!AllBlocks(partner).Any(item => item.With.Contains(persona.Id.Value)))
            {
                Add(issues, DefinitionIssueCode.OneSidedWith, block.Source.At("with"),
                    $"同行を片側にしか書いていません（{partnerId} の予定にも書いてください）。同行としては扱いません。");
            }
        }
    }

    /// <summary>ペルソナの基本のブロックと、例外日に足すブロックをすべて返す。</summary>
    private static IEnumerable<BlockDef> AllBlocks(PersonaDef persona) =>
        persona.Schedule.Blocks.Concat(persona.Schedule.Exceptions.SelectMany(item => item.Blocks));

    /// <summary>親をたどって、自分に戻る（循環する）かどうか。</summary>
    private static bool IsWithinCycle(BlockDef block, ScheduleDef schedule)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal) { block.Id };
        var current = block;
        while (current.Within is not null)
        {
            if (!visited.Add(current.Within))
            {
                return true;
            }

            var parent = schedule.Blocks.FirstOrDefault(item => item.Id == current.Within);
            if (parent is null)
            {
                return false;
            }

            current = parent;
        }

        return false;
    }

    /// <summary>親をたどって、ブロックに実際に効く値を返す。</summary>
    private static string? EffectiveValue(BlockDef block, ScheduleDef schedule, Func<BlockDef, string?> pick)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = block;
        while (visited.Add(current.Id))
        {
            var value = pick(current);
            if (value is not null)
            {
                return value;
            }

            if (current.Within is null)
            {
                return null;
            }

            var parent = schedule.Blocks.FirstOrDefault(item => item.Id == current.Within);
            if (parent is null)
            {
                return null;
            }

            current = parent;
        }

        return null;
    }

    /// <summary>28 年周期の全日と、例外日の前後の日について、固定予定の重なりと、同行の時刻、場所、行動の一致を見る。</summary>
    private static void CheckScheduleOverlaps(PersonaDef persona, DefinitionPack definitions, List<DefinitionIssue> issues)
    {
        var reported = new HashSet<(string Message, SourceLocation Location)>();
        var table = definitions.Worlds.TryGetValue(persona.World, out var world) ? RouteCache.BuildTable(world) : null;
        var first = new DateOnly(ScheduleCheckFirstYear, 1, 1);
        var last = first.AddYears(ScheduleCheckYears).AddDays(-1);
        var specials = SpecialDates(persona);
        var dates = new SortedSet<DateOnly>(specials);

        // 同行の相手の例外日も、この人の日付として見る（相手の例外日が範囲の外でも、同じ警告になる）。
        foreach (var partnerId in AllBlocks(persona).SelectMany(block => block.With).Distinct(StringComparer.Ordinal))
        {
            if (definitions.Personas.TryGetValue(new PersonaDefId(partnerId), out var partner))
            {
                dates.UnionWith(SpecialDates(partner));
            }
        }
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            dates.Add(date);
        }

        var own = new DayResolver(definitions, persona, specials);
        var partnerResolvers = new Dictionary<string, DayResolver>(StringComparer.Ordinal);
        var checkedPairs = new HashSet<(string Own, string Partner)>();
        DateOnly? previousDate = null;
        ResolvedDay? previousDay = null;
        foreach (var date in dates)
        {
            var day = own.Get(date);
            ReportScheduleIssues(day.Issues, issues, reported);
            var consecutive = previousDate is { } before && before == date.AddDays(-1) ? previousDay : null;
            if (consecutive is not null)
            {
                ReportCrossMidnight(consecutive, day, persona, issues, reported);
            }

            if (table is not null)
            {
                ReportScheduleGaps(consecutive, day, persona, table, issues, reported);
            }

            CheckPartnersOn(date, day, own.KeyOf(date), persona, definitions, partnerResolvers, checkedPairs, issues, reported);
            previousDate = date;
            previousDay = day;
        }
    }

    /// <summary>例外日と、その前後の日（範囲の端は越えない）。</summary>
    private static HashSet<DateOnly> SpecialDates(PersonaDef persona)
    {
        var dates = new HashSet<DateOnly>();
        foreach (var exception in persona.Schedule.Exceptions)
        {
            var date = exception.Date;
            if (date > DateOnly.MinValue)
            {
                dates.Add(date.AddDays(-1));
            }

            dates.Add(date);
            if (date < DateOnly.MaxValue)
            {
                dates.Add(date.AddDays(1));
            }
        }

        return dates;
    }

    /// <summary>ペルソナの日ごとの解決。同じ結果になる日は、一度だけ解決する。</summary>
    private sealed class DayResolver(DefinitionPack definitions, PersonaDef persona, HashSet<DateOnly> specials)
    {
        private readonly Dictionary<string, ResolvedDay> _byKey = new(StringComparer.Ordinal);

        /// <summary>日付の区分のキー。例外日の前後は日付そのもの、それ以外は（期間、曜日）。</summary>
        public string KeyOf(DateOnly date) => specials.Contains(date)
            ? "date:" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : FixedScheduleResolver.PeriodOf(persona.Schedule.Periods, date) + "|" + ((int)date.DayOfWeek).ToString(CultureInfo.InvariantCulture);

        /// <summary>その日の解決の結果。</summary>
        public ResolvedDay Get(DateOnly date)
        {
            var key = KeyOf(date);
            if (!_byKey.TryGetValue(key, out var day))
            {
                day = FixedScheduleResolver.Resolve(definitions, persona, date, NoHolidays.Instance);
                _byKey[key] = day;
            }

            return day;
        }
    }

    /// <summary>同じ日の同行の相手と、時刻、場所、行動が一致するかを見る。</summary>
    private static void CheckPartnersOn(
        DateOnly date,
        ResolvedDay day,
        string ownKey,
        PersonaDef persona,
        DefinitionPack definitions,
        Dictionary<string, DayResolver> partnerResolvers,
        HashSet<(string Own, string Partner)> checkedPairs,
        List<DefinitionIssue> issues,
        HashSet<(string Message, SourceLocation Location)> reported)
    {
        var withBlocks = day.Blocks.Where(block => block.With.Length > 0).ToList();
        foreach (var partnerId in withBlocks.SelectMany(block => block.With).Distinct(StringComparer.Ordinal))
        {
            if (!definitions.Personas.TryGetValue(new PersonaDefId(partnerId), out var partner))
            {
                continue;
            }

            if (!partnerResolvers.TryGetValue(partnerId, out var resolver))
            {
                resolver = new DayResolver(definitions, partner, SpecialDates(partner));
                partnerResolvers[partnerId] = resolver;
            }

            var partnerKey = partnerId + "|" + resolver.KeyOf(date);
            if (!checkedPairs.Add((ownKey, partnerKey)))
            {
                continue;
            }

            var partnerDay = resolver.Get(date);
            foreach (var block in withBlocks.Where(block => block.With.Contains(partnerId, StringComparer.Ordinal)))
            {
                var matched = partnerDay.Blocks.Any(other =>
                    other.With.Contains(persona.Id.Value, StringComparer.Ordinal)
                    && other.StartMinute == block.StartMinute
                    && other.EndMinute == block.EndMinute
                    && other.Action == block.Action
                    && other.Place == block.Place);
                if (matched)
                {
                    continue;
                }

                var definition = FindDefinition(block.DefId, persona);
                if (definition is null)
                {
                    continue;
                }

                var message = $"同行の相手 {partnerId} と、時刻、場所、行動が一致しません: {block.DefId}";
                if (reported.Add((message, definition.Source.At("with"))))
                {
                    Add(issues, DefinitionIssueCode.PartnerMismatch, definition.Source.At("with"), message);
                }
            }
        }
    }

    /// <summary>固定予定の隙間が、場所の間の移動時間より短いものを警告にする。警告は後のブロックの定義の位置に付ける。</summary>
    private static void ReportScheduleGaps(
        ResolvedDay? previous,
        ResolvedDay day,
        PersonaDef persona,
        Routes.RouteTable table,
        List<DefinitionIssue> issues,
        HashSet<(string Message, SourceLocation Location)> reported)
    {
        var sequence = (previous?.Blocks ?? ImmutableArray<ResolvedBlock>.Empty)
            .Select(block => (Block: block, Start: block.StartMinute - MinutesPerDay, End: block.EndMinute - MinutesPerDay, Current: false))
            .Concat(day.Blocks.Select(block => (Block: block, Start: block.StartMinute, End: block.EndMinute, Current: true)))
            .Where(entry => entry.Block.Place is not null)
            .OrderBy(entry => entry.Start)
            .ThenBy(entry => entry.Block.Id, StringComparer.Ordinal)
            .ToList();
        for (var i = 1; i < sequence.Count; i++)
        {
            var before = sequence[i - 1];
            var after = sequence[i];
            if (!after.Current)
            {
                continue;
            }

            var gap = after.Start - before.End;
            if (table.Minutes(new PlaceId(before.Block.Place!), new PlaceId(after.Block.Place!)) is not { } travel || gap >= travel)
            {
                continue;
            }

            var message = $"{persona.Id}: 固定予定の隙間（{before.Block.DefId} と {after.Block.DefId} の間は {gap} 分）が、移動時間（{travel} 分）より短い";
            if (FindDefinition(after.Block.DefId, persona) is { } definition && reported.Add((message, definition.Source.Location)))
            {
                Add(issues, DefinitionIssueCode.ScheduleGap, definition.Source.Location, message);
            }
        }
    }

    /// <summary>固定予定の解決で出た問題を、重複を除いて一覧に足す。</summary>
    private static void ReportScheduleIssues(IEnumerable<ScheduleIssue> items, List<DefinitionIssue> issues, HashSet<(string Message, SourceLocation Location)> reported)
    {
        foreach (var item in items)
        {
            if (reported.Add((item.Message, item.Block.Source.Location)))
            {
                Add(issues, DefinitionIssueCode.ScheduleOverlap, item.Block.Source.Location, item.Message);
            }
        }
    }

    /// <summary>前の日から続くブロックと今日のブロックの重なりを一覧に足す。</summary>
    private static void ReportCrossMidnight(
        ResolvedDay previous,
        ResolvedDay day,
        PersonaDef persona,
        List<DefinitionIssue> issues,
        HashSet<(string Message, SourceLocation Location)> reported)
    {
        foreach (var tail in previous.Blocks)
        {
            if (tail.EndMinute <= MinutesPerDay)
            {
                continue;
            }

            var tailEnd = tail.EndMinute - MinutesPerDay;
            foreach (var block in day.Blocks)
            {
                if (block.StartMinute >= tailEnd || FindDefinition(block.DefId, persona) is not { } definition)
                {
                    continue;
                }

                var message = $"前日から続くブロック {tail.DefId} と重なっています: {block.DefId}";
                if (reported.Add((message, definition.Source.Location)))
                {
                    Add(issues, DefinitionIssueCode.ScheduleOverlap, definition.Source.Location, message);
                }
            }
        }
    }

    /// <summary>解決したブロックの定義を、定義 ID で引く。</summary>
    private static BlockDef? FindDefinition(string defId, PersonaDef persona) =>
        persona.Schedule.Blocks.FirstOrDefault(block => block.Id == defId)
        ?? persona.Schedule.Exceptions.SelectMany(item => item.Blocks).FirstOrDefault(block => block.Id == defId);

    /// <summary>ワイルドカードの書き方か。</summary>
    private static bool IsWildcard(string entry) => entry.EndsWith(".*", StringComparison.Ordinal);

    /// <summary>ワイルドカードの前の部分を返す（末尾の "." を含む）。</summary>
    private static string WildcardPrefix(string entry) => entry[..^1];

    /// <summary>問題を一覧に足す。重大度はコードから決める。</summary>
    private static void Add(List<DefinitionIssue> issues, DefinitionIssueCode code, SourceLocation location, string message) =>
        issues.Add(new DefinitionIssue(SeverityOf(code), code, location, message));

    /// <summary>問題のコードの重大度を返す。</summary>
    private static IssueSeverity SeverityOf(DefinitionIssueCode code) => code switch
    {
        DefinitionIssueCode.UnknownKey
            or DefinitionIssueCode.UnusedPlaceTag
            or DefinitionIssueCode.UnsatisfiableRequirement
            or DefinitionIssueCode.OneSidedWith
            or DefinitionIssueCode.UnmatchedWildcard
            or DefinitionIssueCode.PartnerMismatch
            or DefinitionIssueCode.ScheduleGap => IssueSeverity.Warning,
        _ => IssueSeverity.Error,
    };
}
