using Vivace.Core;
using Vivace.Core.Definitions;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Time;
using Vivace.Testing;

namespace Vivace.Cli;

/// <summary>定義から仮想時計でエンジンを組み、開始から days 日回して行動表を書く。動いているデーモンには触らない。</summary>
public static class DryRunCommand
{
    /// <summary>
    /// 定義のフォルダを読み、ワールドとペルソナを生成して days 日回す。定義にエラーがあれば問題を書いて 1 を返す。
    /// </summary>
    /// <param name="dir">定義のフォルダ。</param>
    /// <param name="days">回す日数。</param>
    /// <param name="seed">ワールドのシード（すべてのワールドに同じ値を使う）。</param>
    /// <param name="start">開始の時刻。</param>
    /// <param name="output">出力先。</param>
    /// <param name="ct">キャンセル。</param>
    public static async Task<int> RunAsync(string dir, int days, ulong seed, DateTimeOffset start, TextWriter output, CancellationToken ct)
    {
        if (!TryEndOf(start, days, out var until))
        {
            output.WriteLine("開始と日数から終わりの時刻を計算できません。");
            return 1;
        }

        if (!Directory.Exists(dir))
        {
            return ValidateCommand.Run(dir, output);
        }

        var load = DefinitionLoader.LoadDirectory(dir);
        if (load.Definitions is not { } definitions)
        {
            return ValidateCommand.Write(load, output);
        }

        var storage = new InMemoryStorage();
        await using var engine = VivaceEngine.Create()
            .UseDefinitions(definitions)
            .UseStorage(storage)
            .UseClock(new VirtualClock(start))
            .DryRun()
            .Build();

        foreach (var world in definitions.Worlds.Values.OrderBy(w => w.Id.Value, StringComparer.Ordinal))
        {
            if (!await Inject(engine, InjectRequest.WorldCreated(world.Id, seed, Origin.Admin, start), output, ct))
            {
                return 1;
            }
        }

        foreach (var persona in definitions.Personas.Values.OrderBy(persona => persona.Id.Value, StringComparer.Ordinal))
        {
            if (!await Inject(engine, InjectRequest.PersonaCreated(persona.Id, new PlaceId(persona.Home), Origin.Admin, start), output, ct))
            {
                return 1;
            }
        }

        await engine.RunUntilAsync(until, ct);

        var offsets = (storage.Latest?.Worlds.Values ?? Enumerable.Empty<WorldState>()).ToDictionary(
            world => world.Id,
            world => definitions.Worlds[world.DefId].UtcOffset);
        foreach (var world in definitions.Worlds.Values.OrderBy(w => w.Id.Value, StringComparer.Ordinal))
        {
            if (world.HolidayCalendar is { } calendar)
            {
                output.WriteLine($"{world.Id.Value}: 祝日のカレンダー {calendar} は未対応のため、祝日なしで回した");
            }
        }

        output.Write(DryRunReport.Render(storage.Events, offsets));
        return 0;
    }

    /// <summary>開始から days 日後の時刻を求める。その1日後まで計算できなければ false（日付の計算が範囲の端で落ちないように）。</summary>
    private static bool TryEndOf(DateTimeOffset start, int days, out DateTimeOffset until)
    {
        try
        {
            until = start.AddDays(days);
            _ = until.AddDays(1);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            until = default;
            return false;
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
