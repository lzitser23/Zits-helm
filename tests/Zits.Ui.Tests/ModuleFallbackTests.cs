namespace Zits.Ui.Tests;

/// <summary>
/// Guards the dual-mode JS module resolution. Both Navius (brain) and Zits.Ui (styled)
/// support package mode (static web assets under <c>_content/&lt;PackageId&gt;/</c>) and
/// vendored mode (the registry CLI copies the JS to the consumer's own wwwroot, served at
/// the app root). Every importer must try the package path first and fall back to the
/// root-relative vendored path, or vendored consumers silently lose all JS-driven behavior.
/// A source scan (no bUnit/JSInterop harness exists in this suite) keeps that pairing wired.
/// </summary>
public class ModuleFallbackTests
{
    private const string BrainPackagePath = "./_content/Navius.Primitives/navius-interop.js";
    private const string BrainVendoredPath = "./navius-interop.js";

    private const string ThemePackagePath = "./_content/Zits.Ui/zits-theme.js";
    private const string ThemeVendoredPath = "./zits-theme.js";

    /// <summary>
    /// The two primary services surface a loud error naming both paths and both modes when
    /// both imports fail (the verified silent no-op defect must not return).
    /// </summary>
    [Theory]
    [InlineData("navius/src/Navius.Primitives/Interop/NaviusJsInterop.cs", BrainPackagePath, BrainVendoredPath)]
    [InlineData("zits-helm/src/Zits.Ui/Theming/ZitsThemeService.cs", ThemePackagePath, ThemeVendoredPath)]
    public void Primary_services_resolve_package_then_vendored_and_surface_both_on_failure(
        string relativePath, string packagePath, string vendoredPath)
    {
        var source = ReadSibling(relativePath);

        AssertPackageThenVendored(source, packagePath, vendoredPath);
        Assert.Contains("(package mode)", source);
        Assert.Contains("(vendored mode)", source);
        Assert.Contains("InvalidOperationException", source);
    }

    /// <summary>
    /// The vendorable components that import the engine directly must carry the same
    /// package-then-vendored fallback, so vendored mode keeps focus/scroll/drag/measure
    /// behavior. These preserve their own graceful degradation, so no loud error is required.
    /// </summary>
    [Theory]
    [InlineData("navius/src/Navius.Primitives/Components/Form/NaviusForm.razor")]
    [InlineData("navius/src/Navius.Primitives/Components/Select/NaviusSelectViewport.razor")]
    [InlineData("navius/src/Navius.Primitives/Components/ScrollArea/NaviusScrollAreaViewport.razor")]
    [InlineData("navius/src/Navius.Primitives/Components/ScrollArea/NaviusScrollAreaThumb.razor")]
    [InlineData("navius/src/Navius.Primitives/Components/PasswordToggleField/NaviusPasswordToggleFieldToggle.razor")]
    [InlineData("navius/src/Navius.Primitives/Components/Slider/NaviusSlider.razor")]
    [InlineData("zits-helm/src/Zits.Ui/Components/ZitsResizableHandle.razor")]
    [InlineData("zits-helm/src/Zits.Ui/Components/ZitsSidebarProvider.razor")]
    public void Vendorable_components_resolve_package_then_vendored(string relativePath)
    {
        var source = ReadSibling(relativePath);
        AssertPackageThenVendored(source, BrainPackagePath, BrainVendoredPath);
    }

    private static void AssertPackageThenVendored(string source, string packagePath, string vendoredPath)
    {
        var package = source.IndexOf(packagePath, StringComparison.Ordinal);
        var vendored = source.IndexOf(vendoredPath, StringComparison.Ordinal);

        Assert.True(package >= 0, $"missing package path '{packagePath}'");
        Assert.True(vendored >= 0, $"missing vendored fallback path '{vendoredPath}'");
        Assert.True(package < vendored, "the package path must be tried before the vendored fallback");
    }

    /// <summary>Read a file addressed relative to the parent of the zits-helm repo root (its siblings).</summary>
    private static string ReadSibling(string relativePath)
    {
        var siblingsRoot = Path.GetFullPath(Path.Combine(RepoPaths.Root, ".."));
        var full = Path.GetFullPath(Path.Combine(siblingsRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return File.ReadAllText(full);
    }
}
