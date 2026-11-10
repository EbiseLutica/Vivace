namespace Vivace.Core.Events;

/// <summary>イベントの出所。文字列にすると "sim"、"channel:misskey" の形になる。</summary>
/// <param name="Kind">大分類。</param>
/// <param name="Name">チャンネルかプラグインの名前。それ以外は null。</param>
public readonly record struct Origin(OriginKind Kind, string? Name = null)
{
    /// <summary>通常の運転の出所。</summary>
    public static Origin Sim => new(OriginKind.Sim);

    /// <summary>追いつきの出所。</summary>
    public static Origin Catchup => new(OriginKind.Catchup);

    /// <summary>テストの出所。</summary>
    public static Origin Test => new(OriginKind.Test);

    /// <summary>管理者の出所。</summary>
    public static Origin Admin => new(OriginKind.Admin);

    /// <summary>チャンネルの到着の出所。</summary>
    /// <param name="channel">チャンネルの種類。</param>
    public static Origin FromChannel(string channel) => new(OriginKind.Channel, channel);

    /// <summary>プラグインの出所。</summary>
    /// <param name="plugin">プラグインの名前。</param>
    public static Origin FromPlugin(string plugin) => new(OriginKind.Plugin, plugin);

    /// <summary>出所を文字列にする（"sim"、"channel:misskey" など）。</summary>
    public override string ToString() => Kind switch
    {
        OriginKind.Sim => "sim",
        OriginKind.Catchup => "catchup",
        OriginKind.Admin => "admin",
        OriginKind.Test => "test",
        OriginKind.Channel => $"channel:{Name}",
        OriginKind.Plugin => $"plugin:{Name}",
        _ => Kind.ToString(),
    };
}
