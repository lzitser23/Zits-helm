using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

// registry-lint: validates registry/registry.json against registry/registry.schema.json
// and runs the structural checks a JSON Schema cannot express:
//   1. every file `path` exists on disk (relative to the repo root),
//   2. every registryDependencies name resolves to a real item,
//   3. no duplicate item names,
//   4. no two items map different sources to the same target.
//
//   registry-lint [--root <repo>]
//
// --root defaults to the current directory. registry.json / registry.schema.json are
// read from <root>/registry/. File sources under ../navius/ resolve against <root> too,
// so in CI the brain repo must be checked out as a sibling of the helm repo.

string root = Directory.GetCurrentDirectory();
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--root" && i + 1 < args.Length)
    {
        root = Path.GetFullPath(args[++i]);
    }
}

var registryPath = Path.Combine(root, "registry", "registry.json");
var schemaPath = Path.Combine(root, "registry", "registry.schema.json");

var errors = new List<string>();

if (!File.Exists(registryPath))
{
    Console.Error.WriteLine($"registry not found: {registryPath}");
    return 1;
}
if (!File.Exists(schemaPath))
{
    Console.Error.WriteLine($"schema not found: {schemaPath}");
    return 1;
}

var registryText = File.ReadAllText(registryPath);

JsonNode? instance;
try
{
    instance = JsonNode.Parse(registryText);
}
catch (JsonException ex)
{
    Console.Error.WriteLine($"registry.json is not valid JSON: {ex.Message}");
    return 1;
}

// 1. Schema validation.
var schema = JsonSchema.FromFile(schemaPath);
var evaluation = schema.Evaluate(instance, new EvaluationOptions
{
    OutputFormat = OutputFormat.List,
});
if (!evaluation.IsValid)
{
    foreach (var detail in evaluation.Details)
    {
        if (detail.Errors is null) continue;
        foreach (var kv in detail.Errors)
        {
            errors.Add($"schema: {detail.InstanceLocation} {kv.Value} ({kv.Key})");
        }
    }
    if (errors.Count == 0)
    {
        errors.Add("schema: registry.json failed schema validation.");
    }
}

// Structural checks operate on the parsed document.
using var doc = JsonDocument.Parse(registryText);
var itemsEl = doc.RootElement.GetProperty("items");

var names = new HashSet<string>(StringComparer.Ordinal);
var targetToSources = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

// First pass: collect item names (needed to resolve registryDependencies).
var itemNames = new List<string>();
foreach (var item in itemsEl.EnumerateArray())
{
    if (item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
    {
        itemNames.Add(n.GetString()!);
    }
}
var known = new HashSet<string>(itemNames, StringComparer.Ordinal);

foreach (var item in itemsEl.EnumerateArray())
{
    var name = item.GetProperty("name").GetString()!;

    // 3. Duplicate item names.
    if (!names.Add(name))
    {
        errors.Add($"duplicate item name: '{name}'");
    }

    // 2. registryDependencies resolve.
    if (item.TryGetProperty("registryDependencies", out var deps))
    {
        foreach (var dep in deps.EnumerateArray())
        {
            var depName = dep.GetString();
            if (depName is null || !known.Contains(depName))
            {
                errors.Add($"item '{name}': registryDependency '{depName}' is not a known item");
            }
        }
    }

    if (!item.TryGetProperty("files", out var files)) continue;
    foreach (var file in files.EnumerateArray())
    {
        var srcRel = file.GetProperty("path").GetString()!;
        var tgtRel = file.GetProperty("target").GetString()!;

        // 1. Source file exists on disk.
        var abs = Path.GetFullPath(Path.Combine(root, srcRel.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(abs))
        {
            errors.Add($"item '{name}': missing source file '{srcRel}' (resolved to {abs})");
        }

        // 4. Target collision: same target fed by two different sources.
        if (!targetToSources.TryGetValue(tgtRel, out var sources))
        {
            sources = new HashSet<string>(StringComparer.Ordinal);
            targetToSources[tgtRel] = sources;
        }
        sources.Add(srcRel);
    }
}

foreach (var (target, sources) in targetToSources)
{
    if (sources.Count > 1)
    {
        errors.Add($"target collision: '{target}' is written from {sources.Count} different sources: " +
            string.Join(", ", sources.OrderBy(s => s, StringComparer.Ordinal)));
    }
}

if (errors.Count > 0)
{
    Console.Error.WriteLine($"registry-lint: {errors.Count} problem(s) found:");
    foreach (var e in errors.OrderBy(x => x, StringComparer.Ordinal))
    {
        Console.Error.WriteLine($"  - {e}");
    }
    return 1;
}

Console.WriteLine($"registry-lint: OK ({itemNames.Count} items validated against the schema; all file paths, dependencies and targets are consistent).");
return 0;
