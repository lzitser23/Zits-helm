using Jint;
using Jint.Native;

namespace Zits.Ui.Tests;

/// <summary>
/// Guards the fix for the "theme reverts on navigation" regression (issue #12). Blazor
/// enhanced navigation patches &lt;html&gt; to match the freshly server-rendered document on
/// every internal navigation, which silently strips the client-applied .dark class and all
/// data-zits-* attributes that zits-theme-init.js applied pre-paint. The init script must
/// re-run its restore logic after every enhanced navigation, not just once before first
/// paint, or the theme visually reverts the moment the user clicks an internal link.
/// These tests execute the real script in a JS engine against a minimal DOM harness
/// (documentElement + classList/attributes, localStorage, matchMedia, and the two event
/// registration seams), then assert on the resulting document state — so a rename, an
/// inlined restore body, or a reordered registration all stay green as long as the
/// behavior holds, and only losing the behavior turns them red.
/// </summary>
public class ThemeInitScriptTests
{
    private static string ScriptSource => File.ReadAllText(
        Path.Combine(RepoPaths.Root, "src", "Zits.Ui", "wwwroot", "zits-theme-init.js"));

    private const string DarkSelection =
        """{"mode":"dark","base":"neutral","primary":"ink","radius":"md","font":"system","style":"standard"}""";

    [Fact]
    public void Stored_selection_restores_the_mode_class_and_every_dimension()
    {
        var dom = Run(stored: DarkSelection);

        Assert.True(dom.HasClass("dark"));
        Assert.Equal("dark", dom.Attribute("data-zits-mode"));
        Assert.Equal("neutral", dom.Attribute("data-zits-base"));
        Assert.Equal("ink", dom.Attribute("data-zits-primary"));
        Assert.Equal("md", dom.Attribute("data-zits-radius"));
        Assert.Equal("system", dom.Attribute("data-zits-font"));
        Assert.Equal("standard", dom.Attribute("data-zits-style"));
    }

    /// <summary>
    /// The legacy storage format is a bare mode string. It carries no dimensions, so
    /// restoring it must touch the class only and leave the markup's own data-zits-*
    /// (or their absence) alone.
    /// </summary>
    [Fact]
    public void Legacy_bare_mode_value_restores_the_class_only()
    {
        var dom = Run(stored: "dark");

        Assert.True(dom.HasClass("dark"));
        Assert.Null(dom.Attribute("data-zits-mode"));
        Assert.Empty(dom.Attributes);
    }

    /// <summary>
    /// No stored selection means the user never chose: whatever the host markup
    /// hardcodes (for example a default class="dark") must survive untouched.
    /// </summary>
    [Fact]
    public void No_stored_selection_leaves_the_document_untouched()
    {
        var dom = Run(stored: null, seedClasses: ["dark"], seedAttributes: new() { ["data-zits-base"] = "slate" });

        Assert.True(dom.HasClass("dark"));
        Assert.Equal("slate", dom.Attribute("data-zits-base"));
        Assert.Single(dom.Attributes);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void System_mode_follows_the_os_preference(bool systemDark, bool expectDark)
    {
        // Seed .dark so the light case proves the class is actively removed, not merely absent.
        var dom = Run(
            stored: """{"mode":"system"}""",
            systemPrefersDark: systemDark,
            seedClasses: ["dark"]);

        Assert.Equal(expectDark, dom.HasClass("dark"));
        Assert.Equal("system", dom.Attribute("data-zits-mode"));
    }

    /// <summary>
    /// The regression itself: after enhanced navigation the server-rendered markup is
    /// patched back over the client-applied theme. Re-running the restore logic from the
    /// enhancedload event is what puts it back.
    /// </summary>
    [Fact]
    public void Enhanced_navigation_reapplies_the_theme_after_the_dom_is_patched_back()
    {
        var dom = Run(stored: DarkSelection, blazorPresentAtLoad: true);
        Assert.True(dom.HasClass("dark"));

        dom.StripTheme();
        Assert.False(dom.HasClass("dark"));
        Assert.Empty(dom.Attributes);

        dom.FireBlazor("enhancedload");

        Assert.True(dom.HasClass("dark"));
        Assert.Equal("dark", dom.Attribute("data-zits-mode"));
        Assert.Equal("standard", dom.Attribute("data-zits-style"));
    }

    /// <summary>
    /// This script runs in &lt;head&gt;, before blazor.web.js, so the Blazor global usually
    /// does not exist yet at load. Registration must be deferred to DOMContentLoaded and
    /// pick Blazor up then, or enhanced navigation is never hooked at all.
    /// </summary>
    [Fact]
    public void Registration_is_deferred_to_DOMContentLoaded_when_Blazor_is_not_loaded_yet()
    {
        var dom = Run(stored: DarkSelection, blazorPresentAtLoad: false);
        Assert.False(dom.HasBlazorHandler("enhancedload"));

        dom.InstallBlazor();
        dom.FireDocument("DOMContentLoaded");
        Assert.True(dom.HasBlazorHandler("enhancedload"));

        dom.StripTheme();
        dom.FireBlazor("enhancedload");
        Assert.True(dom.HasClass("dark"));
    }

    /// <summary>
    /// Storage can throw outright (Safari private mode, blocked third-party storage). The
    /// script must swallow that and leave the host markup alone rather than break the page.
    /// </summary>
    [Fact]
    public void Unavailable_storage_neither_throws_nor_touches_the_document()
    {
        var dom = Run(stored: null, storageThrows: true, seedClasses: ["dark"]);

        Assert.True(dom.HasClass("dark"));
        Assert.Empty(dom.Attributes);
    }

    /// <summary>
    /// The pre-paint contract requires a classic, synchronous script (loaded before first
    /// paint in &lt;head&gt;); an ES module would defer execution and reintroduce the
    /// flash-of-wrong-theme bug this file exists to prevent. Jint parses with the script
    /// goal, so module syntax fails to parse here exactly as it would in a classic
    /// &lt;script&gt; tag.
    /// </summary>
    [Fact]
    public void Parses_and_runs_as_a_classic_script()
    {
        var engine = new Engine();
        engine.Execute(Harness);

        engine.Execute(ScriptSource); // throws on module-only syntax (import/export)
    }

    // --- harness -------------------------------------------------------------

    private static Dom Run(
        string? stored,
        bool systemPrefersDark = false,
        bool blazorPresentAtLoad = false,
        bool storageThrows = false,
        string[]? seedClasses = null,
        Dictionary<string, string>? seedAttributes = null)
    {
        var engine = new Engine();
        engine.Execute(Harness);

        var dom = new Dom(engine);
        dom.SetStorage(stored, storageThrows);
        dom.SetSystemDark(systemPrefersDark);
        foreach (var cls in seedClasses ?? [])
        {
            engine.Execute($"document.documentElement.classList.add({Quote(cls)});");
        }
        foreach (var (name, value) in seedAttributes ?? [])
        {
            engine.Execute($"document.documentElement.setAttribute({Quote(name)},{Quote(value)});");
        }
        if (blazorPresentAtLoad)
        {
            dom.InstallBlazor();
        }

        engine.Execute(ScriptSource);
        return dom;
    }

    private static string Quote(string value) => "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

    /// <summary>The slice of the browser the pre-paint script touches, and nothing else.</summary>
    private const string Harness =
        """
        globalThis.window = globalThis;
        var __classes = [];
        var __attrs = {};
        var __store = { value: null, throws: false };
        var __systemDark = false;
        var __docHandlers = {};
        var __blazorHandlers = {};

        globalThis.document = {
          documentElement: {
            classList: {
              add: function (c) { if (__classes.indexOf(c) < 0) __classes.push(c); },
              remove: function (c) { var i = __classes.indexOf(c); if (i >= 0) __classes.splice(i, 1); },
              contains: function (c) { return __classes.indexOf(c) >= 0; }
            },
            setAttribute: function (n, v) { __attrs[n] = '' + v; },
            getAttribute: function (n) { return n in __attrs ? __attrs[n] : null; },
            removeAttribute: function (n) { delete __attrs[n]; }
          },
          addEventListener: function (type, fn) { (__docHandlers[type] = __docHandlers[type] || []).push(fn); }
        };

        globalThis.localStorage = {
          getItem: function () {
            if (__store.throws) throw new Error('storage is unavailable');
            return __store.value;
          }
        };

        globalThis.matchMedia = function (query) {
          return { matches: query === '(prefers-color-scheme: dark)' && __systemDark };
        };

        globalThis.__installBlazor = function () {
          globalThis.Blazor = {
            addEventListener: function (type, fn) { (__blazorHandlers[type] = __blazorHandlers[type] || []).push(fn); }
          };
        };

        globalThis.__fire = function (handlers, type) {
          var list = handlers[type] || [];
          for (var i = 0; i < list.length; i++) list[i]({ type: type });
          return list.length;
        };

        // Stand-in for what enhanced navigation does: patch <html> back to the
        // server-rendered markup, dropping every client-applied theme mutation.
        globalThis.__stripTheme = function () { __classes = []; __attrs = {}; };
        """;

    /// <summary>Reads and drives the harness from the test side.</summary>
    private sealed class Dom(Engine engine)
    {
        public bool HasClass(string name)
            => engine.Evaluate($"document.documentElement.classList.contains({Quote(name)})").AsBoolean();

        public string? Attribute(string name)
        {
            var value = engine.Evaluate($"document.documentElement.getAttribute({Quote(name)})");
            return value.IsNull() ? null : value.AsString();
        }

        /// <summary>Every attribute currently set on the documentElement.</summary>
        public IReadOnlyList<string> Attributes
        {
            get
            {
                var joined = engine.Evaluate("Object.keys(__attrs).join('|')").AsString();
                return joined.Length == 0 ? [] : joined.Split('|');
            }
        }

        public void SetStorage(string? value, bool throws)
        {
            engine.SetValue("__storedValue", value is null ? JsValue.Null : value);
            engine.Execute("__store.value = __storedValue;");
            engine.Execute($"__store.throws = {(throws ? "true" : "false")};");
        }

        public void SetSystemDark(bool dark) => engine.Execute($"__systemDark = {(dark ? "true" : "false")};");

        public void InstallBlazor() => engine.Execute("__installBlazor();");

        public void StripTheme() => engine.Execute("__stripTheme();");

        public bool HasBlazorHandler(string type)
            => engine.Evaluate($"(__blazorHandlers[{Quote(type)}] || []).length > 0").AsBoolean();

        public void FireBlazor(string type)
            => Assert.True(engine.Evaluate($"__fire(__blazorHandlers, {Quote(type)})").AsNumber() > 0,
                $"no Blazor '{type}' handler was registered");

        public void FireDocument(string type)
            => Assert.True(engine.Evaluate($"__fire(__docHandlers, {Quote(type)})").AsNumber() > 0,
                $"no document '{type}' handler was registered");
    }
}
