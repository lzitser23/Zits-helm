using Bunit;

namespace Zits.Ui.Tests;

public class ChartDitherTests : TestContext
{
    [Fact]
    public void Chart_registry_ships_the_shared_pattern_renderer()
    {
        var registry = File.ReadAllText(Path.Combine(RepoPaths.Root, "registry", "registry.json"));

        Assert.Contains("src/Zits.Ui/Components/ZitsChartPatterns.razor", registry);
        Assert.Contains("Zits/Chart/ZitsChartPatterns.razor", registry);
        Assert.Contains("src/Zits.Ui/Components/ZitsChartSparkle.razor", registry);
    }

    /// <summary>
    /// A non-solid fill variant is only real if the shape ends up painted with the pattern:
    /// ZitsChartPatterns must emit a &lt;pattern&gt; for the variant, and the plotted shape
    /// must reference that exact id through fill="url(#...)". Rendering the container +
    /// chart together is what proves the two halves agree on the id.
    /// </summary>
    [Theory]
    [InlineData(ChartFillVariant.Gradient, "gradient")]
    [InlineData(ChartFillVariant.Dotted, "dotted")]
    [InlineData(ChartFillVariant.Hatched, "hatched")]
    public void Filled_bars_are_painted_with_the_pattern_the_renderer_emits(
        ChartFillVariant variant, string variantSlug)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var config = new ChartConfig
        {
            ["desktop"] = new ChartSeries("Desktop", "var(--chart-1)") { FillVariant = variant },
            ["mobile"] = new ChartSeries("Mobile", "var(--chart-2)"), // stays Solid
        };
        var data = new[]
        {
            new ChartDataPoint("Jan", ("desktop", 186), ("mobile", 80)),
            new ChartDataPoint("Feb", ("desktop", 305), ("mobile", 200)),
        };

        var cut = RenderComponent<ZitsChartContainer>(ps => ps
            .Add(p => p.Config, config)
            .AddChildContent<ZitsBarChart>(chart => chart
                .Add(p => p.Data, data)));

        var chartId = cut.Find("[data-chart]").GetAttribute("data-chart")!;
        var patternId = $"{chartId}-desktop-{variantSlug}";

        Assert.NotNull(cut.Find($"pattern[id='{patternId}']"));

        var filled = cut.FindAll("rect[data-series-key='desktop']");
        Assert.NotEmpty(filled);
        Assert.All(filled, bar => Assert.Equal($"url(#{patternId})", bar.GetAttribute("fill")));

        // The solid series keeps the plain colour var, so the pattern is variant-driven
        // rather than applied to everything the container renders.
        Assert.All(
            cut.FindAll("rect[data-series-key='mobile']"),
            bar => Assert.Equal("var(--color-mobile)", bar.GetAttribute("fill")));
    }
}
