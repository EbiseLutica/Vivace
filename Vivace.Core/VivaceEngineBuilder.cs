using Vivace.Core.Decision;
using Vivace.Core.Definitions;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Randomness;
using Vivace.Core.Storage;
using Vivace.Core.Time;

namespace Vivace.Core;

/// <summary>エンジンを組むビルダー。定義と保存先は必須で、ほかは省略すると既定のものを使う（時計は実時間の SystemClock）。</summary>
public sealed class VivaceEngineBuilder
{
    /// <summary>定義のフォルダ。定義そのものを渡したときは null。</summary>
    private string? _definitionDirectory;

    /// <summary>読み込み済みの定義。フォルダを渡したときは null。</summary>
    private DefinitionPack? _definitions;

    /// <summary>保存先。</summary>
    private IStorage? _storage;

    /// <summary>時計。省略すると実時間の SystemClock。</summary>
    private IClock? _clock;

    /// <summary>自由時間の決定。省略すると効用による決定。</summary>
    private IDecider? _decider;

    /// <summary>祝日のカレンダー。省略すると祝日のないカレンダー。</summary>
    private IHolidayCalendar? _holidays;

    /// <summary>乱数の列を開く実装。省略すると SplitMix。</summary>
    private IRandomSource? _random;

    /// <summary>空のビルダーを作る。VivaceEngine.Create から使う。</summary>
    internal VivaceEngineBuilder()
    {
    }

    /// <summary>ドライラン（外に何も出さない）として組むかどうか。</summary>
    internal bool IsDryRun { get; private set; }

    /// <summary>定義のフォルダを使う。読み込みは Build で行う。</summary>
    /// <param name="directory">定義のフォルダ。</param>
    public VivaceEngineBuilder UseDefinitions(string directory)
    {
        _definitionDirectory = directory;
        _definitions = null;
        return this;
    }

    /// <summary>読み込み済みの定義を使う。</summary>
    /// <param name="definitions">定義。</param>
    public VivaceEngineBuilder UseDefinitions(DefinitionPack definitions)
    {
        _definitions = definitions;
        _definitionDirectory = null;
        return this;
    }

    /// <summary>保存先を使う。</summary>
    /// <param name="storage">保存先。エンジンを破棄すると、一緒に破棄する。</param>
    public VivaceEngineBuilder UseStorage(IStorage storage)
    {
        _storage = storage;
        return this;
    }

    /// <summary>時計を使う。</summary>
    /// <param name="clock">時計。</param>
    public VivaceEngineBuilder UseClock(IClock clock)
    {
        _clock = clock;
        return this;
    }

    /// <summary>自由時間の決定を使う。</summary>
    /// <param name="decider">決定。</param>
    public VivaceEngineBuilder UseDecider(IDecider decider)
    {
        _decider = decider;
        return this;
    }

    /// <summary>祝日のカレンダーを使う。</summary>
    /// <param name="holidays">祝日のカレンダー。</param>
    public VivaceEngineBuilder UseHolidays(IHolidayCalendar holidays)
    {
        _holidays = holidays;
        return this;
    }

    /// <summary>乱数の列を開く実装を使う。</summary>
    /// <param name="random">乱数の列を開く実装。</param>
    public VivaceEngineBuilder UseRandom(IRandomSource random)
    {
        _random = random;
        return this;
    }

    /// <summary>ドライラン（外に何も出さない）として組む。</summary>
    public VivaceEngineBuilder DryRun()
    {
        IsDryRun = true;
        return this;
    }

    /// <summary>エンジンを組む。</summary>
    /// <exception cref="InvalidOperationException">定義がない、定義にエラーがある、保存先がない。</exception>
    public VivaceEngine Build()
    {
        var definitions = LoadDefinitions();
        var storage = _storage ?? throw new InvalidOperationException("ストレージが渡されていません。 UseStorage で指定してください。");
        var clock = ResolveClock();
        return new VivaceEngine(SimEngine.Create(definitions, storage, _random ?? new SplitMixRandomSource(), clock, _decider, _holidays));
    }

    /// <summary>使う時計を返す。渡されていなければ、実時間の SystemClock。</summary>
    internal IClock ResolveClock() => _clock ?? new SystemClock();

    /// <summary>指定された定義を返す。フォルダなら読み込み、エラーがあれば例外にする。</summary>
    private DefinitionPack LoadDefinitions()
    {
        if (_definitions is not null)
        {
            return _definitions;
        }

        if (_definitionDirectory is null)
        {
            throw new InvalidOperationException("定義がありません。 UseDefinitions で指定してください。");
        }

        var result = DefinitionLoader.LoadDirectory(_definitionDirectory);
        if (result.HasErrors || result.Definitions is null)
        {
            var errors = result.Issues.Where(issue => issue.Severity == IssueSeverity.Error).Select(issue => issue.ToString());
            throw new InvalidOperationException($"定義エラーです: {string.Join("; ", errors)}");
        }

        return result.Definitions;
    }
}
