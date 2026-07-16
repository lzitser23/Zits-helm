namespace Zits.Ui.Tests;

/// <summary>
/// Guards the fix for the "theme reverts on navigation" regression (issue #12). Blazor
/// enhanced navigation patches &lt;html&gt; to match the freshly server-rendered document on
/// every internal navigation, which silently strips the client-applied .dark class and all
/// data-zits-* attributes that zits-theme-init.js applied pre-paint. The init script must
/// re-run its restore logic after every enhanced navigation, not just once before first
/// paint, or the theme visually reverts the moment the user clicks an internal link. A
/// source scan (no JS runtime exists in this suite) keeps that invariant wired.
/// </summary>
public class ThemeInitScriptTests
{
    private static string Source => File.ReadAllText(
        Path.Combine(RepoPaths.Root, "src", "Zits.Ui", "wwwroot", "zits-theme-init.js"));

    /// <summary>
    /// Without an 'enhancedload' listener, enhanced navigation can silently wipe the
    /// client-applied theme again on the very next internal navigation.
    /// </summary>
    [Fact]
    public void Registers_enhancedload_listener_via_Blazor_addEventListener()
    {
        var source = Source;

        Assert.Contains("Blazor.addEventListener", source);
        Assert.Contains("'enhancedload'", source);
    }

    /// <summary>
    /// The restore logic must run twice: once immediately for the pre-paint contract, and
    /// again from the enhancedload registration so navigations re-apply the theme. Both
    /// call sites must reference the same named function, and the immediate call must come
    /// first (the function is declared and invoked before registration is even attempted).
    /// </summary>
    [Fact]
    public void Restore_function_runs_immediately_and_is_wired_to_enhancedload()
    {
        var source = Source;

        var declaration = source.IndexOf("function restore()", StringComparison.Ordinal);
        var immediateCall = source.IndexOf("restore();", StringComparison.Ordinal);
        var registration = source.IndexOf("addEventListener('enhancedload', restore)", StringComparison.Ordinal);

        Assert.True(declaration >= 0, "expected a named restore() function");
        Assert.True(immediateCall >= 0, "expected an immediate pre-paint call to restore()");
        Assert.True(registration >= 0, "expected restore to be registered for enhancedload");
        Assert.True(declaration < immediateCall, "restore() must be declared before it is called");
        Assert.True(immediateCall < registration, "the pre-paint call must happen before enhancedload registration");
    }

    /// <summary>
    /// The pre-paint contract requires this to stay a classic, synchronous script (loaded
    /// before first paint in &lt;head&gt;); an ES module would defer execution and reintroduce
    /// the flash-of-wrong-theme bug this file exists to prevent.
    /// </summary>
    [Fact]
    public void Stays_a_classic_script()
    {
        Assert.DoesNotContain("export ", Source);
    }
}
