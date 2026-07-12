using Bunit;

namespace Zits.Ui.Tests;

public class AlertTests : TestContext
{
    [Fact]
    public void Warning_variant_uses_the_warning_semantic_role()
    {
        var cut = RenderComponent<ZitsAlert>(parameters => parameters
            .Add(component => component.Variant, "warning")
            .AddChildContent("Review recommended"));

        var alert = cut.Find("[role='alert']");
        var classes = alert.ClassList;

        Assert.Contains("border-warning/50", classes);
        Assert.Contains("text-warning", classes);
        Assert.Contains("dark:border-warning", classes);
        Assert.Equal("Review recommended", alert.TextContent);
    }

    [Fact]
    public void Warning_tokens_cover_light_dark_forced_colors_and_tailwind_utilities()
    {
        var css = File.ReadAllText(RepoPaths.TokenStylesheet);

        Assert.Contains("--warning: oklch(0.554191 0.116908 75.008);", css);
        Assert.Contains("--warning-foreground: oklch(1 0 0);", css);
        Assert.Contains("--warning: oklch(0.754289 0.11595 78.624);", css);
        Assert.Contains("--warning-foreground: oklch(0.196091 0.004248 84.591);", css);
        Assert.Contains("--color-warning: var(--warning);", css);
        Assert.Contains("--color-warning-foreground: var(--warning-foreground);", css);
        Assert.Contains("@media (forced-colors: active)", css);
        Assert.Contains("--warning: CanvasText;", css);
        Assert.Contains("--warning-foreground: Canvas;", css);
    }
}
