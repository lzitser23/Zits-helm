using System.Reflection;
using System.Text.Json;

// navius - a minimal copy-paste component registry CLI for Blazor (Navius).
//
//   navius list
//   navius add <name> [--to <dir>] [--namespace <ns>] [--root <repo>] [--registry <path>]
//
// `add` resolves registryDependencies (so `add dialog` also brings `core`),
// copies each item's files into the target, and, when --namespace is given,
// rewrites the Navius.Primitives root namespace. This is the "you own the code"
// model: after `add`, the files are yours to edit.

string root = Directory.GetCurrentDirectory();
string? to = null;
string? ns = null;
string? registryPath = null;
var styledOnly = false;
var overwrite = false;
var packageRoot = AppContext.BaseDirectory;
var rootExplicit = false;
var positional = new List<string>();

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--to": to = args[++i]; break;
        case "--namespace": ns = args[++i]; break;
        case "--styled-only": styledOnly = true; break;
        case "--overwrite": overwrite = true; break;
        case "--root":
            root = Path.GetFullPath(args[++i]);
            rootExplicit = true;
            break;
        case "--registry":
            registryPath = Path.GetFullPath(args[++i]);
            break;
        default: positional.Add(args[i]); break;
    }
}

if (registryPath is null)
{
    var workingRegistry = Path.Combine(root, "registry", "registry.json");
    var bundledRegistry = Path.Combine(packageRoot, "registry", "registry.json");

    if (File.Exists(workingRegistry))
    {
        registryPath = workingRegistry;
    }
    else if (File.Exists(bundledRegistry))
    {
        registryPath = bundledRegistry;
        if (!rootExplicit)
        {
            root = packageRoot;
        }
    }
    else
    {
        registryPath = workingRegistry;
    }
}

if (positional.Count == 0)
{
    PrintUsage();
    return 1;
}

if (!File.Exists(registryPath))
{
    Console.Error.WriteLine($"registry not found: {registryPath}");
    return 1;
}

using var doc = JsonDocument.Parse(File.ReadAllText(registryPath));
var root_ = doc.RootElement;
var byName = new Dictionary<string, JsonElement>();
foreach (var it in root_.GetProperty("items").EnumerateArray())
{
    byName[it.GetProperty("name").GetString()!] = it;
}

string command = positional[0];

switch (command)
{
    case "list":
        Console.WriteLine($"registry: {root_.GetProperty("name").GetString()}");
        foreach (var it in root_.GetProperty("items").EnumerateArray())
        {
            var deps = it.TryGetProperty("registryDependencies", out var d) && d.GetArrayLength() > 0
                ? "  <- " + string.Join(", ", d.EnumerateArray().Select(x => x.GetString()))
                : "";
            Console.WriteLine($"  {it.GetProperty("name").GetString(),-10} {it.GetProperty("type").GetString(),-14}{deps}");
        }
        return 0;

    case "add":
        if (positional.Count < 2)
        {
            Console.Error.WriteLine("usage: navius add <name> [--to <dir>] [--namespace <ns>] [--styled-only] [--overwrite]");
            return 1;
        }
        if (styledOnly && ns is not null)
        {
            Console.Error.WriteLine(
                "--styled-only cannot be combined with --namespace: the namespace rewrite retargets " +
                "the Navius.Primitives root namespace, which only makes sense when you own (vendor) the " +
                "brain code. In --styled-only the brain stays as the published Navius.Primitives package.");
            return 1;
        }
        return Add(positional[1]);

    default:
        PrintUsage();
        return 1;
}

int Add(string name)
{
    to ??= Directory.GetCurrentDirectory();

    // Topologically resolve registryDependencies (deps first).
    var resolved = new List<string>();
    var seen = new HashSet<string>();

    bool Resolve(string n)
    {
        if (seen.Contains(n)) return true;
        if (!byName.TryGetValue(n, out var item))
        {
            Console.Error.WriteLine($"unknown item: {n}");
            return false;
        }
        seen.Add(n);
        if (item.TryGetProperty("registryDependencies", out var deps))
        {
            foreach (var dep in deps.EnumerateArray())
            {
                if (!Resolve(dep.GetString()!)) return false;
            }
        }
        resolved.Add(n);
        return true;
    }

    if (!Resolve(name)) return 1;

    int copied = 0;
    int missing = 0;
    int skipped = 0;
    int existing = 0;
    var nugetPackages = new SortedSet<string>(StringComparer.Ordinal);
    foreach (var n in resolved)
    {
        var item = byName[n];
        if (item.TryGetProperty("dependencies", out var pkgs))
        {
            foreach (var pkg in pkgs.EnumerateArray())
            {
                nugetPackages.Add(pkg.GetString()!);
            }
        }
        Console.WriteLine($"{n}:");
        foreach (var f in item.GetProperty("files").EnumerateArray())
        {
            var srcRel = f.GetProperty("path").GetString()!;
            var tgtRel = f.GetProperty("target").GetString()!;

            // --styled-only: skip the whole brain layer (the files that land under Navius/,
            // plus the engine's navius-interop.js) so it is consumed as the Navius.Primitives
            // package instead of vendored. The styled Zits/ files (including the cn() helper
            // and _Imports) still copy. Both a target and a source test are needed: the brain
            // glue templates (Navius/_Imports.razor, Navius/ServiceCollectionExtensions.cs)
            // are sourced from registry/, while navius-interop.js targets wwwroot/.
            if (styledOnly &&
                (tgtRel.StartsWith("Navius/", StringComparison.Ordinal) ||
                 srcRel.StartsWith("../navius/", StringComparison.Ordinal)))
            {
                Console.WriteLine($"  - {tgtRel} (brain; from the Navius.Primitives package)");
                skipped++;
                continue;
            }

            var src = ResolveSourcePath(srcRel);
            var dst = Path.Combine(to, tgtRel.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(src))
            {
                Console.Error.WriteLine($"  ! missing source: {srcRel} (resolved to {src})");
                missing++;
                continue;
            }

            // "You own the code": never clobber a file the consumer may have edited.
            // Skip existing targets unless --overwrite is given.
            if (File.Exists(dst) && !overwrite)
            {
                Console.WriteLine($"  ~ skipped (exists): {tgtRel}");
                existing++;
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            var text = File.ReadAllText(src);
            if (ns is not null)
            {
                // Rewrite the root namespace, but preserve the interop module's static
                // web asset path (./_content/Navius.Primitives/...): that literal names
                // the brain RCL on disk, not the consumer's namespace, so rewriting it
                // would 404 the JS import.
                const string keep = "_content/Navius.Primitives/";
                const string sentinel = "￿org.navius._content￿";
                text = text.Replace(keep, sentinel);
                text = text.Replace("Navius.Primitives", ns);
                text = text.Replace(sentinel, keep);
            }
            File.WriteAllText(dst, text);
            Console.WriteLine($"  + {tgtRel}");
            copied++;
        }
    }

    Console.WriteLine($"\nAdded '{name}' - {resolved.Count} item(s), {copied} file(s)" +
        (styledOnly ? $", {skipped} brain file(s) skipped" : "") +
        (existing > 0 ? $", {existing} existing file(s) skipped (use --overwrite to replace)" : "") +
        $" -> {to}");

    if (nugetPackages.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Required NuGet package(s):");
        foreach (var pkg in nugetPackages)
        {
            Console.WriteLine($"  dotnet add package {pkg}");
        }
    }

    if (styledOnly)
    {
        PrintStyledOnlyGuidance();
    }

    return missing == 0 ? 0 : 1;
}

// Guidance for --styled-only: the brain is consumed as the Navius.Primitives
// package instead of being vendored, so the consumer wires up three things.
void PrintStyledOnlyGuidance()
{
    // The tool and the brain package ship from this repo on the same version, so the
    // running tool's own version is the matching brain version. Fall back to a marker.
    var informational = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    var version = informational is null ? "<latest>" : informational.Split('+')[0];

    Console.WriteLine();
    Console.WriteLine("--styled-only: the Navius brain was not vendored. Consume it as a package:");
    Console.WriteLine();
    Console.WriteLine("  1. Reference the brain package (or the latest published version):");
    Console.WriteLine($"       <PackageReference Include=\"Navius.Primitives\" Version=\"{version}\" />");
    Console.WriteLine();
    Console.WriteLine("  2. Register the brain services at startup (Program.cs):");
    Console.WriteLine("       builder.Services.AddNavius();   // Navius.Primitives namespace");
    Console.WriteLine("     Mount a single <NaviusPortalOutlet /> near the app root (plus");
    Console.WriteLine("     <NaviusToastProvider> + <NaviusToastViewport /> if you use toasts).");
    Console.WriteLine();
    Console.WriteLine("  3. JS interop: nothing to wire. navius-interop.js ships in the package as a");
    Console.WriteLine("     static web asset and loads automatically from");
    Console.WriteLine("     _content/Navius.Primitives/navius-interop.js (the path NaviusJsInterop.cs");
    Console.WriteLine("     imports). No <script> tag or file copy is needed.");
    Console.WriteLine();
    Console.WriteLine("  Styling: the styled layer still needs the zits/ui theme tokens (CSS variables");
    Console.WriteLine("  like --popover, --primary) and Tailwind. The cn() helper was vendored for you");
    Console.WriteLine("  (Zits/Cn.cs); the theme stylesheet and Tailwind setup are not part of `add`.");
}

void PrintUsage()
{
    Console.WriteLine("navius - copy-paste component registry for Blazor (Navius)");
    Console.WriteLine();
    Console.WriteLine("  navius list");
    Console.WriteLine("  navius add <name> [--to <dir>] [--namespace <ns>] [--styled-only] [--overwrite] [--root <repo>] [--registry <path>]");
    Console.WriteLine();
    Console.WriteLine("  --styled-only  copy only the styled zits/ui + lib files; consume the Navius");
    Console.WriteLine("                 headless brain as the Navius.Primitives NuGet package instead");
    Console.WriteLine("                 of vendoring it. Cannot be combined with --namespace.");
    Console.WriteLine("  --overwrite    replace target files that already exist (default: skip them).");
}

string ResolveSourcePath(string sourcePath)
{
    var normalizedSourcePath = sourcePath.Replace('/', Path.DirectorySeparatorChar);
    var primaryPath = Path.GetFullPath(Path.Combine(root, normalizedSourcePath));

    if (File.Exists(primaryPath) || rootExplicit)
    {
        return primaryPath;
    }

    var bundledPath = ResolveBundledSourcePath(sourcePath);
    return bundledPath is not null && File.Exists(bundledPath)
        ? bundledPath
        : primaryPath;
}

string? ResolveBundledSourcePath(string sourcePath)
{
    const string brainPrefix = "../navius/";
    const string helmPrefix = "src/Zits.Ui/";

    if (sourcePath.StartsWith(brainPrefix, StringComparison.Ordinal))
    {
        var relative = sourcePath[brainPrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(packageRoot, "registry-source", "navius", relative);
    }

    if (sourcePath.StartsWith(helmPrefix, StringComparison.Ordinal))
    {
        var relative = sourcePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(packageRoot, "registry-source", "zits-helm", relative);
    }

    // Registry-relative files (e.g. registry/lib/Cn.cs) are bundled verbatim under the
    // package root, next to registry.json (see the bundledRegistry path above).
    if (sourcePath.StartsWith("registry/", StringComparison.Ordinal))
    {
        var relative = sourcePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(packageRoot, relative);
    }

    return null;
}
