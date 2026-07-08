using Microsoft.Extensions.DependencyInjection;
using Navius.Primitives.Portal;

namespace Navius.Primitives;

/// <summary>DI registration for the vendored Navius engine services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Navius engine services the vendored primitives rely on (the legacy
    /// portal registry and the global keyboard-shortcut dispatcher). Call once at startup.
    /// As you vendor components that ship their own services (for example the toast
    /// manager), add their registrations here.
    /// </summary>
    public static IServiceCollection AddNavius(this IServiceCollection services)
    {
        services.AddScoped<PortalService>();
        services.AddScoped<KeyboardShortcutService>();
        return services;
    }
}
