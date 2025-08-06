using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ballware.Generic.Data.Ef.Configuration;

namespace Ballware.Generic.Data.Ef.Initialization;

public class InitializationWorker : IHostedService
{
    private IServiceProvider ServiceProvider { get; }

    public InitializationWorker(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = ServiceProvider.CreateAsyncScope();

        var options = scope.ServiceProvider.GetRequiredService<StorageOptions>();

        if (options.AutoMigrations)
        {
            var context = scope.ServiceProvider.GetRequiredService<ITenantDbContext>();

            await context.MigrateDatabaseAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
