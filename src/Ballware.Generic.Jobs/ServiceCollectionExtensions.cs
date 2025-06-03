using Ballware.Generic.Jobs.Internal;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.AspNetCore;

namespace Ballware.Generic.Jobs;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBallwareGenericBackgroundJobs(this IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            q.AddJob<GenericImportJob>(GenericImportJob.Key, configurator => configurator.StoreDurably());
        });

        services.AddQuartzServer(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        return services;
    }
}