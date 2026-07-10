using System.Text.Json;
using Microsoft.JSInterop;
using Zits.Ui.Theming;

namespace Zits.Ui.Tests;

/// <summary>
/// The C# side of the theme engine must keep the DOM in sync with what it reports:
/// resetting to System must resync the mode class from the OS preference (not leave
/// the previous selection's .dark behind), and stored selections that C# normalizes
/// (invalid dimension values) must be reapplied so the DOM and storage carry the
/// normalized state, not the raw one the pre-paint script restored.
/// The JS module is faked at the IJSRuntime seam; arguments round-trip through JSON
/// exactly like the real interop serializer.
/// </summary>
public class ThemeServiceTests
{
    [Fact]
    public async Task Reset_resyncs_the_mode_class_from_the_system_preference()
    {
        var js = new FakeThemeJs { StoredJson = """{"mode":"dark"}""" };
        await using var service = new ZitsThemeService(js);
        await service.InitializeAsync();
        Assert.Equal(ZitsThemeMode.Dark, service.Current.Mode);

        await service.ResetAsync();

        Assert.Equal(ZitsTheme.Default, service.Current);
        var clear = js.Module.Calls.FindIndex(c => c.Id == "clearTheme");
        var sync = js.Module.Calls.FindIndex(c => c.Id == "syncModeClass");
        Assert.True(clear >= 0, "reset must clear the persisted theme");
        Assert.True(sync > clear, "reset must resync the mode class after clearing");
        Assert.Equal("system", js.Module.Calls[sync].Args.Single());
    }

    [Fact]
    public async Task Init_reapplies_the_normalized_state_when_stored_values_are_invalid()
    {
        var js = new FakeThemeJs
        {
            StoredJson = """{"mode":"dark","base":"bogus","primary":"neon","radius":"md","font":"system","style":"standard"}""",
        };
        await using var service = new ZitsThemeService(js);

        await service.InitializeAsync();

        Assert.Equal(ZitsTheme.Default.Base, service.Current.Base);
        Assert.Equal(ZitsTheme.Default.Primary, service.Current.Primary);

        var apply = Assert.Single(js.Module.Calls, c => c.Id == "applyTheme");
        var state = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(apply.Args.Single());
        Assert.Equal("dark", state["mode"]);
        Assert.Equal(ZitsTheme.Default.Base, state["base"]);
        Assert.Equal(ZitsTheme.Default.Primary, state["primary"]);
    }

    [Fact]
    public async Task Init_without_a_stored_selection_leaves_the_document_alone()
    {
        var js = new FakeThemeJs { StoredJson = null };
        await using var service = new ZitsThemeService(js);

        await service.InitializeAsync();

        Assert.Equal(ZitsTheme.Default, service.Current);
        Assert.DoesNotContain(js.Module.Calls, c => c.Id is "applyTheme" or "clearTheme" or "syncModeClass");
    }

    // --- fakes ---------------------------------------------------------------

    /// <summary>IJSRuntime whose module import yields a recording zits-theme.js fake.</summary>
    private sealed class FakeThemeJs : IJSRuntime
    {
        public FakeModule Module { get; } = new();

        public string? StoredJson
        {
            get => Module.StoredJson;
            set => Module.StoredJson = value;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Assert.Equal("import", identifier);
            return ValueTask.FromResult((TValue)(object)Module);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }

    /// <summary>The zits-theme.js module surface, recording calls and answering reads.</summary>
    private sealed class FakeModule : IJSObjectReference
    {
        public List<(string Id, object?[] Args)> Calls { get; } = [];
        public string? StoredJson { get; set; }
        public bool SystemDark { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add((identifier, args ?? []));
            return identifier switch
            {
                "isSystemDark" => ValueTask.FromResult((TValue)(object)SystemDark),
                // Round-trip through JSON so the service's private DTO deserializes
                // exactly as it would from the real interop layer.
                "readTheme" => ValueTask.FromResult(JsonSerializer.Deserialize<TValue>(StoredJson ?? "null")!),
                "watchSystem" => ValueTask.FromResult((TValue)(object)new FakeWatcher()),
                _ => ValueTask.FromResult(default(TValue)!),
            };
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeWatcher : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
