using System.Diagnostics;

namespace Zits.Ui.Tests;

/// <summary>
/// Covers <c>navius add &lt;name&gt; --styled-only</c>: it must vendor only the styled
/// zits/ui (+ lib) files and leave the headless Navius brain to the published
/// Navius.Primitives package, and the vendored styled set must compile against it.
/// </summary>
[Collection("cli-vendor")]
public class StyledOnlyVendorTests
{
    [Fact]
    public void Styled_only_vendors_the_styled_layer_and_compiles_against_the_brain_package()
    {
        using var scratch = new TempDir();

        var (exit, log) = RunCli(
            $"add date-picker --styled-only --to \"{scratch.Path}\" --root \"{RepoPaths.Root}\" --registry \"{RegistryPath}\"");
        Assert.True(exit == 0, "navius add --styled-only failed:\n" + Tail(log));

        // --- 1. The brain layer is NOT vendored -------------------------------------
        // Everything that lands under Navius/ (brain components plus the vendored-brain
        // glue) and the engine's navius-interop.js come from the Navius.Primitives package.
        Assert.False(
            File.Exists(Path.Combine(scratch.Path, "wwwroot", "navius-interop.js")),
            "navius-interop.js must not be copied in --styled-only mode (it ships in the package)");

        var naviusDir = Path.Combine(scratch.Path, "Navius");
        var underNavius = Directory.Exists(naviusDir)
            ? Directory.GetFiles(naviusDir, "*", SearchOption.AllDirectories)
            : Array.Empty<string>();
        Assert.Empty(underNavius);

        // --- 2. The styled layer IS vendored ----------------------------------------
        Assert.True(
            File.Exists(Path.Combine(scratch.Path, "Zits", "DatePicker", "ZitsDatePicker.razor")),
            "the styled ZitsDatePicker must be copied");
        Assert.True(
            File.Exists(Path.Combine(scratch.Path, "Zits", "Popover", "ZitsPopoverContent.razor")),
            "the styled popover dependency must be copied");
        Assert.True(
            File.Exists(Path.Combine(scratch.Path, "Zits", "Cn.cs")),
            "the cn() helper the styled files call must be copied (it is not in the package)");

        // --- 3. Compile-correctness against the brain package -----------------------
        // A scratch Razor class library holding the vendored styled files (which include
        // Zits/Cn.cs), referencing the Navius.Primitives project (equivalent to the NuGet
        // package, no network). No hand-authored _Imports.razor: the vendored
        // Zits/_Imports.razor (from registry/templates/ZitsImports.razor) must supply every
        // brain-component using the styled files need. RZ10012 is promoted to an error
        // because an unresolved <NaviusX> tag silently renders as literal HTML.
        File.WriteAllText(Path.Combine(scratch.Path, "ScratchStyled.csproj"), ScratchCsproj);

        var (buildExit, buildLog) = Run("dotnet", "build -c Debug -v q --nologo -warnaserror:RZ10012", scratch.Path);
        Assert.True(buildExit == 0, "vendored styled-only project failed to compile:\n" + Tail(buildLog));
        Assert.DoesNotContain("RZ10012", buildLog, StringComparison.Ordinal);
    }

    [Fact]
    public void Styled_only_cannot_be_combined_with_namespace()
    {
        using var scratch = new TempDir();

        var (exit, log) = RunCli(
            $"add date-picker --styled-only --namespace Foo.Ui --to \"{scratch.Path}\" --root \"{RepoPaths.Root}\" --registry \"{RegistryPath}\"");

        Assert.NotEqual(0, exit);
        Assert.Contains("--styled-only cannot be combined with --namespace", log);
        Assert.False(File.Exists(Path.Combine(scratch.Path, "Zits", "DatePicker", "ZitsDatePicker.razor")));
    }

    // --- fixtures ----------------------------------------------------------------

    private static string RegistryPath => Path.Combine(RepoPaths.Root, "registry", "registry.json");

    private static string CliProject => Path.Combine(RepoPaths.Root, "tools", "Navius.Cli", "Navius.Cli.csproj");

    private static string BrainProject => Path.GetFullPath(
        Path.Combine(RepoPaths.Root, "..", "navius", "src", "Navius.Primitives", "Navius.Primitives.csproj"));

    private static string ScratchCsproj =>
        $"""
        <Project Sdk="Microsoft.NET.Sdk.Razor">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>ScratchStyled</RootNamespace>
          </PropertyGroup>
          <ItemGroup>
            <ProjectReference Include="{BrainProject}" />
            <PackageReference Include="TailwindMerge.NET" Version="1.4.0" />
          </ItemGroup>
        </Project>
        """;

    private static (int ExitCode, string Output) RunCli(string args)
        => Run("dotnet", $"run --project \"{CliProject}\" -c Debug -- {args}", RepoPaths.Root);

    private static (int ExitCode, string Output) Run(string file, string args, string workingDir)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        if (!proc.WaitForExit(300_000))
        {
            proc.Kill(entireProcessTree: true);
            throw new TimeoutException($"'{file} {args}' did not exit within 300s");
        }

        return (proc.ExitCode, stdout + stderr);
    }

    private static string Tail(string log, int lines = 40)
    {
        var all = log.Replace("\r\n", "\n").Split('\n');
        return string.Join('\n', all[Math.Max(0, all.Length - lines)..]);
    }

    /// <summary>A throwaway working directory, removed on dispose.</summary>
    private sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "zits-styled-only-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Best-effort cleanup; a locked build artifact must not fail the test.
            }
        }
    }
}
