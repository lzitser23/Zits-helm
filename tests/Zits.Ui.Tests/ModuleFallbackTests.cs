using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Microsoft.Extensions.DependencyInjection;
using Zits.Ui.Theming;

namespace Zits.Ui.Tests;

// The JS engine resolves in two modes: package mode serves it as a static web asset under
// _content/<PackageId>/, while vendored mode (the registry CLI) copies the JS into the
// consumer's own wwwroot, served at the app root, with no package reference. Every importer
// must try the package path first and fall back to the vendored path, or a vendored consumer
// silently loses all JS-driven behavior. The Navius engine's own importers are covered
// behaviourally in that repo (lzitser23/Navius#28); these are the Zits.Ui importers.

/// <summary>
/// The Zits.Ui theme engine's half of the dual-mode resolution, exercised through the
/// IJSRuntime seam: a package-mode import failure must actually fall through to the
/// vendored path (and the resulting module must be the one the service goes on to use),
/// a successful package import must never reach for the vendored path, and losing both
/// must raise a loud error naming both paths and keeping the package failure as the cause.
/// </summary>
public class ThemeModuleFallbackTests
{
    private const string PackagePath = "./_content/Zits.Ui/zits-theme.js";
    private const string VendoredPath = "./zits-theme.js";

    [Fact]
    public async Task Vendored_module_serves_the_service_when_the_package_import_fails()
    {
        var js = new PathAwareJs { FailingImports = { PackagePath } };
        await using var service = new ZitsThemeService(js);

        await service.InitializeAsync();

        Assert.Equal([PackagePath, VendoredPath], js.ImportedPaths);
        var module = Assert.Single(js.Modules);
        Assert.Equal(VendoredPath, module.Path);
        // The service is live on the vendored module, not merely past the import.
        Assert.Contains("isSystemDark", module.Calls);
        Assert.Contains("watchSystem", module.Calls);
    }

    [Fact]
    public async Task Package_module_is_used_alone_when_its_import_succeeds()
    {
        var js = new PathAwareJs();
        await using var service = new ZitsThemeService(js);

        await service.InitializeAsync();

        Assert.Equal([PackagePath], js.ImportedPaths);
        Assert.Equal(PackagePath, Assert.Single(js.Modules).Path);
    }

    [Fact]
    public async Task Losing_both_imports_reports_both_paths_and_keeps_the_package_failure_as_the_cause()
    {
        var js = new PathAwareJs { FailingImports = { PackagePath, VendoredPath } };
        // Not disposed: the failed import is cached in the service's Lazy, so disposal
        // would replay the same failure rather than tear anything down.
        var service = new ZitsThemeService(js);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(service.InitializeAsync);

        Assert.Equal([PackagePath, VendoredPath], js.ImportedPaths);
        Assert.Contains($"'{PackagePath}'", error.Message);
        Assert.Contains($"'{VendoredPath}'", error.Message);
        var cause = Assert.IsType<JSException>(error.InnerException);
        Assert.Contains(PackagePath, cause.Message);
    }

    /// <summary>IJSRuntime that records every import path and can fail chosen ones.</summary>
    private sealed class PathAwareJs : IJSRuntime
    {
        public List<string> ImportedPaths { get; } = [];
        public HashSet<string> FailingImports { get; } = [];
        public List<RecordingModule> Modules { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Assert.Equal("import", identifier);
            var path = Assert.IsType<string>(Assert.Single(args ?? []));
            ImportedPaths.Add(path);

            if (FailingImports.Contains(path))
            {
                throw new JSException($"Failed to fetch dynamically imported module: {path}");
            }

            var module = new RecordingModule(path);
            Modules.Add(module);
            return ValueTask.FromResult((TValue)(object)module);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }

    /// <summary>The zits-theme.js surface, remembering which path it was imported from.</summary>
    private sealed class RecordingModule(string path) : IJSObjectReference
    {
        public string Path { get; } = path;
        public List<string> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add(identifier);
            return identifier switch
            {
                "isSystemDark" => ValueTask.FromResult((TValue)(object)false),
                "watchSystem" => ValueTask.FromResult((TValue)(object)new RecordingModule(Path)),
                _ => ValueTask.FromResult(default(TValue)!), // readTheme: nothing stored
            };
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>
/// The vendorable Zits.Ui components import the Navius engine themselves, so they carry
/// their own copy of the package-then-vendored fallback. Rendered against a runtime whose
/// package-mode import fails, each must reach for the vendored path and keep working — a
/// vendored consumer (registry CLI copy, no package reference) is exactly this situation.
/// bUnit's own module setup cannot fail an import (SetupModule exposes no SetException),
/// so the IJSRuntime registration is the seam instead.
/// </summary>
public class ComponentModuleFallbackTests : TestContext
{
    private const string PackagePath = "./_content/Navius.Primitives/navius-interop.js";
    private const string VendoredPath = "./navius-interop.js";

    private readonly PathAwareJsRuntime _js = new() { FailingImports = { PackagePath } };

    public ComponentModuleFallbackTests() => Services.AddSingleton<IJSRuntime>(_js);

    [Fact]
    public async Task Sidebar_provider_falls_back_to_the_vendored_engine()
    {
        var cut = RenderComponent<ZitsSidebarProvider>(ps => ps
            .AddChildContent("<span>content</span>"));

        Assert.Equal([PackagePath, VendoredPath], _js.ImportedPaths);
        // The measurement the provider makes on first render came from the vendored module.
        Assert.Contains("getRect", Assert.Single(_js.Modules).Calls);

        // Still live afterwards: the open state keeps driving the cascade.
        Assert.Equal("expanded", cut.Find("[data-state]").GetAttribute("data-state"));
        await cut.InvokeAsync(() => cut.Instance.ToggleAsync());
        Assert.Equal("collapsed", cut.Find("[data-state]").GetAttribute("data-state"));
    }

    [Fact]
    public void Resizable_handle_falls_back_to_the_vendored_engine()
    {
        var cut = RenderComponent<ZitsResizablePanelGroup>(ps => ps
            .AddChildContent<ZitsResizablePanel>(p => p.Add(x => x.DefaultSize, 60))
            .AddChildContent<ZitsResizableHandle>()
            .AddChildContent<ZitsResizablePanel>(p => p.Add(x => x.DefaultSize, 40)));

        Assert.Empty(_js.ImportedPaths); // nothing is imported until a drag starts
        Assert.Equal("60", cut.Find("[data-panel-resize-handle]").GetAttribute("aria-valuenow"));

        cut.Find("[data-panel-resize-handle]")
           .TriggerEvent("onpointerdown", new PointerEventArgs { ClientX = 120, ClientY = 40 });

        Assert.Equal([PackagePath, VendoredPath], _js.ImportedPaths);
        // The drag really started, so the group was measured through the vendored module.
        Assert.Contains("getRect", Assert.Single(_js.Modules).Calls);
        Assert.Equal("pointer", cut.Find("[data-panel-resize-handle]").GetAttribute("data-resize-handle-active"));

        // Still live afterwards: keyboard resize keeps re-balancing the pair.
        cut.Find("[data-panel-resize-handle]").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("65", cut.Find("[data-panel-resize-handle]").GetAttribute("aria-valuenow"));
    }

    /// <summary>Records every import path, fails the chosen ones, and serves a Navius engine fake.</summary>
    private sealed class PathAwareJsRuntime : IJSRuntime
    {
        public List<string> ImportedPaths { get; } = [];
        public HashSet<string> FailingImports { get; } = [];
        public List<EngineModule> Modules { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier != "import")
            {
                return ValueTask.FromResult(default(TValue)!); // localStorage.getItem etc.
            }

            var path = Assert.IsType<string>(Assert.Single(args ?? []));
            ImportedPaths.Add(path);
            if (FailingImports.Contains(path))
            {
                throw new JSException($"Failed to fetch dynamically imported module: {path}");
            }

            var module = new EngineModule(path);
            Modules.Add(module);
            return ValueTask.FromResult((TValue)(object)module);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }

    /// <summary>The navius-interop.js surface these components use: getRect, and nothing else.</summary>
    private sealed class EngineModule(string path) : IJSObjectReference
    {
        public string Path { get; } = path;
        public List<string> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add(identifier);

            // getRect deserializes into a DOMRect record private to each component, so it
            // can only be built reflectively here. Every edge is a double, so filling them
            // all hands back a real 1024px measurement whatever the declared order.
            var ctor = typeof(TValue)
                .GetConstructors(System.Reflection.BindingFlags.Instance |
                                 System.Reflection.BindingFlags.Public |
                                 System.Reflection.BindingFlags.NonPublic)
                .Single(c => c.GetParameters() is { Length: > 0 } p
                             && p.All(x => x.ParameterType == typeof(double)));
            var rect = (TValue)ctor.Invoke([.. ctor.GetParameters().Select(_ => (object)1024d)]);
            return ValueTask.FromResult(rect);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
