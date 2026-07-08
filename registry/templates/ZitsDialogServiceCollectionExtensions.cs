using Microsoft.Extensions.DependencyInjection;

namespace Zits.Ui;

/// <summary>DI registration for the styled helm's imperative dialog service.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the imperative <see cref="ZitsDialogService"/>. Call once at startup and mount a
    /// single <c>&lt;ZitsDialogOutlet /&gt;</c> near the app root. Also call the brain's
    /// <c>AddNavius()</c> for its overlay services.
    /// </summary>
    public static IServiceCollection AddZitsDialog(this IServiceCollection services)
    {
        services.AddScoped<ZitsDialogService>();
        return services;
    }
}
