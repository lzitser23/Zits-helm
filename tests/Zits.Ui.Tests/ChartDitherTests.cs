namespace Zits.Ui.Tests;

public class ChartDitherTests
{
    [Fact]
    public void Chart_registry_ships_the_shared_pattern_renderer()
    {
        var registry = File.ReadAllText(Path.Combine(RepoPaths.Root, "registry", "registry.json"));

        Assert.Contains("src/Zits.Ui/Components/ZitsChartPatterns.razor", registry);
        Assert.Contains("Zits/Chart/ZitsChartPatterns.razor", registry);
        Assert.Contains("src/Zits.Ui/Components/ZitsChartSparkle.razor", registry);
    }

    [Fact]
    public void Filled_charts_use_the_shared_pattern_renderer()
    {
        var components = Path.Combine(RepoPaths.Root, "src", "Zits.Ui", "Components");
        foreach (var file in new[]
                 {
                     "ZitsAreaChart.razor",
                     "ZitsBarChart.razor",
                     "ZitsPieChart.razor",
                     "ZitsRadarChart.razor",
                 })
        {
            var source = File.ReadAllText(Path.Combine(components, file));
            Assert.Contains("<ZitsChartPatterns", source);
        }
    }

    [Fact]
    public void Dither_variants_match_the_upstream_fill_language()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoPaths.Root, "src", "Zits.Ui", "Components", "ZitsChart.cs"));

        Assert.Contains("Solid", source);
        Assert.Contains("Gradient", source);
        Assert.Contains("Dotted", source);
        Assert.Contains("Hatched", source);
    }

    [Fact]
    public void Chart_container_exposes_motion_bloom_and_selection_contracts()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoPaths.Root, "src", "Zits.Ui", "Components", "ZitsChartContainer.razor"));

        Assert.Contains("SelectedSeriesChanged", source);
        Assert.Contains("DefaultSelectedSeries", source);
        Assert.Contains("BloomOnHover", source);
        Assert.Contains("ReplayToken", source);
        Assert.Contains("prefers-reduced-motion", source);
    }
}
