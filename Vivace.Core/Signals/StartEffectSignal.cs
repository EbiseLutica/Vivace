using Vivace.Core.Domain;

namespace Vivace.Core.Signals;

/// <summary>
/// 行動の効果のハンドラ（effects.handler）を、決める側のレーンで起動する。ホライズンと出力の時刻は行動の終わり。
/// </summary>
/// <param name="BatchSeq">出力のまとまりの連番（行動の開始の連番）。</param>
/// <param name="Persona">ペルソナ。</param>
/// <param name="Horizon">行動の終わりの時刻。出力の時刻になる。</param>
/// <param name="ActionId">行動の定義 ID。</param>
/// <param name="Place">行動の場所。</param>
/// <param name="ActivityVersion">行動の開始のときの行動のバージョン。出力の guard に使う。</param>
public sealed record StartEffectSignal(
    long BatchSeq,
    PersonaId Persona,
    DateTimeOffset Horizon,
    string ActionId,
    PlaceId Place,
    long ActivityVersion) : StartHandlerSignal(BatchSeq, Persona, Horizon);
