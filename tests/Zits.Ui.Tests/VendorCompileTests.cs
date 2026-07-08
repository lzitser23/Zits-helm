using System.Diagnostics;
using System.Text;

namespace Zits.Ui.Tests;

/// <summary>
/// Closure-compile guard for the copy-paste registry. For a representative subset of items,
/// runs the real CLI (<c>navius add</c>) into a throwaway Razor class library and compiles the
/// vendored output, so a consumer who runs <c>navius add &lt;item&gt;</c> gets code that builds.
/// This is the regression net for the missing-infrastructure / _Imports / Cn defects: the whole
/// 82-item registry was audited once by hand; this keeps the load-bearing paths honest.
/// </summary>
[Trait("Category", "VendorCompile")]
[Collection("cli-vendor")]
public class VendorCompileTests
{
    private static string CliProject => Path.Combine(RepoPaths.Root, "tools", "Navius.Cli", "Navius.Cli.csproj");

    // date-picker: styled + popover -> core (portal / overlay / _Imports).
    // toggle-group: brain roving-focus primitive + styled + its own variant type.
    // dialog: the canonical overlay closure (portal, focus trap, styled wrappers, cn).
    [Theory]
    [InlineData("date-picker")]
    [InlineData("toggle-group")]
    [InlineData("dialog")]
    public void Vendored_closure_compiles(string item)
    {
        var work = Path.Combine(Path.GetTempPath(), "navius-vendor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            File.WriteAllText(Path.Combine(work, "Vendor.csproj"), ScratchProject);

            var add = Run("dotnet",
                $"run --project \"{CliProject}\" -- add {item} --to \"{work}\" --root \"{RepoPaths.Root}\"");
            Assert.False(
                add.Output.Contains("missing source", StringComparison.Ordinal),
                $"navius add {item} reported a missing source:\n{Tail(add.Output)}");
            Assert.True(add.ExitCode == 0, $"navius add {item} failed:\n{Tail(add.Output)}");

            var build = Run("dotnet", $"build \"{work}\" -c Debug -v q");
            Assert.True(
                build.ExitCode == 0,
                $"Vendored '{item}' failed to compile:\n{Tail(build.Output)}");
        }
        finally
        {
            TryDelete(work);
        }
    }

    // The `cn` item must ship the canonical Cn.cs (with Merge/UserClass), not a stale copy.
    // 118+ styled components call Cn.Merge; a drifting duplicate silently breaks every one.
    [Fact]
    public void Cn_ships_the_canonical_source_of_truth()
    {
        var canonical = Path.Combine(RepoPaths.Root, "src", "Zits.Ui", "Cn.cs");
        Assert.True(File.Exists(canonical), "src/Zits.Ui/Cn.cs is missing.");

        var body = File.ReadAllText(canonical);
        Assert.Contains("namespace Zits.Ui;", body);
        Assert.Contains("public static IReadOnlyDictionary<string, object> Merge(", body);
        Assert.Contains("public static string? UserClass(", body);

        // The old drifting copy must stay gone (it had `namespace Navius;` and no Merge/UserClass).
        var stale = Path.Combine(RepoPaths.Root, "registry", "lib", "Cn.cs");
        Assert.False(File.Exists(stale), "Stale registry/lib/Cn.cs reappeared; the cn item must point at src/Zits.Ui/Cn.cs.");

        // And the registry must reference the canonical file, not a copy under registry/.
        var registry = File.ReadAllText(Path.Combine(RepoPaths.Root, "registry", "registry.json"));
        Assert.Contains("\"path\": \"src/Zits.Ui/Cn.cs\"", registry);
    }

    private const string ScratchProject =
        """
        <Project Sdk="Microsoft.NET.Sdk.Razor">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>Vendor</RootNamespace>
            <NoWarn>$(NoWarn);CS1591</NoWarn>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="8.0.0" />
            <PackageReference Include="TailwindMerge.NET" Version="1.4.0" />
          </ItemGroup>
        </Project>
        """;

    private static (int ExitCode, string Output) Run(string file, string args)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        var sb = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();
        return (p.ExitCode, sb.ToString());
    }

    private static string Tail(string text, int lines = 40)
    {
        var all = text.Replace("\r\n", "\n").Split('\n');
        return string.Join('\n', all.Skip(Math.Max(0, all.Length - lines)));
    }

    private static void TryDelete(string dir)
    {
        try { Directory.Delete(dir, recursive: true); }
        catch { /* best effort: a leftover temp dir is harmless. */ }
    }
}
