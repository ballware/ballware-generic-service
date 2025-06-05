using Ballware.Generic.Caching.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Caching;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBallwareGenericDistributedCaching(this IServiceCollection services)
    {
        services.AddSingleton<ITenantConnectionCache, DistributedTenantConnectionCache>();

        return services;
    }
}