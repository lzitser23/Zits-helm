using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace Zits.Ui.Theming;

/// <summary>
/// Scoped runtime theme state for zits/ui. It owns the selected six dimensions,
/// persists them through <c>wwwroot/zits-theme.js</c>, and mirrors them onto
/// <c>document.documentElement</c> as <c>data-zits-*</c> attributes.
/// </summary>
public sealed class ZitsThemeService : IAsyncDisposable
{
    private const string ModulePath = "./_content/Zits.Ui/zits-theme.js";

    private readonly IJSRuntime _js;
    private readonly Lazy<Task<IJSObjectReference>> _module;

    private DotNetObjectReference<ZitsThemeService>? _selfRef;
    private IJSObjectReference? _systemWatcher;
    private bool _initialized;
    private bool _disposed;

    public ZitsThemeService(IJSRuntime js)
    {
        _js = js;
        _module = new Lazy<Task<IJSObjectReference>>(
            () => _js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask());
    }

    /// <summary>The current normalized theme selection.</summary>
    public ZitsTheme Current { get; private set; } = ZitsTheme.Default;

    /// <summary>The latest OS dark-mode preference reported by the browser.</summary>
    public bool SystemPrefersDark { get; private set; }

    /// <summary>Raised after the current theme or system preference changes.</summary>
    public event Action? Changed;

    /// <summary>
    /// Load the persisted selection and start watching system color-scheme changes.
    /// Call from <c>OnAfterRenderAsync(firstRender)</c> in a component that uses the service.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized || _disposed)
        {
            return;
        }

        _initialized = true;

        try
        {
            var module = await _module.Value;
            SystemPrefersDark = await module.InvokeAsync<bool>("isSystemDark");

            var stored = await module.InvokeAsync<JsThemeState?>("readTheme");
            if (stored is not null)
            {
                Current = Normalize(stored);
            }

            _selfRef = DotNetObjectReference.Create(this);
            _systemWatcher = await module.InvokeAsync<IJSObjectReference>("watchSystem", _selfRef);
            Changed?.Invoke();
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>Set a complete theme selection and persist/apply it immediately.</summary>
    public async Task SetThemeAsync(ZitsTheme theme)
    {
        Current = Normalize(theme);
        await ApplyCurrentAsync();
        Changed?.Invoke();
    }

    public Task SetModeAsync(ZitsThemeMode mode) => SetThemeAsync(Current with { Mode = mode });
    public Task SetBaseAsync(string value) => SetThemeAsync(Current with { Base = Pick(value, ZitsThemePresets.Bases, ZitsTheme.Default.Base) });
    public Task SetPrimaryAsync(string value) => SetThemeAsync(Current with { Primary = Pick(value, ZitsThemePresets.Primaries, ZitsTheme.Default.Primary) });
    public Task SetRadiusAsync(string value) => SetThemeAsync(Current with { Radius = Pick(value, ZitsThemePresets.Radii, ZitsTheme.Default.Radius) });
    public Task SetFontAsync(string value) => SetThemeAsync(Current with { Font = Pick(value, ZitsThemePresets.Fonts, ZitsTheme.Default.Font) });
    public Task SetStyleAsync(string value) => SetThemeAsync(Current with { Style = Pick(value, ZitsThemePresets.Styles, ZitsTheme.Default.Style) });

    /// <summary>Remove persisted theme data and data-zits-* attributes from the document.</summary>
    public async Task ResetAsync()
    {
        Current = ZitsTheme.Default;

        try
        {
            var module = await _module.Value;
            await module.InvokeVoidAsync("clearTheme");
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        Changed?.Invoke();
    }

    /// <summary>Generate a self-contained CSS export for the current selection.</summary>
    public string GenerateCss() => ThemeStylesheet.GenerateCss(Current);

    /// <summary>Copy the current theme export CSS to the browser clipboard.</summary>
    public async Task CopyCssAsync()
    {
        try
        {
            var module = await _module.Value;
            await module.InvokeVoidAsync("copyText", GenerateCss());
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    [JSInvokable]
    public async Task OnSystemThemeChanged(bool dark)
    {
        SystemPrefersDark = dark;
        if (Current.Mode == ZitsThemeMode.System)
        {
            await ApplyCurrentAsync();
        }
        Changed?.Invoke();
    }

    private async Task ApplyCurrentAsync()
    {
        try
        {
            var module = await _module.Value;
            await module.InvokeVoidAsync("applyTheme", ToJs(Current));
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static Dictionary<string, string> ToJs(ZitsTheme theme) => new()
    {
        ["mode"] = ModeSlug(theme.Mode),
        ["base"] = theme.Base,
        ["primary"] = theme.Primary,
        ["radius"] = theme.Radius,
        ["font"] = theme.Font,
        ["style"] = theme.Style,
    };

    private static ZitsTheme Normalize(ZitsTheme theme) => new(
        theme.Mode,
        Pick(theme.Base, ZitsThemePresets.Bases, ZitsTheme.Default.Base),
        Pick(theme.Primary, ZitsThemePresets.Primaries, ZitsTheme.Default.Primary),
        Pick(theme.Radius, ZitsThemePresets.Radii, ZitsTheme.Default.Radius),
        Pick(theme.Font, ZitsThemePresets.Fonts, ZitsTheme.Default.Font),
        Pick(theme.Style, ZitsThemePresets.Styles, ZitsTheme.Default.Style));

    private static ZitsTheme Normalize(JsThemeState state) => new(
        ParseMode(state.Mode),
        Pick(state.Base, ZitsThemePresets.Bases, ZitsTheme.Default.Base),
        Pick(state.Primary, ZitsThemePresets.Primaries, ZitsTheme.Default.Primary),
        Pick(state.Radius, ZitsThemePresets.Radii, ZitsTheme.Default.Radius),
        Pick(state.Font, ZitsThemePresets.Fonts, ZitsTheme.Default.Font),
        Pick(state.Style, ZitsThemePresets.Styles, ZitsTheme.Default.Style));

    private static string Pick(string? value, IReadOnlyList<string> allowed, string fallback)
        => value is not null && allowed.Contains(value, StringComparer.Ordinal) ? value : fallback;

    private static ZitsThemeMode ParseMode(string? value) => value switch
    {
        "light" => ZitsThemeMode.Light,
        "dark" => ZitsThemeMode.Dark,
        _ => ZitsThemeMode.System,
    };

    public static string ModeSlug(ZitsThemeMode mode) => mode switch
    {
        ZitsThemeMode.Light => "light",
        ZitsThemeMode.Dark => "dark",
        _ => "system",
    };

    private sealed class JsThemeState
    {
        [JsonPropertyName("mode")] public string? Mode { get; set; }
        [JsonPropertyName("base")] public string? Base { get; set; }
        [JsonPropertyName("primary")] public string? Primary { get; set; }
        [JsonPropertyName("radius")] public string? Radius { get; set; }
        [JsonPropertyName("font")] public string? Font { get; set; }
        [JsonPropertyName("style")] public string? Style { get; set; }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;

        if (_systemWatcher is not null)
        {
            try
            {
                await _systemWatcher.InvokeVoidAsync("destroy");
                await _systemWatcher.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        if (_module.IsValueCreated)
        {
            try
            {
                var module = await _module.Value;
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        _selfRef?.Dispose();
    }
}
