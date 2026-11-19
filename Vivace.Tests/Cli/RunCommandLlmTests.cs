using Vivace.Cli;
using Vivace.Core.Time;
using Vivace.Testing;
using Vivace.Tests.Support;

namespace Vivace.Tests.Cli;

/// <summary>vivace run で LLM を作るタイミング。定義の検証が通ったあとだけ作り、通知もそのあとに出る。</summary>
public class RunCommandLlmTests
{
    /// <summary>定義のフォルダがなければ、LLM は作らず、LLM の通知も出さない。</summary>
    [Fact]
    public async Task MissingFolderDoesNotCreateLlmOrWriteNotice()
    {
        var calls = 0;
        var output = new StringWriter { NewLine = "\n" };

        var code = await RunCommand.RunAsync("no-such-definitions-folder", new VirtualClock(Scenario.Start), Scenario.WorldSeed, false, output, CancellationToken.None, llmFactory: writer =>
        {
            calls++;
            writer.WriteLine(LlmSetup.NoKeyNotice);
            return null;
        });

        Assert.Equal(1, code);
        Assert.Equal(0, calls);
        Assert.DoesNotContain(LlmSetup.NoKeyNotice, output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>定義の検証が通ったら LLM を作り、祝日の知らせのあとに、LLM の通知を書く。</summary>
    [Fact]
    public async Task ValidDefinitionsCreateLlmAfterHolidayNotice()
    {
        using var folder = new DefinitionFolder(TestWorld.Files());
        var calls = 0;
        var output = new StringWriter { NewLine = "\n" };

        var code = await RunCommand.RunAsync(folder.Path, new VirtualClock(Scenario.Start), Scenario.WorldSeed, false, output, CancellationToken.None, Scenario.Start.AddDays(1), llmFactory: writer =>
        {
            calls++;
            writer.WriteLine("LLM-NOTICE");
            return null;
        });

        var text = output.ToString();
        Assert.Equal(0, code);
        Assert.Equal(1, calls);
        Assert.True(text.IndexOf("祝日のカレンダー", StringComparison.Ordinal) < text.IndexOf("LLM-NOTICE", StringComparison.Ordinal));
    }
}
