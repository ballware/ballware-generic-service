using System.Collections.Immutable;
using Ballware.Generic.Data.Ef.Configuration;
using Ballware.Generic.Data.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Data.Ef.Postgres.Tests.Repository;

[TestFixture]
public class TenantEntityBaseRepositoryTest : RepositoryBaseTest
{
    [Test]
    public async Task Save_and_remove_value_succeeds()
    {
        using var scope = Application.Services.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<ITenantEntityRepository>();

        var expectedValue = await repository.NewAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty);

        expectedValue.Entity = "fake_entity";
        expectedValue.Model = "fake_model";
        
        await repository.SaveAsync(TenantId, null, "primary", ImmutableDictionary<string, object>.Empty, expectedValue);

        var actualByIdValue = await repository.ByIdAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty, expectedValue.Id);
        var actualByEntityValue = await repository.ByEntityAsync(TenantId, expectedValue.Entity);
        
        Assert.Multiple(() =>
        {
            Assert.That(actualByIdValue, Is.Not.Null);
            Assert.That(actualByIdValue?.Id, Is.EqualTo(expectedValue.Id));
            Assert.That(actualByIdValue?.Entity, Is.EqualTo(expectedValue.Entity));
            Assert.That(actualByIdValue?.Model, Is.EqualTo(expectedValue.Model));
            
            Assert.That(actualByEntityValue, Is.Not.Null);
            Assert.That(actualByEntityValue?.Id, Is.EqualTo(expectedValue.Id));
            Assert.That(actualByEntityValue?.Entity, Is.EqualTo(expectedValue.Entity));
            Assert.That(actualByEntityValue?.Model, Is.EqualTo(expectedValue.Model));
        });

        actualByIdValue.Model = "changed_fake_model";
        
        await repository.SaveAsync(TenantId, null, "primary", ImmutableDictionary<string, object>.Empty, actualByIdValue);
        
        actualByIdValue = await repository.ByIdAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty, expectedValue.Id);
        
        Assert.Multiple(() =>
        {
            Assert.That(actualByIdValue, Is.Not.Null);
            Assert.That(actualByIdValue?.Id, Is.EqualTo(expectedValue.Id));
            Assert.That(actualByIdValue?.Entity, Is.EqualTo(expectedValue.Entity));
            Assert.That(actualByIdValue?.Model, Is.EqualTo("changed_fake_model"));
        });
       

        var removeParams = new Dictionary<string, object>([new KeyValuePair<string, object>("Id", expectedValue.Id)]);

        var removeResult = await repository.RemoveAsync(TenantId, null, ImmutableDictionary<string, object>.Empty, removeParams);

        Assert.Multiple(() =>
        {
            Assert.That(removeResult.Result, Is.True);
        });

        actualByIdValue = await repository.ByIdAsync(TenantId, "primary", ImmutableDictionary<string, object>.Empty, expectedValue.Id);

        Assert.That(actualByIdValue, Is.Null);
    }

    protected override void PrepareApplication(WebApplicationBuilder builder)
    {
        var storageOptions = PreparedBuilder.Configuration.GetSection("Storage").Get<StorageOptions>();
        
        builder.Services.AddBallwareTenantStorageForPostgres(storageOptions, MasterConnectionString);
        builder.Services.AddAutoMapper(config =>
        {
            config.AddBallwareTenantStorageMappings();
        });
    }
}