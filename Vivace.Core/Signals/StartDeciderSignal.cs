using Vivace.Core.Domain;
using Vivace.Core.Events;

namespace Vivace.Core.Signals;

/// <summary>決定タイミングのハンドラ（Decider）を、決める側のレーンで起動する。ホライズンはトリガーの時刻。</summary>
/// <param name="BatchSeq">出力のまとまりの連番（決定タイミングの連番と同じ）。</param>
/// <param name="Persona">ペルソナ。</param>
/// <param name="Now">トリガーの時刻。出力の時刻になる。</param>
/// <param name="Reason">決定タイミングの理由。</param>
/// <param name="BlockId">関係するブロック。</param>
public sealed record StartDeciderSignal(long BatchSeq, PersonaId Persona, DateTimeOffset Now, DecisionReason Reason, string? BlockId)
    : StartHandlerSignal(BatchSeq, Persona, Now);
