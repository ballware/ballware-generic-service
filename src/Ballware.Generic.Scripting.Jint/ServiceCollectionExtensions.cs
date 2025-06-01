using Ballware.Generic.Scripting.Jint.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Scripting.Jint;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBallwareJintGenericScripting(this IServiceCollection services)
    {
        services.AddScoped<IGenericEntityScriptingExecutor, JintEntityMetadataScriptingExecutor>();
        services.AddScoped<IStatisticScriptingExecutor, JintStatisticScriptingExecutor>();

        return services;
    }

}