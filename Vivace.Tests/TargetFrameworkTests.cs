using System.Reflection;
using System.Runtime.Versioning;
using Vivace.Tests.Support;

namespace Vivace.Tests;

/// <summary>
/// Directory.Build.props の共通設定が、テストのプロジェクトに効いていることを確かめる。
/// </summary>
public class TargetFrameworkTests
{
    /// <summary>
    /// テストのアセンブリの対象フレームワークを、スナップショットと比べる。
    /// </summary>
    [Fact]
    public Task TargetFrameworkIsNet10()
    {
        var attribute = typeof(TargetFrameworkTests).Assembly.GetCustomAttribute<TargetFrameworkAttribute>();
        return Snapshot.Object(new { attribute?.FrameworkName });
    }
}
