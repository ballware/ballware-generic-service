using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Caching.Tests;

[TestFixture]
public class ServiceCollectionExtensionsTest
{
    [Test]
    public void AddBallwareDistributedCaching_succeeds()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        services.AddLogging();
        services.AddDistributedMemoryCache();
        services.AddBallwareGenericDistributedCaching();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Assert
        var cache = serviceProvider.GetService<ITenantConnectionCache>();
        
        Assert.That(cache, Is.Not.Null);
    }
}