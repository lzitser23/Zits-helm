using Bunit;
using Microsoft.AspNetCore.Components.Web;

namespace Zits.Ui.Tests;

/// <summary>
/// The date-picker day grid must implement the ARIA grid keyboard pattern, not just
/// grid semantics: one roving tab stop for the whole grid, ArrowLeft/Right by day,
/// ArrowUp/Down by week, Home/End to week start/end, month-crossing moves the view.
/// Rendered through the real popover (bUnit loose JS interop absorbs the positioning
/// engine), so these tests exercise the exact markup a consumer gets.
/// </summary>
public class DatePickerGridTests : TestContext
{
    public DatePickerGridTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // 2026-01-15 is a Thursday; its Sunday-first week runs Jan 11 (Su) .. Jan 17 (Sa).
    private static readonly DateOnly Anchor = new(2026, 1, 15);

    private IRenderedComponent<ZitsDatePicker> RenderOpen(DateOnly? value)
    {
        var cut = RenderComponent<ZitsDatePicker>(ps => ps
            .Add(p => p.Value, value));
        cut.Find("button").Click(); // the trigger is the only button while closed
        return cut;
    }

    private static string DayAttr(DateOnly d) => d.ToString("yyyy-MM-dd");

    [Fact]
    public void Day_grid_has_a_single_roving_tab_stop()
    {
        var cut = RenderOpen(Anchor);

        var days = cut.FindAll("button[data-day]");
        Assert.True(days.Count >= 28, "expected a rendered month grid of day buttons");

        var tabbable = days.Where(d => d.GetAttribute("tabindex") == "0").ToList();
        var single = Assert.Single(tabbable);
        Assert.Equal(DayAttr(Anchor), single.GetAttribute("data-day"));
        Assert.All(
            days.Where(d => d.GetAttribute("data-day") != DayAttr(Anchor)),
            d => Assert.Equal("-1", d.GetAttribute("tabindex")));
    }

    [Theory]
    [InlineData("ArrowRight", "2026-01-16")] // +1 day
    [InlineData("ArrowLeft", "2026-01-14")]  // -1 day
    [InlineData("ArrowDown", "2026-01-22")]  // +1 week
    [InlineData("ArrowUp", "2026-01-08")]    // -1 week
    [InlineData("Home", "2026-01-11")]       // week start (Sunday)
    [InlineData("End", "2026-01-17")]        // week end (Saturday)
    public void Grid_keys_move_the_roving_focus_target(string key, string expectedDay)
    {
        var cut = RenderOpen(Anchor);

        cut.Find($"button[data-day='{DayAttr(Anchor)}']")
           .KeyDown(new KeyboardEventArgs { Key = key });

        var target = cut.Find($"button[data-day='{expectedDay}']");
        Assert.Equal("0", target.GetAttribute("tabindex"));
        Assert.Single(cut.FindAll("button[data-day]"), d => d.GetAttribute("tabindex") == "0");
    }

    [Fact]
    public void Arrow_across_the_month_boundary_moves_the_visible_month()
    {
        var last = new DateOnly(2026, 1, 31);
        var cut = RenderOpen(last);

        cut.Find($"button[data-day='{DayAttr(last)}']")
           .KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        var target = cut.Find("button[data-day='2026-02-01']");
        Assert.Equal("0", target.GetAttribute("tabindex"));
        Assert.Contains("February 2026", cut.Markup);
    }

    [Fact]
    public void Selecting_a_day_sets_the_value_and_closes_the_popover()
    {
        DateOnly? received = null;
        var cut = RenderComponent<ZitsDatePicker>(ps => ps
            .Add(p => p.Value, Anchor)
            .Add(p => p.ValueChanged, v => received = v));
        cut.Find("button").Click();

        cut.Find("button[data-day='2026-01-20']").Click();

        Assert.Equal(new DateOnly(2026, 1, 20), received);
        Assert.Empty(cut.FindAll("button[data-day]")); // popover content unmounted
    }
}
