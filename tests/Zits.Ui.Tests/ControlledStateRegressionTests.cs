using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Navius.Primitives.Common;
using Navius.Primitives.Components.Menu;
using Navius.Primitives.Components.Popover;
using Zits.Ui;

namespace Zits.Ui.Tests;

// Regression cover for the "always-controlled wart": the Navius primitives derive
// controlled-ness from whether the state parameter was SUPPLIED, so a wrapper that
// forwards the state param unconditionally pins an uncontrolled consumer into
// controlled mode at the parameter's default and freezes the component.
//
// These render each wrapper WITHOUT its state parameter (pure uncontrolled usage),
// drive the interaction through the primitive, and assert the internal state actually
// advances and the *Changed observer fires. Before the conditional-forwarding fix both
// tests fail: the primitive sees the (default) state param, treats it as controlled,
// and the observed DOM state never leaves its frozen default.
public sealed class ControlledStateRegressionTests
{
    [Fact]
    public void Popover_uncontrolled_opens_on_trigger_click()
    {
        using var ctx = new Bunit.TestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        bool? observed = null;

        RenderFragment trigger = builder =>
        {
            builder.OpenComponent<NaviusPopoverTrigger>(0);
            builder.AddAttribute(1, "ChildContent",
                (RenderFragment)(b => b.AddContent(0, "toggle")));
            builder.CloseComponent();
        };

        // No Open / @bind-Open: this is uncontrolled usage.
        var cut = ctx.RenderComponent<ZitsPopover>(ps => ps
            .Add(p => p.OpenChanged, (bool v) => observed = v)
            .Add(p => p.ChildContent, trigger));

        var button = cut.Find("button[data-navius-popover-trigger]");
        Assert.Equal("false", button.GetAttribute("aria-expanded"));

        button.Click();

        // Uncontrolled: the primitive advances its own open state and echoes the change.
        Assert.Equal("true", cut.Find("button[data-navius-popover-trigger]").GetAttribute("aria-expanded"));
        Assert.True(observed);
    }

    [Fact]
    public void MenuCheckboxItem_uncontrolled_checks_on_click()
    {
        using var ctx = new Bunit.TestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        bool? observed = null;

        // No Checked / @bind-Checked: uncontrolled, seeded unchecked via DefaultChecked.
        // PreventDefault keeps the (absent) menu context out of the activation path.
        var cut = ctx.RenderComponent<ZitsMenuCheckboxItem>(ps => ps
            .Add(p => p.DefaultChecked, false)
            .Add(p => p.CheckedChanged, (bool? v) => observed = v)
            .Add(p => p.OnSelect, (NaviusSelectEventArgs a) => a.PreventDefault())
            .Add(p => p.ChildContent, (RenderFragment)(b => b.AddContent(0, "Show grid"))));

        var item = cut.Find("[role=menuitemcheckbox]");
        Assert.Equal("false", item.GetAttribute("aria-checked"));

        item.Click();

        // Uncontrolled: the primitive flips its own checked state and echoes the change.
        Assert.Equal("true", cut.Find("[role=menuitemcheckbox]").GetAttribute("aria-checked"));
        Assert.True(observed);
    }
}
