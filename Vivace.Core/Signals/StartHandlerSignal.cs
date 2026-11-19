using Vivace.Core.Domain;

namespace Vivace.Core.Signals;

/// <summary>ハンドラを、レーンで起動する。ハンドラの種類ごとに派生する。</summary>
/// <param name="BatchSeq">出力のまとまりの連番（トリガーのイベントの連番）。</param>
/// <param name="Persona">ペルソナ。</param>
/// <param name="Horizon">ホライズン。出力が揃うまで、この時刻以上のイベントを適用しない。</param>
public abstract record StartHandlerSignal(long BatchSeq, PersonaId Persona, DateTimeOffset Horizon) : EngineSignal;
