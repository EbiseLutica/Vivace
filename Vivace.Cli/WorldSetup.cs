using Vivace.Core;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Domain;
using Vivace.Core.Events;

namespace Vivace.Cli;

/// <summary>定義からワールドとペルソナを生成する。dry-run と run で、同じ順と同じ投入にする。</summary>
public static class WorldSetup
{
    /// <summary>
    /// ワールドを ID の序数順、ペルソナを ID の序数順に投入する（Origin.Admin）。拒否されたら理由を書いて false。受け付けたら true。
    /// </summary>
    /// <param name="engine">投入先のエンジン。</param>
    /// <param name="definitions">定義。</param>
    /// <param name="seed">ワールドのシード（すべてのワールドに同じ値を使う）。</param>
    /// <param name="at">生成の時刻。null なら、エンジンが処理した時点の時計の時刻。</param>
    /// <param name="output">拒否の理由を書く先。</param>
    /// <param name="ct">キャンセル。</param>
    public static async Task<bool> InjectAsync(VivaceEngine engine, DefinitionPack definitions, ulong seed, DateTimeOffset? at, TextWriter output, CancellationToken ct)
    {
        foreach (var world in definitions.Worlds.Values.OrderBy(w => w.Id.Value, StringComparer.Ordinal))
        {
            if (!await Inject(engine, InjectRequest.WorldCreated(world.Id, seed, Origin.Admin, at), output, ct))
            {
                return false;
            }
        }

        foreach (var persona in definitions.Personas.Values.OrderBy(persona => persona.Id.Value, StringComparer.Ordinal))
        {
            if (!await Inject(engine, InjectRequest.PersonaCreated(persona.Id, new PlaceId(persona.Home), Origin.Admin, at), output, ct))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>祝日のカレンダーを指定したワールドについて、祝日なしで回すことを1行ずつ書く。ワールドは ID の序数順。</summary>
    /// <param name="definitions">定義。</param>
    /// <param name="output">出力先。</param>
    public static void WriteHolidayNotices(DefinitionPack definitions, TextWriter output)
    {
        foreach (var world in definitions.Worlds.Values.OrderBy(w => w.Id.Value, StringComparer.Ordinal))
        {
            if (world.HolidayCalendar is { } calendar)
            {
                output.WriteLine($"{world.Id.Value}: 祝日のカレンダー {calendar} は未対応のため、祝日なしで回した");
            }
        }
    }

    /// <summary>投入し、拒否されたら理由を書く。受け付けたら true。</summary>
    private static async Task<bool> Inject(VivaceEngine engine, InjectRequest request, TextWriter output, CancellationToken ct)
    {
        var result = await engine.InjectAsync(request, ct);
        if (result.Accepted)
        {
            return true;
        }

        output.WriteLine($"投入が拒否されました（{request.EventType}）: {result.Reason}");
        return false;
    }
}
