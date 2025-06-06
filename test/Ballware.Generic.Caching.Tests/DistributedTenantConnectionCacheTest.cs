using System.Text;
using Ballware.Generic.Caching.Configuration;
using Ballware.Generic.Caching.Internal;
using Ballware.Generic.Data.Public;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;

namespace Ballware.Generic.Caching.Tests;

[TestFixture]
public class DistributedTenantConnectionCacheTest
{
    private Mock<IDistributedCache> DistributedCacheMock { get; set; } = null!;

    [SetUp]
    public void Setup()
    {
        DistributedCacheMock = new Mock<IDistributedCache>();
    }
    
    [Test]
    public void TestCacheOperations_succeeds()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var entity = "TestEntity";
        var key = Guid.NewGuid().ToString();
        
        var expectedKey = $"{tenantId}".ToLowerInvariant();
        var expectedItem = new TenantConnection
        {
            Id = Guid.NewGuid(), 
            Schema = "fakeschema",
            ConnectionString = "fake connection string",
            Model = "fake model",
            Provider = "mssql"
        };
        
        var expectedSerializedItem = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(expectedItem));

        var options = Options.Create(new CacheOptions()
        {
            CacheExpirationHours = 1
        });
        
        // Mock the distributed cache methods
        DistributedCacheMock.Setup(c => c.Get(expectedKey))
            .Returns(expectedSerializedItem);

        DistributedCacheMock.Setup(cache =>
                cache.Set(expectedKey, expectedSerializedItem, It.IsAny<DistributedCacheEntryOptions>()))
            .Callback((string key, byte[] item, DistributedCacheEntryOptions options) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(key, Is.EqualTo(expectedKey));
                    Assert.That(item, Is.EqualTo(expectedSerializedItem));
                    Assert.That(options.AbsoluteExpirationRelativeToNow, Is.EqualTo(TimeSpan.FromHours(1)));
                });
            });
        
        var cache = new DistributedTenantConnectionCache(
            new LoggerFactory().CreateLogger<DistributedTenantConnectionCache>(),
            DistributedCacheMock.Object,
            options
        );
        
        // Act
        cache.SetItem(tenantId, expectedItem);
        
        Assert.That(cache.TryGetItem(tenantId, out TenantConnection? cachedItem), Is.True);
        
        cache.PurgeItem(tenantId);
        
        DistributedCacheMock.Setup(c => c.Get(expectedKey))
            .Returns(null as byte[]);
        
        Assert.That(cache.TryGetItem(tenantId, out TenantConnection? uncachedItem), Is.False);
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(uncachedItem, Is.Null);
            Assert.That(cachedItem, Is.Not.Null);
            Assert.That(cachedItem!.Id, Is.EqualTo(expectedItem.Id));
            Assert.That(cachedItem.ConnectionString, Is.EqualTo(expectedItem.ConnectionString));
            Assert.That(cachedItem.Schema, Is.EqualTo(expectedItem.Schema));
            Assert.That(cachedItem.Model, Is.EqualTo(expectedItem.Model));
            Assert.That(cachedItem.Provider, Is.EqualTo(expectedItem.Provider));
        });
        
        DistributedCacheMock.Verify(c => c.Set(expectedKey, expectedSerializedItem, It.IsAny<DistributedCacheEntryOptions>()), Times.Once);
        DistributedCacheMock.Verify(c => c.Get(expectedKey), Times.Exactly(2));
        DistributedCacheMock.Verify(c => c.Remove(expectedKey), Times.Once);
        
    }
}