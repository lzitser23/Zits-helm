using Zits.Ui;

namespace Zits.Ui.Tests;

/// <summary>
/// The class-merge helper is shadcn parity (clsx + tailwind-merge): after joining and
/// de-duplicating, it resolves Tailwind conflicts so a consumer-supplied class reliably
/// beats a component's base class. These guard the verified width-override defect: against
/// a base <c>w-[280px]</c>, a consumer <c>w-36</c> must win, not lose to source order.
/// </summary>
public class CnTests
{
    [Fact]
    public void Class_resolves_width_conflict_to_a_single_token_consumer_wins()
    {
        var result = Cn.Class("w-[280px]", "w-36");

        var tokens = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(tokens, t => t.StartsWith("w-", StringComparison.Ordinal));
        Assert.Contains("w-36", tokens);
        Assert.DoesNotContain("w-[280px]", tokens);
    }

    [Fact]
    public void Class_last_conflicting_padding_token_wins()
    {
        Assert.Equal("p-4", Cn.Class("p-2", "p-4"));
    }

    [Fact]
    public void Class_keeps_non_conflicting_tokens_and_dedupes()
    {
        var result = Cn.Class("flex items-center", "flex gap-2");
        var tokens = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(tokens, t => t == "flex");
        Assert.Contains("items-center", tokens);
        Assert.Contains("gap-2", tokens);
    }

    [Fact]
    public void Merge_appends_consumer_class_so_it_beats_the_base_width()
    {
        var attributes = new Dictionary<string, object> { ["class"] = "w-36" };

        var merged = Cn.Merge(attributes, "inline-flex h-9 w-[280px] items-center");
        var classValue = (string)merged["class"];
        var tokens = classValue.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains("w-36", tokens);
        Assert.DoesNotContain("w-[280px]", tokens);
        Assert.Single(tokens, t => t.StartsWith("w-", StringComparison.Ordinal));
        // Non-conflicting base tokens survive the merge.
        Assert.Contains("inline-flex", tokens);
        Assert.Contains("items-center", tokens);
    }
}
