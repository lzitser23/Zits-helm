using Microsoft.Extensions.DependencyInjection;
using Zits.Ui.Theming;

namespace Zits.Ui;

/// <summary>
/// DI registration for the styled helm's services. Mirrors the brain's <c>AddNavius()</c>
/// (which registers <c>ToastManager</c> scoped): call once at startup and mount a single
/// <c>&lt;ZitsDialogOutlet /&gt;</c> near the app root. Consumers that use <c>AddZitsUi()</c>
/// still call <c>AddNavius()</c> for the brain's own services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Register the helm's injectable services.</summary>
    public static IServiceCollection AddZitsUi(this IServiceCollection services)
    {
        services.AddScoped<ZitsDialogService>();
        services.AddScoped<ZitsThemeService>();
        return services;
    }
}
