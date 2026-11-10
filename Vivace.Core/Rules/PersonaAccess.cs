using Vivace.Core.Domain;
using Vivace.Core.Events;

namespace Vivace.Core.Rules;

/// <summary>ペルソナを状態から取り出し、状態へ差し替える。</summary>
internal static class PersonaAccess
{
    /// <summary>ペルソナが生きていれば取り出す。</summary>
    internal static bool TryPersona(SimState state, SimEvent ev, out PersonaState persona)
    {
        if (ev.PersonaId is { } id && state.Personas.TryGetValue(id, out var found) && found.Status == PersonaStatus.Alive)
        {
            persona = found;
            return true;
        }

        persona = null!;
        return false;
    }

    /// <summary>ペルソナの状態を差し替えた全体の状態を返す。</summary>
    internal static SimState Put(SimState state, PersonaState persona) =>
        state with { Personas = state.Personas.SetItem(persona.Id, persona) };
}
