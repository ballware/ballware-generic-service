using System.Collections.Immutable;
using System.Text;
using Ballware.Generic.Caching;
using Ballware.Generic.Data.Ef.Configuration;
using Ballware.Generic.Data.Public;
using Ballware.Generic.Data.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;

namespace Ballware.Generic.Data.Ef.Postgres.Tests.Repository;

[TestFixture]
public class CachableTenantConnectionBaseRepositoryTest : RepositoryBaseTest
{
    private Mock<ITenantConnectionCache> DistributedCacheMock { get; } = new Mock<ITenantConnectionCache>();
    
    [Test]
    public async Task Save_and_remove_value_succeeds()
    {
        using var scope = Application.Services.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<ITenantConnectionRepository>();

        var expectedValue = await repository.NewAsync("primary", ImmutableDictionary<string, object>.Empty);

        expectedValue.ConnectionString = "fake_connection_string";
        expectedValue.Model = "fake_model";
        expectedValue.Provider = "mssql";
        expectedValue.Schema = "fakeschema";
        
        DistributedCacheMock.Setup(c => c.SetItem(expectedValue.Id, expectedValue));
        
        await repository.SaveAsync(null, "primary", ImmutableDictionary<string, object>.Empty, expectedValue);
        
        DistributedCacheMock.Verify(c => c.SetItem(expectedValue.Id, expectedValue), 
            Times.Once);

        TenantConnection? cachedItem;
        
        DistributedCacheMock.Setup(c => c.TryGetItem(expectedValue.Id, out cachedItem))
            .Callback((Guid id, out TenantConnection? item) =>
            {
                item = null;
            })
            .Returns(false);
        
        var uncachedActualValue = await repository.ByIdAsync(expectedValue.Id);
        
        Assert.Multiple(() =>
        {
            Assert.That(uncachedActualValue, Is.Not.Null);
            Assert.That(uncachedActualValue?.Id, Is.EqualTo(expectedValue.Id));
            Assert.That(uncachedActualValue?.Model, Is.EqualTo(expectedValue.Model));
            Assert.That(uncachedActualValue?.Provider, Is.EqualTo(expectedValue.Provider));
            Assert.That(uncachedActualValue?.Schema, Is.EqualTo(expectedValue.Schema));
        });
        
        DistributedCacheMock.Setup(c => c.TryGetItem(expectedValue.Id, out cachedItem))
            .Callback((Guid id, out TenantConnection? item) =>
            {
                item = expectedValue;
            })
            .Returns(true);
        
        var cachedActualValue = await repository.ByIdAsync(expectedValue.Id);
        
        Assert.Multiple(() =>
        {
            Assert.That(cachedActualValue, Is.Not.Null);
            Assert.That(cachedActualValue?.Id, Is.EqualTo(expectedValue.Id));
            Assert.That(cachedActualValue?.Model, Is.EqualTo(expectedValue.Model));
            Assert.That(cachedActualValue?.Provider, Is.EqualTo(expectedValue.Provider));
            Assert.That(cachedActualValue?.Schema, Is.EqualTo(expectedValue.Schema));
        });
        
        DistributedCacheMock.Verify(c => c.TryGetItem(expectedValue.Id, out cachedItem), Times.Exactly(2));

        DistributedCacheMock.Setup(c => c.PurgeItem(expectedValue.Id));
        
        var removeParams = new Dictionary<string, object>([new KeyValuePair<string, object>("Id", expectedValue.Id)]);

        var removeResult = await repository.RemoveAsync(null, ImmutableDictionary<string, object>.Empty, removeParams);

        Assert.Multiple(() =>
        {
            Assert.That(removeResult.Result, Is.True);
        });
        
        DistributedCacheMock.Verify(c => c.PurgeItem(expectedValue.Id), Times.Once);
    }
    
    protected override void PrepareApplication(WebApplicationBuilder builder)
    {
        var storageOptions = PreparedBuilder.Configuration.GetSection("Storage").Get<StorageOptions>();

        storageOptions.EnableCaching = true;
        
        builder.Services.AddSingleton(DistributedCacheMock.Object);
        
        builder.Services.AddBallwareTenantStorageForPostgres(storageOptions, MasterConnectionString);
        builder.Services.AddAutoMapper(config =>
        {
            config.AddBallwareTenantStorageMappings();
        });
    }
}