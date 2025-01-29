using Ballware.Generic.Authorization.Jint.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Authorization.Jint;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBallwareJintRightsChecker(this IServiceCollection services)
    {
        services.AddSingleton<ITenantRightsChecker, JavascriptTenantRightsChecker>();
        services.AddSingleton<IEntityRightsChecker, JavascriptEntityRightsChecker>();

        return services;
    }

}