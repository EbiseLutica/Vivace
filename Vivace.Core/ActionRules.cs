using System.Collections.Immutable;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Domain.State;
using Vivace.Core.Domain;
using Vivace.Core.Handlers;
using Vivace.Core.Time;

namespace Vivace.Core;

/// <summary>
/// 行動の候補の判定と、制限の判定。Decider と Apply の両方が使う。
/// </summary>
public static class ActionRules
{
    /// <summary>次の予定の場所へ向かう行動の ID。</summary>
    public const string GoToNextId = "core.go_to_next";

    /// <summary>好き（like）の倍率。</summary>
    public const double LikeFactor = 2.0;

    /// <summary>嫌い（dislike）の倍率。</summary>
    public const double DislikeFactor = 0.4;

    /// <summary>投稿をスキップしたあと、投稿の行動を候補にしない時間。</summary>
    public static readonly TimeSpan SkipCooldown = TimeSpan.FromMinutes(30);

    /// <summary>
    /// ペルソナが使える行動か。使う行動の一覧（<c>use</c>）の、完全一致かワイルドカード（"core.*"）に当たるかで見る。
    /// </summary>
    /// <param name="persona">ペルソナの定義。</param>
    /// <param name="actionId">行動の定義 ID。</param>
    public static bool IsUsable(PersonaDef persona, string actionId)
    {
        foreach (var pattern in persona.Use)
        {
            if (pattern == actionId)
            {
                return true;
            }

            if (pattern.EndsWith(".*", StringComparison.Ordinal)
                && actionId.StartsWith(pattern[..^1], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 行動の好みの倍率。行動 ID と行動タグの両方に当たれば積をとる。禁止（forbid、数値の 0）に当たれば null。
    /// </summary>
    /// <param name="persona">ペルソナの定義。</param>
    /// <param name="action">行動の定義。</param>
    public static double? PreferenceFactor(PersonaDef persona, ActionDef action)
    {
        var factor = 1.0;
        var keys = new List<string> { action.Id };
        keys.AddRange(action.Tags.Select(tag => "tag:" + tag));
        foreach (var key in keys)
        {
            if (!persona.Prefs.TryGetValue(key, out var preference))
            {
                continue;
            }

            switch (preference.Kind)
            {
                case PreferenceKind.Forbid:
                    return null;
                case PreferenceKind.Like:
                    factor *= LikeFactor;
                    break;
                case PreferenceKind.Dislike:
                    factor *= DislikeFactor;
                    break;
                default:
                    if (preference.Factor == 0)
                    {
                        return null;
                    }

                    factor *= preference.Factor;
                    break;
            }
        }

        return factor;
    }

    /// <summary>場所のタグと携行品が、行動の requires を満たすか。</summary>
    /// <param name="action">行動の定義。</param>
    /// <param name="persona">ペルソナの定義。</param>
    /// <param name="place">場所の定義。</param>
    public static bool PlaceAllows(ActionDef action, PersonaDef persona, PlaceDef place) =>
        action.Requires.ChannelCapability is null
        && action.Requires.PlaceTags.All(tag => place.Tags.Contains(tag, StringComparer.Ordinal))
        && action.Requires.Items.All(item => persona.Items.Contains(item, StringComparer.Ordinal));

    /// <summary>
    /// 制限（クールダウン、1日の回数、時刻の範囲）を満たすか。時刻は現地の時刻で見る。
    /// </summary>
    /// <param name="action">行動の定義。</param>
    /// <param name="usage">行動の利用の記録（なければ null）。</param>
    /// <param name="now">いまの時刻。</param>
    /// <param name="utcOffset">ワールドの時差。</param>
    public static bool WithinLimits(ActionDef action, ActionUsage? usage, DateTimeOffset now, TimeSpan utcOffset)
    {
        var limits = action.Limits;
        var local = LocalTimeHelper.ToLocal(now, utcOffset);
        if (limits.Cooldown is { } cooldown && usage?.LastStartedAt is { } last && now - last < cooldown)
        {
            return false;
        }

        if (limits.MaxPerDay is { } max)
        {
            var today = usage is { } u && u.CountDate == local.Date ? u.CountToday : 0;
            if (today >= max)
            {
                return false;
            }
        }

        if (limits.Hours is { } hours)
        {
            var inside = hours.Start <= hours.End
                ? local.Time >= hours.Start && local.Time < hours.End
                : local.Time >= hours.Start || local.Time < hours.End;
            if (!inside)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>行動を決めたときに実行できるか。ペルソナが場所にいて、行動が使えて、場所とつながり、制限を満たすこと。帰還できるかは見ない。</summary>
    /// <param name="ctx">規則のコンテキスト。</param>
    /// <param name="state">全体の状態。</param>
    /// <param name="persona">ペルソナの状態。</param>
    /// <param name="actionId">行動の定義 ID。</param>
    /// <param name="place">場所の ID。</param>
    /// <param name="now">いまの時刻（制限の判定に使う）。</param>
    public static bool CanChoose(RuleContext ctx, SimState state, PersonaState persona, string actionId, PlaceId place, DateTimeOffset now)
    {
        if (persona.Location is not AtPlace at
            || !ctx.Defs.Actions.TryGetValue(actionId, out var action))
        {
            return false;
        }

        var worldDefId = state.Worlds[persona.World].DefId;
        var world = ctx.Defs.Worlds[worldDefId];
        var minutes = ctx.Routes.For(worldDefId).Minutes(at.Place, place);
        if (minutes is null)
        {
            return false;
        }

        if (actionId == GoToNextId)
        {
            return world.Places.Any(p => p.Id == place.Value);
        }

        var personaDef = ctx.Defs.Personas[persona.DefId];
        if (action.ScheduleOnly
            || !IsUsable(personaDef, actionId)
            || !HasChannelFor(personaDef, action)
            || PreferenceFactor(personaDef, action) is null
            || world.Places.FirstOrDefault(p => p.Id == place.Value) is not { } placeDef
            || !PlaceAllows(action, personaDef, placeDef)
            || !WithinLimits(action, persona.Usage.GetValueOrDefault(actionId), now.AddMinutes(minutes.Value), world.UtcOffset)
            || !WithinPostingLimits(action, persona.Posting, now.AddMinutes(minutes.Value)))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 行動が投稿（効果が sns.post）なら、ペルソナにつながるチャンネルがあるか。投稿以外は常に満たす。
    /// </summary>
    /// <param name="persona">ペルソナの定義。</param>
    /// <param name="action">行動の定義。</param>
    public static bool HasChannelFor(PersonaDef persona, ActionDef action) =>
        action.Effects.Handler != SnsPostHandler.Name || !persona.Channels.IsDefaultOrEmpty;

    /// <summary>
    /// 投稿の行動（効果が sns.post）の、投稿の状態による制限を満たすか。最後のスキップから SkipCooldown 以内、
    /// または最後の投稿から行動の cooldown 以内なら満たさない。主の行動と副行動（スマホを開く機会）で同じに見る。投稿以外は常に満たす。
    /// </summary>
    /// <param name="action">行動の定義。</param>
    /// <param name="posting">投稿の状態。</param>
    /// <param name="now">候補の行動を始める時刻。</param>
    public static bool WithinPostingLimits(ActionDef action, PostingState posting, DateTimeOffset now)
    {
        if (action.Effects.Handler != SnsPostHandler.Name)
        {
            return true;
        }

        if (posting.LastSkippedAt is { } skipped && now - skipped < SkipCooldown)
        {
            return false;
        }

        return action.Limits.Cooldown is not { } cooldown
            || posting.LastPostAt is not { } last
            || now - last >= cooldown;
    }

    /// <summary>繰り返しの減衰の係数。直前に同じ行動を k 回続けていたら、decay の k 乗になる。</summary>
    /// <param name="decay">行動の repeat_decay。</param>
    /// <param name="k">直前に続けた回数。</param>
    public static double RepeatDecayFactor(double decay, int k)
    {
        var factor = 1.0;
        for (var i = 0; i < k; i++)
        {
            factor *= decay;
        }

        return factor;
    }
}
