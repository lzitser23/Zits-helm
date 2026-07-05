using System.Globalization;
using System.Text;

namespace Zits.Ui.Theming;

/// <summary>
/// Generates <c>wwwroot/zits-theme.css</c>, the runtime theming token layer. A single
/// gray ramp (five Tailwind v4 families) is emitted once per <c>data-zits-base</c>;
/// style recipes map surface tokens from that ramp; primaries set only the brand
/// tokens; radius and font are orthogonal literal blocks. Because every value is a
/// custom property resolved at the element that uses it, the six dimensions compose
/// without multiplying, and a scoped subtree can carry its own full theme (including a
/// forced light/dark). Output is deterministic (fixed ordering, invariant culture):
/// run <c>Zits.Ui.CssGen</c> to regenerate the committed file, never hand-edit it.
///
/// <see cref="ThemeResolver"/> reimplements the same var/ramp/relative-color math in
/// pure C# so <see cref="GenerateCss(ZitsTheme)"/> can bake any single combination to a
/// self-contained shadcn-style <c>:root {} .dark {}</c> block (the copy-your-theme
/// export), and so the token contrast can be unit-tested across every combination.
/// </summary>
public static class ThemeStylesheet
{
    // The 20 surface tokens a style recipe sets, in the design-contract order. Style
    // blocks touch these and nothing else (never --destructive or chart 2..5).
    private static readonly string[] SurfaceOrder =
    [
        "--background", "--foreground",
        "--card", "--card-foreground",
        "--popover", "--popover-foreground",
        "--secondary", "--secondary-foreground",
        "--muted", "--muted-foreground",
        "--accent", "--accent-foreground",
        "--border", "--input",
        "--sidebar", "--sidebar-foreground",
        "--sidebar-accent", "--sidebar-accent-foreground",
        "--sidebar-border", "--sidebar-ring",
    ];

    // The full self-contained export order (surface tokens plus the brand set).
    private static readonly string[] ColorExportOrder =
    [
        "--background", "--foreground",
        "--card", "--card-foreground",
        "--popover", "--popover-foreground",
        "--primary", "--primary-foreground",
        "--secondary", "--secondary-foreground",
        "--muted", "--muted-foreground",
        "--accent", "--accent-foreground",
        "--border", "--input", "--ring",
        "--chart-1",
        "--sidebar", "--sidebar-foreground",
        "--sidebar-accent", "--sidebar-accent-foreground",
        "--sidebar-border", "--sidebar-ring",
    ];

    private const string MonoStack =
        "ui-monospace, 'Cascadia Code', 'Source Code Pro', Menlo, Consolas, 'DejaVu Sans Mono', monospace";

    private static readonly (string Slug, string Stack)[] FontStacks =
    [
        ("system", "system-ui, sans-serif"),
        ("grotesque", "Inter, Roboto, 'Helvetica Neue', 'Arial Nova', 'Nimbus Sans', Arial, sans-serif"),
        ("humanist", "Seravek, 'Gill Sans Nova', Ubuntu, Calibri, 'DejaVu Sans', source-sans-pro, sans-serif"),
        ("geometric", "Avenir, Montserrat, Corbel, 'URW Gothic', source-sans-pro, sans-serif"),
        ("rounded", "ui-rounded, 'Hiragino Maru Gothic ProN', Quicksand, Comfortaa, Manjari, 'Arial Rounded MT', 'Arial Rounded MT Bold', Calibri, source-sans-pro, sans-serif"),
        ("serif", "Charter, 'Bitstream Charter', 'Sitka Text', Cambria, serif"),
        ("mono", MonoStack),
    ];

    // Base radius (rem) per preset. The scale is derived from this by the §4.7 formula.
    private static readonly (string Slug, decimal Rem)[] BaseRadii =
    [
        ("none", 0m), ("sm", 0.375m), ("md", 0.625m), ("lg", 0.875m), ("xl", 1.125m),
    ];

    /// <summary>Generate the full stylesheet text (LF newlines, invariant culture).</summary>
    public static string Generate()
    {
        var sb = new StringBuilder();

        sb.Append(
            "/*\n" +
            " * zits-theme.css (GENERATED, do not edit by hand)\n" +
            " *\n" +
            " * Runtime theming tokens for the zits/ui styled layer: OKLCH gray ramps (five\n" +
            " * Tailwind v4 families), 18 primaries, 4 style recipes, 5 radii and 7 font\n" +
            " * stacks, all switched by data-zits-* attributes. A shared gray ramp is emitted\n" +
            " * once per base; style recipes map surface tokens from it and primaries are\n" +
            " * orthogonal, so the dimensions compose instead of multiplying. Values are\n" +
            " * computed in C# (Zits.Ui.Theming) and committed as a static asset, no Node at\n" +
            " * build time. Regenerate with:\n" +
            " *\n" +
            " *   dotnet run --project src/Zits.Ui.CssGen\n" +
            " */\n\n");

        AppendDefaultRamp(sb);
        AppendColorSchemes(sb);
        AppendBaseRamps(sb);
        AppendBaseStandalone(sb);
        AppendPrimaries(sb);
        AppendStyles(sb);
        AppendForcedLight(sb);
        AppendRadii(sb);
        AppendFonts(sb);

        return sb.ToString();
    }

    // --- Block 1.5: browser-native control scheme -------------------------------

    private static void AppendColorSchemes(StringBuilder sb)
    {
        sb.Append("/* Color scheme: keep browser-native controls in the active theme mode. */\n");
        sb.Append(":root,\n[data-zits-theme],\n[data-zits-mode=\"light\"] {\n");
        sb.Append("  color-scheme: light;\n");
        sb.Append("}\n\n");
        sb.Append(".dark,\n.dark [data-zits-theme],\n.dark[data-zits-theme],\n[data-zits-mode=\"dark\"] {\n");
        sb.Append("  color-scheme: dark;\n");
        sb.Append("}\n\n");
        sb.Append(".dark [data-zits-mode=\"light\"],\n.dark[data-zits-mode=\"light\"],\n[data-zits-mode=\"light\"] {\n");
        sb.Append("  color-scheme: light;\n");
        sb.Append("}\n\n");
    }

    // --- Block 1: default ramp (neutral) -----------------------------------------

    private static void AppendDefaultRamp(StringBuilder sb)
    {
        sb.Append("/* Default gray ramp (neutral). Every surface token resolves from these. */\n");
        AppendRamp(sb, ":root,\n[data-zits-theme]", "neutral");
    }

    // --- Block 2: per-base ramp blocks (mode-independent) -------------------------

    private static void AppendBaseRamps(StringBuilder sb)
    {
        sb.Append("/* Base ramps: switch the whole gray scale with data-zits-base (mode-independent). */\n");
        foreach (var b in ZitsThemePresets.Bases)
        {
            AppendRamp(sb, $"[data-zits-base=\"{b}\"]", b);
        }
    }

    private static void AppendRamp(StringBuilder sb, string selector, string family)
    {
        sb.Append(selector).Append(" {\n");
        sb.Append("  --zits-gray-0: ").Append(ThemePalette.White.Css).Append(";\n");
        foreach (var step in ThemePalette.Steps)
        {
            sb.Append("  --zits-gray-").Append(step.ToString(CultureInfo.InvariantCulture)).Append(": ")
                .Append(ThemePalette.Get(family, step).Css).Append(";\n");
        }
        sb.Append("}\n\n");
    }

    // --- Block 3: base standalone surface mapping (so data-zits-base alone works) -

    private static void AppendBaseStandalone(StringBuilder sb)
    {
        sb.Append("/* So data-zits-base alone paints surfaces: the standard recipe on the raw ramp. */\n");
        AppendStyleBlock(sb, "[data-zits-base]", StyleRecipes("standard", dark: false));
        AppendStyleBlock(
            sb,
            ".dark [data-zits-base],\n.dark[data-zits-base],\n[data-zits-base][data-zits-mode=\"dark\"]",
            StyleRecipes("standard", dark: true));
    }

    // --- Block 4: primaries (18 light, then 18 dark) -----------------------------

    private static void AppendPrimaries(StringBuilder sb)
    {
        sb.Append("/* Primaries: brand tokens only (--primary/-foreground/--ring/--chart-1). */\n");
        foreach (var p in ZitsThemePresets.Primaries)
        {
            AppendPrimaryBlock(sb, $"[data-zits-primary=\"{p}\"]", p, dark: false);
        }
        foreach (var p in ZitsThemePresets.Primaries)
        {
            AppendPrimaryBlock(
                sb,
                $".dark [data-zits-primary=\"{p}\"],\n.dark[data-zits-primary=\"{p}\"],\n[data-zits-primary=\"{p}\"][data-zits-mode=\"dark\"]",
                p,
                dark: true);
        }
    }

    private static void AppendPrimaryBlock(StringBuilder sb, string selector, string primary, bool dark)
    {
        var p = ResolvePrimary(primary, dark);
        sb.Append(selector).Append(" {\n");
        sb.Append("  --primary: ").Append(p.Primary).Append(";\n");
        sb.Append("  --primary-foreground: ").Append(p.Foreground).Append(";\n");
        sb.Append("  --ring: ").Append(p.Ring).Append(";\n");
        sb.Append("  --chart-1: ").Append(p.Chart1).Append(";\n");
        sb.Append("}\n\n");
    }

    // --- Block 5: style recipes (each: light then dark) --------------------------

    private static void AppendStyles(StringBuilder sb)
    {
        sb.Append("/* Style recipes: map the surface tokens. standard is emitted explicitly. */\n");
        foreach (var style in ZitsThemePresets.Styles)
        {
            AppendStyleBlock(sb, $"[data-zits-style=\"{style}\"]", StyleRecipes(style, dark: false));
            AppendStyleBlock(
                sb,
                $".dark [data-zits-style=\"{style}\"],\n.dark[data-zits-style=\"{style}\"],\n[data-zits-style=\"{style}\"][data-zits-mode=\"dark\"]",
                StyleRecipes(style, dark: true));
        }
    }

    // --- Block 6: forced-light re-assert (wins over dark by file order) -----------

    private static void AppendForcedLight(StringBuilder sb)
    {
        sb.Append("/* Forced light: a data-zits-mode=\"light\" scope inside a dark page stays light.\n");
        sb.Append(" * Same specificity as the dark blocks, emitted later so it wins the cascade. */\n");
        foreach (var style in ZitsThemePresets.Styles)
        {
            AppendStyleBlock(sb, $"[data-zits-mode=\"light\"][data-zits-style=\"{style}\"]", StyleRecipes(style, dark: false));
        }
        AppendStyleBlock(sb, "[data-zits-mode=\"light\"][data-zits-base]", StyleRecipes("standard", dark: false));
        foreach (var p in ZitsThemePresets.Primaries)
        {
            AppendPrimaryBlock(sb, $"[data-zits-primary=\"{p}\"][data-zits-mode=\"light\"]", p, dark: false);
        }
    }

    // --- Block 7: radius scale (mode-independent) --------------------------------

    private static void AppendRadii(StringBuilder sb)
    {
        sb.Append("/* Radius scale: --radius plus the sm/md/lg/xl steps derived from it. */\n");
        foreach (var (slug, _) in BaseRadii)
        {
            sb.Append($"[data-zits-radius=\"{slug}\"]").Append(" {\n");
            foreach (var (name, value) in RadiusTokens(slug))
            {
                sb.Append("  ").Append(name).Append(": ").Append(value).Append(";\n");
            }
            sb.Append("}\n\n");
        }
    }

    // --- Block 8: font stacks ----------------------------------------------------

    private static void AppendFonts(StringBuilder sb)
    {
        sb.Append("/* Font stacks: set --font-sans and apply it. */\n");
        foreach (var (slug, stack) in FontStacks)
        {
            sb.Append($"[data-zits-font=\"{slug}\"]").Append(" {\n");
            sb.Append("  --font-sans: ").Append(stack).Append(";\n");
            sb.Append("  font-family: var(--font-sans);\n");
            sb.Append("}\n\n");
        }
    }

    private static void AppendStyleBlock(StringBuilder sb, string selector, Recipe[] recipes)
    {
        sb.Append(selector).Append(" {\n");
        for (var i = 0; i < SurfaceOrder.Length; i++)
        {
            sb.Append("  ").Append(SurfaceOrder[i]).Append(": ").Append(RecipeCss(recipes[i])).Append(";\n");
        }
        sb.Append("}\n\n");
    }

    // === Public resolution + export ==============================================

    /// <summary>
    /// Resolve one theme in one mode to absolute token values (no var(), no oklch(from ..)).
    /// Keys are the 20 surface tokens plus the brand set (--primary, --primary-foreground,
    /// --ring, --chart-1). This is the programmatic token API behind the CSS export and the
    /// contrast tests; radius and font literals are added by <see cref="GenerateCss(ZitsTheme)"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Resolve(ZitsTheme theme, bool dark)
        => ThemeResolver.Resolve(theme, dark);

    /// <summary>
    /// Emit a self-contained <c>:root {} .dark {}</c> block for one theme selection: the
    /// copy-your-theme export. Both modes are always emitted (light resolved with mode=light,
    /// dark with mode=dark) regardless of <see cref="ZitsTheme.Mode"/>. Fully resolved, so it
    /// is valid standalone CSS against the zits-ui.css @theme inline mapping.
    /// </summary>
    public static string GenerateCss(ZitsTheme theme)
    {
        var light = Resolve(theme, dark: false);
        var dark = Resolve(theme, dark: true);
        var radius = RadiusTokens(theme.Radius);
        var fontStack = FontStackFor(theme.Font);

        var sb = new StringBuilder();
        sb.Append("/* zits/ui theme export. Drop in beside zits-ui.css (its @theme inline maps these). */\n");
        AppendExportBlock(sb, ":root", light, radius, fontStack);
        sb.Append('\n');
        AppendExportBlock(sb, ".dark", dark, radius, fontStack);
        return sb.ToString();
    }

    private static void AppendExportBlock(
        StringBuilder sb,
        string selector,
        IReadOnlyDictionary<string, string> colors,
        IReadOnlyList<(string Name, string Value)> radius,
        string fontStack)
    {
        sb.Append(selector).Append(" {\n");
        sb.Append("  color-scheme: ").Append(selector == ".dark" ? "dark" : "light").Append(";\n");
        foreach (var name in ColorExportOrder)
        {
            sb.Append("  ").Append(name).Append(": ").Append(colors[name]).Append(";\n");
        }
        foreach (var (name, value) in radius)
        {
            sb.Append("  ").Append(name).Append(": ").Append(value).Append(";\n");
        }
        sb.Append("  --font-sans: ").Append(fontStack).Append(";\n");
        sb.Append("  --font-mono: ").Append(MonoStack).Append(";\n");
        sb.Append("}\n");
    }

    private static string FontStackFor(string font)
    {
        foreach (var (slug, stack) in FontStacks)
        {
            if (slug == font)
            {
                return stack;
            }
        }
        return FontStacks[0].Stack;
    }

    // === Radius math (§4.7) ======================================================

    private static IReadOnlyList<(string Name, string Value)> RadiusTokens(string radius)
    {
        var r = BaseRadiusRem(radius);
        var sm = Math.Max(0m, r - 0.25m);
        var md = Math.Max(0m, r - 0.125m);
        var lg = r;
        var xl = r + 0.25m;
        return
        [
            ("--radius", Rem(r)),
            ("--radius-sm", Rem(sm)),
            ("--radius-md", Rem(md)),
            ("--radius-lg", Rem(lg)),
            ("--radius-xl", Rem(xl)),
        ];
    }

    private static decimal BaseRadiusRem(string radius)
    {
        foreach (var (slug, rem) in BaseRadii)
        {
            if (slug == radius)
            {
                return rem;
            }
        }
        return BaseRadii[2].Rem; // md
    }

    private static string Rem(decimal value)
        => value.ToString("0.############", CultureInfo.InvariantCulture) + "rem";

    // === Primary resolution ======================================================

    private readonly record struct ResolvedPrimary(
        string Primary, string Foreground, string Ring, string Chart1, string HueToken);

    private static ResolvedPrimary ResolvePrimary(string primary, bool dark)
    {
        if (primary == "ink")
        {
            return dark
                ? new ResolvedPrimary(
                    ThemePalette.InkDarkPrimary, ThemePalette.InkDarkPrimaryForeground,
                    ThemePalette.InkDarkRing, ThemePalette.InkDarkChart1, HueToken(ThemePalette.InkDarkPrimary))
                : new ResolvedPrimary(
                    ThemePalette.InkLightPrimary, ThemePalette.InkLightPrimaryForeground,
                    ThemePalette.InkLightRing, ThemePalette.InkLightChart1, HueToken(ThemePalette.InkLightPrimary));
        }

        var step = HueStep(primary, dark);
        var color = ThemePalette.Get(primary, step);
        // Foreground: a light primary needs a dark hue-950 text; otherwise near-white.
        var foreground = color.L >= 0.7 ? ThemePalette.Get(primary, 950).Css : "oklch(0.985 0 0)";
        return new ResolvedPrimary(color.Css, foreground, color.Css, color.Css, HueToken(color.Css));
    }

    private static int HueStep(string hue, bool dark)
        => hue is "yellow" or "amber" or "lime" ? 400 : (dark ? 500 : 600);

    /// <summary>The hue token (3rd component) of an "oklch(l c h)" string, for relative colors.</summary>
    private static string HueToken(string oklchCss)
    {
        var inner = oklchCss["oklch(".Length..].TrimEnd(')');
        var parts = inner.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts[2];
    }

    /// <summary>Inject an alpha into an "oklch(l c h)" string: "oklch(l c h / a)".</summary>
    private static string WithAlpha(string oklchCss, string alpha)
        => oklchCss[..^1] + " / " + alpha + ")";

    // === Style recipe model ======================================================

    private enum RecipeKind { Ramp, RampAlpha, FromPrimary, Ring, Literal }

    private readonly record struct Recipe(RecipeKind Kind, int Step, string A, string B, string Text)
    {
        public static Recipe Ramp(int step) => new(RecipeKind.Ramp, step, "", "", "");
        public static Recipe RampAlpha(int step, string alpha) => new(RecipeKind.RampAlpha, step, alpha, "", "");
        public static Recipe FromPrimary(string l, string c) => new(RecipeKind.FromPrimary, 0, l, c, "");
        public static readonly Recipe Ring = new(RecipeKind.Ring, 0, "", "", "");
        public static Recipe Lit(string css) => new(RecipeKind.Literal, 0, "", "", css);
    }

    private static string RecipeCss(Recipe r) => r.Kind switch
    {
        RecipeKind.Ramp => $"var(--zits-gray-{r.Step.ToString(CultureInfo.InvariantCulture)})",
        RecipeKind.RampAlpha => $"oklch(from var(--zits-gray-{r.Step.ToString(CultureInfo.InvariantCulture)}) l c h / {r.A})",
        RecipeKind.FromPrimary => $"oklch(from var(--primary) {r.A} {r.B} h)",
        RecipeKind.Ring => "var(--ring)",
        RecipeKind.Literal => r.Text,
        _ => throw new InvalidOperationException($"Unknown recipe kind {r.Kind}."),
    };

    private static Recipe[] StyleRecipes(string style, bool dark) => (style, dark) switch
    {
        ("standard", false) => StandardLight,
        ("standard", true) => StandardDark,
        ("tinted", false) => TintedLight,
        ("tinted", true) => TintedDark,
        ("soft", false) => SoftLight,
        ("soft", true) => SoftDark,
        ("contrast", false) => ContrastLight,
        ("contrast", true) => ContrastDark,
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown style."),
    };

    private static readonly Recipe[] StandardLight =
    [
        Recipe.Ramp(0), Recipe.Ramp(950),
        Recipe.Ramp(0), Recipe.Ramp(950),
        Recipe.Ramp(0), Recipe.Ramp(950),
        Recipe.Ramp(100), Recipe.Ramp(900),
        Recipe.Ramp(100), Recipe.Ramp(500),
        Recipe.Ramp(100), Recipe.Ramp(900),
        Recipe.Ramp(200), Recipe.Ramp(200),
        Recipe.Ramp(50), Recipe.Ramp(950),
        Recipe.Ramp(100), Recipe.Ramp(900),
        Recipe.Ramp(200), Recipe.Ring,
    ];

    private static readonly Recipe[] StandardDark =
    [
        Recipe.Ramp(950), Recipe.Ramp(50),
        Recipe.Ramp(900), Recipe.Ramp(50),
        Recipe.Ramp(900), Recipe.Ramp(50),
        Recipe.Ramp(800), Recipe.Ramp(50),
        Recipe.Ramp(800), Recipe.Ramp(400),
        Recipe.Ramp(800), Recipe.Ramp(50),
        Recipe.Lit("oklch(1 0 0 / 10%)"), Recipe.Lit("oklch(1 0 0 / 15%)"),
        Recipe.Ramp(900), Recipe.Ramp(50),
        Recipe.Ramp(800), Recipe.Ramp(50),
        Recipe.Lit("oklch(1 0 0 / 10%)"), Recipe.Ring,
    ];

    private static readonly Recipe[] TintedLight =
    [
        Recipe.FromPrimary("0.98", "0.006"), Recipe.FromPrimary("0.16", "0.015"),
        Recipe.FromPrimary("0.995", "0.004"), Recipe.FromPrimary("0.16", "0.015"),
        Recipe.FromPrimary("0.995", "0.004"), Recipe.FromPrimary("0.16", "0.015"),
        Recipe.FromPrimary("0.955", "0.012"), Recipe.FromPrimary("0.2", "0.02"),
        Recipe.FromPrimary("0.955", "0.012"), Recipe.FromPrimary("0.5", "0.02"),
        Recipe.FromPrimary("0.94", "0.02"), Recipe.FromPrimary("0.2", "0.02"),
        Recipe.FromPrimary("0.9", "0.015"), Recipe.FromPrimary("0.9", "0.015"),
        Recipe.FromPrimary("0.97", "0.008"), Recipe.FromPrimary("0.16", "0.015"),
        Recipe.FromPrimary("0.94", "0.02"), Recipe.FromPrimary("0.2", "0.02"),
        Recipe.FromPrimary("0.9", "0.015"), Recipe.Ring,
    ];

    private static readonly Recipe[] TintedDark =
    [
        Recipe.FromPrimary("0.16", "0.012"), Recipe.FromPrimary("0.97", "0.005"),
        Recipe.FromPrimary("0.205", "0.014"), Recipe.FromPrimary("0.97", "0.005"),
        Recipe.FromPrimary("0.205", "0.014"), Recipe.FromPrimary("0.97", "0.005"),
        Recipe.FromPrimary("0.26", "0.016"), Recipe.FromPrimary("0.97", "0.005"),
        Recipe.FromPrimary("0.26", "0.016"), Recipe.FromPrimary("0.68", "0.02"),
        Recipe.FromPrimary("0.3", "0.02"), Recipe.FromPrimary("0.97", "0.005"),
        Recipe.FromPrimary("0.32", "0.018"), Recipe.FromPrimary("0.32", "0.018"),
        Recipe.FromPrimary("0.205", "0.014"), Recipe.FromPrimary("0.97", "0.005"),
        Recipe.FromPrimary("0.3", "0.02"), Recipe.FromPrimary("0.97", "0.005"),
        Recipe.FromPrimary("0.32", "0.018"), Recipe.Ring,
    ];

    private static readonly Recipe[] SoftLight =
    [
        Recipe.Ramp(50), Recipe.Ramp(900),
        Recipe.Ramp(0), Recipe.Ramp(900),
        Recipe.Ramp(0), Recipe.Ramp(900),
        Recipe.Ramp(100), Recipe.Ramp(800),
        Recipe.Ramp(100), Recipe.Ramp(500),
        Recipe.Ramp(100), Recipe.Ramp(800),
        Recipe.RampAlpha(200, "70%"), Recipe.Ramp(100),
        Recipe.Ramp(100), Recipe.Ramp(900),
        Recipe.Ramp(200), Recipe.Ramp(800),
        Recipe.RampAlpha(200, "50%"), Recipe.Ring,
    ];

    private static readonly Recipe[] SoftDark =
    [
        Recipe.Ramp(900), Recipe.Ramp(100),
        Recipe.Ramp(800), Recipe.Ramp(100),
        Recipe.Ramp(800), Recipe.Ramp(100),
        Recipe.Ramp(800), Recipe.Ramp(200),
        Recipe.Ramp(800), Recipe.Ramp(400),
        Recipe.Ramp(800), Recipe.Ramp(200),
        Recipe.Lit("oklch(1 0 0 / 8%)"), Recipe.Lit("oklch(1 0 0 / 10%)"),
        Recipe.Ramp(800), Recipe.Ramp(100),
        Recipe.Ramp(700), Recipe.Ramp(200),
        Recipe.Lit("oklch(1 0 0 / 8%)"), Recipe.Ring,
    ];

    private static readonly Recipe[] ContrastLight =
    [
        Recipe.Ramp(0), Recipe.Lit("oklch(0 0 0)"),
        Recipe.Ramp(0), Recipe.Lit("oklch(0 0 0)"),
        Recipe.Ramp(0), Recipe.Lit("oklch(0 0 0)"),
        Recipe.Ramp(100), Recipe.Lit("oklch(0 0 0)"),
        Recipe.Ramp(100), Recipe.Ramp(600),
        Recipe.Ramp(100), Recipe.Lit("oklch(0 0 0)"),
        Recipe.Ramp(400), Recipe.Ramp(400),
        Recipe.Ramp(0), Recipe.Lit("oklch(0 0 0)"),
        Recipe.Ramp(100), Recipe.Lit("oklch(0 0 0)"),
        Recipe.Ramp(400), Recipe.Ring,
    ];

    private static readonly Recipe[] ContrastDark =
    [
        Recipe.Lit("oklch(0 0 0)"), Recipe.Ramp(50),
        Recipe.Lit("oklch(0.13 0 0)"), Recipe.Ramp(50),
        Recipe.Lit("oklch(0.13 0 0)"), Recipe.Ramp(50),
        Recipe.Ramp(800), Recipe.Ramp(50),
        Recipe.Ramp(800), Recipe.Ramp(300),
        Recipe.Ramp(800), Recipe.Ramp(50),
        Recipe.Lit("oklch(1 0 0 / 25%)"), Recipe.Lit("oklch(1 0 0 / 25%)"),
        Recipe.Lit("oklch(0 0 0)"), Recipe.Ramp(50),
        Recipe.Ramp(800), Recipe.Ramp(50),
        Recipe.Lit("oklch(1 0 0 / 25%)"), Recipe.Ring,
    ];

    // === Resolver ================================================================

    /// <summary>
    /// Pure-C# resolution of a theme to absolute token values, reimplementing the ramp
    /// lookups, the primary step + foreground rules, the style mappings, and the
    /// oklch(from ..) relative-color arithmetic. Kept in lockstep with the CSS emitted
    /// above (both drive off the same recipe tables).
    /// </summary>
    internal static class ThemeResolver
    {
        public static IReadOnlyDictionary<string, string> Resolve(ZitsTheme theme, bool dark)
        {
            var primary = ResolvePrimary(theme.Primary, dark);
            string GrayCss(int step) => step == 0 ? ThemePalette.White.Css : ThemePalette.Get(theme.Base, step).Css;

            var recipes = StyleRecipes(theme.Style, dark);
            var tokens = new Dictionary<string, string>(SurfaceOrder.Length + 4);
            for (var i = 0; i < SurfaceOrder.Length; i++)
            {
                tokens[SurfaceOrder[i]] = ResolveRecipe(recipes[i], GrayCss, primary);
            }

            tokens["--primary"] = primary.Primary;
            tokens["--primary-foreground"] = primary.Foreground;
            tokens["--ring"] = primary.Ring;
            tokens["--chart-1"] = primary.Chart1;
            return tokens;
        }

        private static string ResolveRecipe(Recipe r, Func<int, string> grayCss, ResolvedPrimary p) => r.Kind switch
        {
            RecipeKind.Ramp => grayCss(r.Step),
            RecipeKind.RampAlpha => WithAlpha(grayCss(r.Step), r.A),
            RecipeKind.FromPrimary => $"oklch({r.A} {r.B} {p.HueToken})",
            RecipeKind.Ring => p.Ring,
            RecipeKind.Literal => r.Text,
            _ => throw new InvalidOperationException($"Unknown recipe kind {r.Kind}."),
        };
    }
}
