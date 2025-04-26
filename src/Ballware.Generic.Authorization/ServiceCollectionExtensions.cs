using System.Diagnostics.CodeAnalysis;
using Ballware.Generic.Authorization.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Authorization;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBallwareGenericAuthorizationUtils(this IServiceCollection services, string tenantClaim, string userIdClaim, string rightClaim)
    {
        services.AddSingleton<IPrincipalUtils>(new DefaultPrincipalUtils(tenantClaim, userIdClaim, rightClaim));

        return services;
    }
}