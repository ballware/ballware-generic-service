using System.Collections.Immutable;
using System.Data;
using System.Text.Json;
using Ballware.Generic.Data.Public;
using Ballware.Generic.Data.Repository;
using Ballware.Generic.Metadata;
using Ballware.Generic.Scripting;
using Ballware.Generic.Tenant.Data.Postgres.Internal;
using Ballware.Generic.Tenant.Data.Postgres.Tests.Utils;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;

namespace Ballware.Generic.Tenant.Data.Postgres.Tests.Generic;

[TestFixture]
public class PostgresGenericScriptingDataAdapterTest : DatabaseBackedBaseTest
{
    private PostgresTenantConfiguration Configuration { get; set; } = null!;
    private Mock<ITenantConnectionRepository> ConnectionRepositoryMock { get; set; } = null!;
    private Mock<ITenantEntityRepository> EntityRepositoryMock { get; set; } = null!;
    private Mock<IGenericEntityScriptingExecutor> ScriptingExecutorMock { get; set; } = null!;
    
    private Guid TenantId { get; set; } = Guid.NewGuid();
    private Guid UserId { get; set; } = Guid.NewGuid();
    private TenantConnection TenantConnection { get; set; } = null!;
    private string Schema { get; set; } = null!;
    private string User { get; set; } = null!;
    
    private Metadata.Tenant Tenant { get; set; } = null!;
    
    private Dictionary<string, object> Claims { get; set; } = null!;
    
    private ITenantSchemaProvider SchemaProvider { get; set; } = null!;
    
    [SetUp]
    public async Task Setup()
    {
        UserId = Guid.NewGuid();
        TenantId = Guid.NewGuid();
        
        Tenant = new Metadata.Tenant()
        {
            Id = TenantId,
            Provider = "postgres",
        };

        Claims = new Dictionary<string, object> { { "sub", UserId.ToString() } };

        var testMethodInfo = TestContext.CurrentContext.Test.Method;

        var testConnectionAttribute = testMethodInfo?.GetCustomAttributes<TenantConnectionAttribute>(false).FirstOrDefault();

        Schema = testConnectionAttribute?.Schema ?? "public";
        User = testConnectionAttribute?.User ?? $"tenant_{TenantId.ToString("N").ToLower()}";
        
        SqlMapper.AddTypeHandler(new PostgresColumnTypeHandler());
        
        Configuration = new PostgresTenantConfiguration()
        {
            TenantMasterConnectionString = MasterConnectionString
        };
        
        ScriptingExecutorMock = new Mock<IGenericEntityScriptingExecutor>();
        ConnectionRepositoryMock = new Mock<ITenantConnectionRepository>();
        EntityRepositoryMock = new Mock<ITenantEntityRepository>();
        
        ConnectionRepositoryMock.Setup(m => m.NewAsync("primary", It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync((string _, IDictionary<string, object> _) => new TenantConnection()
            {
                Id = TenantId
            });
        ConnectionRepositoryMock.Setup(m => m.ByIdAsync(TenantId))
            .ReturnsAsync((Guid _) => TenantConnection);
        ConnectionRepositoryMock.Setup(m =>
                m.SaveAsync(UserId, "primary", It.IsAny<IDictionary<string, object>>(), It.IsAny<TenantConnection>()))
            .Callback((Guid? _, string _, IDictionary<string, object> _, TenantConnection connection) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(connection, Is.Not.Null);
                    Assert.That(connection.Id, Is.EqualTo(TenantId));
                    Assert.That(connection.Schema, Is.EqualTo(Schema));
                    
                    TenantConnection = connection;
                });    
            });
        
        EntityRepositoryMock.Setup(m => m.NewAsync(TenantId, "primary", It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync((Guid _, string _, IDictionary<string, object> _) => new TenantEntity()
            {
                Id = Guid.NewGuid()
            });        
        
        var tenantModel = new PostgresTenantModel()
        {
            Schema = Schema,
            DatabaseObjects = []
        };
        
        var serializedTenantModel = JsonSerializer.Serialize(tenantModel, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        {
            await using var tenantDb = new NpgsqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", Schema, User);
            await tenantDb.CloseAsync();
        }
        
        SchemaProvider = new PostgresSchemaProvider(Configuration, ConnectionRepositoryMock.Object, EntityRepositoryMock.Object, new PostgresStorageProvider(ConnectionRepositoryMock.Object));
            
        await SchemaProvider.CreateOrUpdateTenantAsync(TenantId, "postgres", serializedTenantModel, UserId);
    }

    [TearDown]
    public async Task TearDown()
    {
        await using var tenantDb = new NpgsqlConnection(Configuration.TenantMasterConnectionString);
        await tenantDb.DropSchemaForUserAsync("tenant", Schema, User);
        await tenantDb.CloseAsync();
    }
    
    [Test]
    [TenantConnection("generictenant2")]
    public async Task Dataadapter_operations_succeeds()
    {
        ScriptingExecutorMock.Setup(s => s.ListScript(It.IsAny<IScriptingEntityUserContext>(),
                It.IsAny<string>(), It.IsAny<IEnumerable<object>>()))
            .Returns((IScriptingEntityUserContext _, string _, IEnumerable<object> items) => items);            
        
        ScriptingExecutorMock.Setup(s => s.ByIdScriptAsync(It.IsAny<IScriptingEntityUserContext>(),
                It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync((IScriptingEntityUserContext _, string _, IDictionary<string, object> item) => item);            
        
        ScriptingExecutorMock.Setup(s => s.RemovePreliminaryCheckAsync(It.IsAny<IScriptingEntityUserContext>(),
                It.IsAny<object>()))
            .ReturnsAsync((IScriptingEntityUserContext _, IDictionary<string, object> item) => (true, []));            
        
        PreparedBuilder.Services.AddSingleton(ScriptingExecutorMock.Object);
        
        var app = PreparedBuilder.Build();

        var entityModel = new PostgresTableModel()
        {
            TableName = "testentity",
            NoIdentity = false,
            CustomColumns = [
                new PostgresColumnModel() { ColumnName = "coltextline", ColumnType = PostgresColumnType.String, MaxLength = 50, Nullable = true },
                new PostgresColumnModel() { ColumnName = "colnumber", ColumnType = PostgresColumnType.Int, Nullable = true }
            ],
            CustomIndexes = []
        };
            
        var serializedEntityModel = JsonSerializer.Serialize(entityModel, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await SchemaProvider.CreateOrUpdateEntityAsync(TenantId, serializedEntityModel, UserId);

        var storageProvider = new PostgresStorageProvider(ConnectionRepositoryMock.Object);
        var genericProvider = new PostgresGenericProvider(storageProvider, app.Services);

        var entity = new Metadata.Entity()
        {
            Application = "test",
            Identifier = "testentity",
            ListQuery = [
                new QueryEntry() { Identifier = "primary", Query = "select uuid as id, coltextline, colnumber from testentity" },
                new QueryEntry() { Identifier = "count", Query = "select count (*) from testentity" }
            ],
            NewQuery = [
                new QueryEntry() { Identifier = "primary", Query = "select gen_random_uuid() as id, 'test textline' as coltextline, 3 as colnumber" }
            ],
            ByIdQuery = [
                new QueryEntry() { Identifier = "primary", Query = "select uuid as id, coltextline, colnumber from testentity where tenant_id=@tenant_id and uuid = @id" }
            ],
            SaveStatement = [
                new QueryEntry() { Identifier = "primary", Query = "INSERT INTO testentity (uuid, tenant_id, coltextline, colnumber, creator_id, create_stamp) VALUES (@id, @tenant_id, @coltextline, @colnumber, @claim_sub::uuid, NOW()) ON CONFLICT (tenant_id, uuid) DO UPDATE SET coltextline = @coltextline, colnumber = @colnumber, last_changer_id = @claim_sub::uuid, last_change_stamp = NOW()" }
            ],
            RemoveStatement = "delete from testentity where tenant_id=@tenant_id and uuid=@id",
            ScalarValueQuery = "primary"
        };

        var scriptingDataAdapter = new PostgresGenericScriptingDataAdapter(genericProvider);

        using var connection = await storageProvider.OpenConnectionAsync(TenantId);
        
        var context = DefaultScriptingEntityUserContext.Create(connection, Tenant, entity, UserId, Claims);

        var queryNewActual = scriptingDataAdapter.QueryNew(context,
            "primary", ImmutableDictionary<string, object>.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(queryNewActual, Is.Not.Null);
        });
        
        queryNewActual.Colnumber = (object)4;
        queryNewActual.Coltextline = null;
        
        scriptingDataAdapter.Save(context, "primary", queryNewActual);
        
        var querySingleActual = scriptingDataAdapter.QuerySingle(context,
            "primary", ImmutableDictionary.CreateRange(new[]
            {
                new KeyValuePair<string, object>("id", queryNewActual.id),
            }));
        
        Assert.Multiple(() =>
        {   
            Assert.That(querySingleActual, Is.Not.Null);
            
            Guid actualId = querySingleActual?.id;
            Guid expectedId = queryNewActual.id;
            
            Assert.That(actualId, Is.EqualTo(expectedId));
        });
        
        var queryListActual = scriptingDataAdapter.QueryList(context,
            "primary", ImmutableDictionary<string, object>.Empty)?.ToList();
        
        Assert.Multiple(() =>
        {   
            Assert.That(queryListActual, Is.Not.Null);
            Assert.That(queryListActual.Count, Is.EqualTo(1));
        });

        var queryCountActual = scriptingDataAdapter.Count(context,
            "count", ImmutableDictionary<string, object>.Empty);
        
        Assert.Multiple(() =>
        {   
            Assert.That(queryCountActual, Is.EqualTo(1));
        });

        var queryScalarIntActual = scriptingDataAdapter.QueryScalarValue(context,
            "colnumber", ImmutableDictionary.CreateRange(new[]
            {
                new KeyValuePair<string, object>("id", queryNewActual.id),
            }));
        
        var queryScalarNullActual = scriptingDataAdapter.QueryScalarValue(context,
            "coltextline", ImmutableDictionary.CreateRange(new[]
            {
                new KeyValuePair<string, object>("id", queryNewActual.id),
            }));
        
        var queryScalarNotExistingActual = scriptingDataAdapter.QueryScalarValue(context,
            "colnotexisting", ImmutableDictionary.CreateRange(new[]
            {
                new KeyValuePair<string, object>("id", queryNewActual.id),
            }));
        
        Assert.Multiple(() =>
        {
            Assert.That(queryScalarIntActual, Is.EqualTo(4));
            Assert.That(queryScalarNullActual, Is.Null);
            Assert.That(queryScalarNotExistingActual, Is.Null);
        });
        
        scriptingDataAdapter.Remove(context, ImmutableDictionary.CreateRange(new[]
        {
            new KeyValuePair<string, object>("id", queryNewActual.id),
        }));
        
        var queryRemovedCountActual = scriptingDataAdapter.Count(context,
            "count", ImmutableDictionary<string, object>.Empty);
        
        Assert.Multiple(() =>
        {   
            Assert.That(queryRemovedCountActual, Is.EqualTo(0));
        });
    }
}