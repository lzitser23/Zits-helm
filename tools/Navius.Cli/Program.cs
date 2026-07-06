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
var packageRoot = AppContext.BaseDirectory;
var rootExplicit = false;
var positional = new List<string>();

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--to": to = args[++i]; break;
        case "--namespace": ns = args[++i]; break;
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
            Console.Error.WriteLine("usage: navius add <name> [--to <dir>] [--namespace <ns>]");
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
    foreach (var n in resolved)
    {
        var item = byName[n];
        Console.WriteLine($"{n}:");
        foreach (var f in item.GetProperty("files").EnumerateArray())
        {
            var srcRel = f.GetProperty("path").GetString()!;
            var tgtRel = f.GetProperty("target").GetString()!;
            var src = ResolveSourcePath(srcRel);
            var dst = Path.Combine(to, tgtRel.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(src))
            {
                Console.Error.WriteLine($"  ! missing source: {srcRel} (resolved to {src})");
                missing++;
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            var text = File.ReadAllText(src);
            if (ns is not null)
            {
                text = text.Replace("Navius.Primitives", ns);
            }
            File.WriteAllText(dst, text);
            Console.WriteLine($"  + {tgtRel}");
            copied++;
        }
    }

    Console.WriteLine($"\nAdded '{name}' - {resolved.Count} item(s), {copied} file(s) -> {to}");
    return missing == 0 ? 0 : 1;
}

void PrintUsage()
{
    Console.WriteLine("navius - copy-paste component registry for Blazor (Navius)");
    Console.WriteLine();
    Console.WriteLine("  navius list");
    Console.WriteLine("  navius add <name> [--to <dir>] [--namespace <ns>] [--root <repo>] [--registry <path>]");
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

    return null;
}
