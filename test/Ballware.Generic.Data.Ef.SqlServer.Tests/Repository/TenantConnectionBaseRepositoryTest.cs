using System.Collections.Immutable;
using Ballware.Generic.Data.Ef.Configuration;
using Ballware.Generic.Data.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Data.Ef.SqlServer.Tests.Repository;

[TestFixture]
public class TenantConnectionBaseRepositoryTest : RepositoryBaseTest
{
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
        
        await repository.SaveAsync(null, "primary", ImmutableDictionary<string, object>.Empty, expectedValue);

        var actualValue = await repository.ByIdAsync("primary", ImmutableDictionary<string, object>.Empty, expectedValue.Id);

        Assert.Multiple(() =>
        {
            Assert.That(actualValue, Is.Not.Null);
            Assert.That(actualValue?.Id, Is.EqualTo(expectedValue.Id));
            Assert.That(actualValue?.Model, Is.EqualTo(expectedValue.Model));
            Assert.That(actualValue?.Provider, Is.EqualTo(expectedValue.Provider));
            Assert.That(actualValue?.Schema, Is.EqualTo(expectedValue.Schema));
        });

        actualValue.Model = "changed_fake_model";
        
        await repository.SaveAsync(null, "primary", ImmutableDictionary<string, object>.Empty, actualValue);
        
        actualValue = await repository.ByIdAsync("primary", ImmutableDictionary<string, object>.Empty, expectedValue.Id);
        
        Assert.Multiple(() =>
        {
            Assert.That(actualValue, Is.Not.Null);
            Assert.That(actualValue?.Id, Is.EqualTo(expectedValue.Id));
            Assert.That(actualValue?.Model, Is.EqualTo("changed_fake_model"));
            Assert.That(actualValue?.Provider, Is.EqualTo(expectedValue.Provider));
            Assert.That(actualValue?.Schema, Is.EqualTo(expectedValue.Schema));
        });
       

        var removeParams = new Dictionary<string, object>([new KeyValuePair<string, object>("Id", expectedValue.Id)]);

        var removeResult = await repository.RemoveAsync(null, ImmutableDictionary<string, object>.Empty, removeParams);

        Assert.Multiple(() =>
        {
            Assert.That(removeResult.Result, Is.True);
        });

        actualValue = await repository.ByIdAsync("primary", ImmutableDictionary<string, object>.Empty, expectedValue.Id);

        Assert.That(actualValue, Is.Null);
    }

    protected override void PrepareApplication(WebApplicationBuilder builder)
    {
        var storageOptions = PreparedBuilder.Configuration.GetSection("Storage").Get<StorageOptions>();
        
        builder.Services.AddBallwareTenantStorageForSqlServer(storageOptions, MasterConnectionString);
        builder.Services.AddAutoMapper(config =>
        {
            config.AddBallwareTenantStorageMappings();
        });
    }
}