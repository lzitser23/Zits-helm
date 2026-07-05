namespace Zits.Ui.Theming;

public enum ZitsThemeMode { Light, Dark, System }

/// <summary>An immutable theme selection across the five preset dimensions plus mode.</summary>
public sealed record ZitsTheme(
    ZitsThemeMode Mode,
    string Base,
    string Primary,
    string Radius,
    string Font,
    string Style)
{
    public static ZitsTheme Default { get; } = new(ZitsThemeMode.System, "neutral", "ink", "md", "system", "standard");
}

public static class ZitsThemePresets
{
    public static readonly IReadOnlyList<string> Bases = ["slate", "gray", "zinc", "neutral", "stone"];
    public static readonly IReadOnlyList<string> Primaries =
        ["ink", "red", "orange", "amber", "yellow", "lime", "green", "emerald", "teal", "cyan",
         "sky", "blue", "indigo", "violet", "purple", "fuchsia", "pink", "rose"];
    public static readonly IReadOnlyList<string> Radii = ["none", "sm", "md", "lg", "xl"];
    public static readonly IReadOnlyList<string> Fonts = ["system", "grotesque", "humanist", "geometric", "rounded", "serif", "mono"];
    public static readonly IReadOnlyList<string> Styles = ["standard", "tinted", "soft", "contrast"];
}
