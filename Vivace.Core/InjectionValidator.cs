using System.Diagnostics.CodeAnalysis;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;

namespace Vivace.Core;

/// <summary>
/// 投入の窓口の検査。時刻を決め、どの型にも共通の検査をしてから、その型のルールに検査を任せ、通ったらキューに入れる下書きを作る。
/// </summary>
public sealed class InjectionValidator
{
    /// <summary>ルールがない型、またはルールが投入を受け付けない型の拒否の理由。</summary>
    internal const string UnsupportedReason = "未対応の型";

    /// <summary>規則のコンテキスト。</summary>
    private readonly RuleContext _ctx;

    /// <summary>規則のコンテキストを受け取って作る。</summary>
    /// <param name="ctx">規則のコンテキスト。</param>
    public InjectionValidator(RuleContext ctx)
    {
        _ctx = ctx;
    }

    /// <summary>要求を検査する。通ったらキューに入れる下書きを、通らなければ拒否の理由を返す。</summary>
    /// <param name="state">いまの状態。</param>
    /// <param name="pending">未適用のイベント。</param>
    /// <param name="request">投入の要求。</param>
    /// <param name="now">エンジンが処理する時点の時計の時刻。</param>
    /// <param name="draft">通ったときの下書き。</param>
    /// <param name="reason">通らなかったときの理由。</param>
    /// <returns>通ったかどうか。</returns>
    public bool TryValidate(
        SimState state,
        IEnumerable<SimEvent> pending,
        InjectRequest request,
        DateTimeOffset now,
        [NotNullWhen(true)] out EventDraft? draft,
        [NotNullWhen(false)] out string? reason)
    {
        draft = null;
        var at = request.At ?? now;
        if (at < now)
        {
            reason = "過去の時刻";
            return false;
        }

        if (!EventApplier.Table.TryGetValue(request.EventType, out var rule))
        {
            reason = UnsupportedReason;
            return false;
        }

        reason = rule.CheckInjection(_ctx, state, pending, request.Payload, at);
        if (reason is not null)
        {
            return false;
        }

        draft = new EventDraft(at, request.EventType, null, null, request.Payload, Origin: request.Origin);
        return true;
    }
}
