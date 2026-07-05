using System.Globalization;
using Zits.Ui.Theming;

namespace Zits.Ui.Tests;

public class ThemeStylesheetTests
{
    // --- 1. Determinism ----------------------------------------------------------

    [Fact]
    public void Generation_is_deterministic()
    {
        Assert.Equal(ThemeStylesheet.Generate(), ThemeStylesheet.Generate());
    }

    // --- 2. Culture independence -------------------------------------------------

    [Fact]
    public void Generation_is_culture_independent()
    {
        var invariant = ThemeStylesheet.Generate();
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal(invariant, ThemeStylesheet.Generate());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // --- 3. Drift guard vs the committed asset -----------------------------------

    [Fact]
    public void Committed_stylesheet_matches_the_generator()
    {
        // Regenerate with: dotnet run --project src/Zits.Ui.CssGen
        var committed = File.ReadAllText(RepoPaths.CommittedStylesheet);
        Assert.Equal(ThemeStylesheet.Generate(), committed);
    }

    // --- 4. Structure: every slug present, blocks in the pinned order ------------

    [Fact]
    public void Every_dimension_slug_appears_as_a_block()
    {
        var css = ThemeStylesheet.Generate();

        foreach (var b in ZitsThemePresets.Bases)
        {
            Assert.Contains($"[data-zits-base=\"{b}\"] {{", css);
        }
        foreach (var p in ZitsThemePresets.Primaries)
        {
            Assert.Contains($"[data-zits-primary=\"{p}\"] {{", css);
        }
        foreach (var r in ZitsThemePresets.Radii)
        {
            Assert.Contains($"[data-zits-radius=\"{r}\"] {{", css);
        }
        foreach (var f in ZitsThemePresets.Fonts)
        {
            Assert.Contains($"[data-zits-font=\"{f}\"] {{", css);
        }
        foreach (var s in ZitsThemePresets.Styles)
        {
            Assert.Contains($"[data-zits-style=\"{s}\"] {{", css);
        }

        // Forced-light re-assert blocks exist (style, base, primary variants).
        Assert.Contains("[data-zits-mode=\"light\"][data-zits-style=\"standard\"] {", css);
        Assert.Contains("[data-zits-mode=\"light\"][data-zits-base] {", css);
        Assert.Contains("[data-zits-primary=\"ink\"][data-zits-mode=\"light\"] {", css);
        Assert.Contains("color-scheme: dark;", css);
        Assert.Contains(".dark [data-zits-mode=\"light\"],", css);

        // Banner + regen command.
        Assert.Contains("GENERATED, do not edit by hand", css);
        Assert.Contains("dotnet run --project src/Zits.Ui.CssGen", css);
    }

    [Fact]
    public void Blocks_are_emitted_in_the_pinned_order()
    {
        var css = ThemeStylesheet.Generate();

        var defaultRamp = css.IndexOf(":root,\n[data-zits-theme] {", StringComparison.Ordinal);
        var baseRamp = css.IndexOf("[data-zits-base=\"slate\"] {", StringComparison.Ordinal);
        var baseStandalone = css.IndexOf("[data-zits-base] {", StringComparison.Ordinal);
        var primary = css.IndexOf("[data-zits-primary=\"ink\"] {", StringComparison.Ordinal);
        var style = css.IndexOf("[data-zits-style=\"standard\"] {", StringComparison.Ordinal);
        var forcedLight = css.IndexOf("[data-zits-mode=\"light\"][data-zits-style=\"standard\"] {", StringComparison.Ordinal);
        var radius = css.IndexOf("[data-zits-radius=\"none\"] {", StringComparison.Ordinal);
        var font = css.IndexOf("[data-zits-font=\"system\"] {", StringComparison.Ordinal);

        Assert.True(defaultRamp >= 0);
        Assert.True(defaultRamp < baseRamp, "default ramp before base ramps");
        Assert.True(baseRamp < baseStandalone, "base ramps before base standalone mapping");
        Assert.True(baseStandalone < primary, "base standalone before primaries");
        Assert.True(primary < style, "primaries before styles");
        Assert.True(style < forcedLight, "styles before forced-light");
        Assert.True(forcedLight < radius, "forced-light before radii");
        Assert.True(radius < font, "radii before fonts");
    }

    // --- 5. Ramp integrity -------------------------------------------------------

    [Fact]
    public void Every_gray_family_has_all_twelve_steps()
    {
        foreach (var family in ZitsThemePresets.Bases)
        {
            // Step 0 is the synthetic white; steps 50..950 come from the palette.
            Assert.Equal("oklch(1 0 0)", ThemePalette.White.Css);
            foreach (var step in ThemePalette.Steps)
            {
                var color = ThemePalette.Get(family, step);
                Assert.False(string.IsNullOrWhiteSpace(color.Css), $"{family}-{step} missing");
            }
            Assert.Equal(11, ThemePalette.Steps.Count); // 11 + white = 12 ramp steps
        }
    }

    [Fact]
    public void Every_palette_value_parses_as_oklch_invariant()
    {
        foreach (var family in ThemePalette.Families.Values)
        {
            foreach (var color in family.Values)
            {
                var (l, c, h) = ParseOklch(color.Css);
                Assert.InRange(l, 0.0, 1.0);
                Assert.True(c >= 0.0, "chroma is non-negative");
                Assert.InRange(h, 0.0, 360.0);
            }
        }

        var white = ParseOklch(ThemePalette.White.Css);
        Assert.Equal(1.0, white.L);
    }

    // --- 6. Contrast matrix (4 styles x 5 bases x 18 primaries x 2 modes) --------

    [Fact]
    public void Contrast_holds_across_every_combination()
    {
        var checkedCombos = 0;
        foreach (var style in ZitsThemePresets.Styles)
        {
            foreach (var @base in ZitsThemePresets.Bases)
            {
                foreach (var primary in ZitsThemePresets.Primaries)
                {
                    foreach (var dark in new[] { false, true })
                    {
                        var mode = dark ? ZitsThemeMode.Dark : ZitsThemeMode.Light;
                        var theme = new ZitsTheme(mode, @base, primary, "md", "system", style);
                        var t = ThemeStylesheet.Resolve(theme, dark);
                        var label = $"style={style} base={@base} primary={primary} mode={(dark ? "dark" : "light")}";

                        AssertDelta(t, "--background", "--foreground", 0.5, label);
                        AssertDelta(t, "--card", "--card-foreground", 0.5, label);
                        AssertDelta(t, "--muted", "--muted-foreground", 0.25, label);
                        AssertDelta(t, "--primary", "--primary-foreground", 0.25, label);
                        AssertDelta(t, "--secondary", "--secondary-foreground", 0.35, label);
                        checkedCombos++;
                    }
                }
            }
        }

        Assert.Equal(4 * 5 * 18 * 2, checkedCombos);
    }

    private static void AssertDelta(
        IReadOnlyDictionary<string, string> tokens, string a, string b, double minimum, string label)
    {
        var la = ParseOklch(tokens[a]).L;
        var lb = ParseOklch(tokens[b]).L;
        var delta = Math.Abs(la - lb);
        Assert.True(
            delta >= minimum,
            $"{label}: |L({a})={la:0.###} - L({b})={lb:0.###}| = {delta:0.###} < {minimum}");
    }

    // --- 7. GenerateCss export ---------------------------------------------------

    [Fact]
    public void GenerateCss_is_a_self_contained_resolved_block()
    {
        var theme = new ZitsTheme(ZitsThemeMode.System, "slate", "blue", "lg", "serif", "tinted");
        var css = ThemeStylesheet.GenerateCss(theme);

        Assert.Contains(":root {", css);
        Assert.Contains(".dark {", css);
        Assert.Contains(":root {\n  color-scheme: light;", css);
        Assert.Contains(".dark {\n  color-scheme: dark;", css);

        // Every §5 surface token is present, plus the brand + radius + font vars.
        foreach (var token in SurfaceTokens)
        {
            Assert.Contains(token + ":", css);
        }
        Assert.Contains("--primary:", css);
        Assert.Contains("--primary-foreground:", css);
        Assert.Contains("--ring:", css);
        Assert.Contains("--chart-1:", css);
        Assert.Contains("--radius:", css);
        Assert.Contains("--font-sans:", css);
        Assert.Contains("--font-mono:", css);

        // Fully resolved: no var() references and no unresolved relative colors.
        Assert.DoesNotContain("var(", css);
        Assert.DoesNotContain("oklch(from", css);

        // Deterministic.
        Assert.Equal(css, ThemeStylesheet.GenerateCss(theme));
    }

    [Fact]
    public void GenerateCss_emits_both_modes_regardless_of_theme_mode()
    {
        // theme.Mode is Light, but the export still carries a resolved .dark block.
        var theme = ZitsTheme.Default with { Mode = ZitsThemeMode.Light };
        var css = ThemeStylesheet.GenerateCss(theme);
        Assert.Contains(":root {", css);
        Assert.Contains(".dark {", css);
    }

    // --- 8. Radius math (§4.7 formula) ------------------------------------------

    [Fact]
    public void Radius_scale_matches_the_formula_for_every_preset()
    {
        var css = ThemeStylesheet.Generate();

        var baseRem = new Dictionary<string, decimal>
        {
            ["none"] = 0m,
            ["sm"] = 0.375m,
            ["md"] = 0.625m,
            ["lg"] = 0.875m,
            ["xl"] = 1.125m,
        };

        foreach (var (slug, r) in baseRem)
        {
            var sm = Math.Max(0m, r - 0.25m);
            var md = Math.Max(0m, r - 0.125m);
            var lg = r;
            var xl = r + 0.25m;

            var expected =
                $"[data-zits-radius=\"{slug}\"] {{\n" +
                $"  --radius: {Rem(r)};\n" +
                $"  --radius-sm: {Rem(sm)};\n" +
                $"  --radius-md: {Rem(md)};\n" +
                $"  --radius-lg: {Rem(lg)};\n" +
                $"  --radius-xl: {Rem(xl)};\n" +
                "}";

            Assert.Contains(expected, css);
        }
    }

    // --- helpers -----------------------------------------------------------------

    private static readonly string[] SurfaceTokens =
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

    private static string Rem(decimal value)
        => value.ToString("0.############", CultureInfo.InvariantCulture) + "rem";

    /// <summary>Parse an "oklch(l c h[ / a])" string into its L, C, H components.</summary>
    private static (double L, double C, double H) ParseOklch(string value)
    {
        var inner = value["oklch(".Length..].TrimEnd(')');
        var slash = inner.IndexOf('/');
        if (slash >= 0)
        {
            inner = inner[..slash];
        }
        var parts = inner.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var l = double.Parse(parts[0], CultureInfo.InvariantCulture);
        var c = double.Parse(parts[1], CultureInfo.InvariantCulture);
        var h = double.Parse(parts[2], CultureInfo.InvariantCulture);
        return (l, c, h);
    }
}
