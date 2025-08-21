using System.ComponentModel.DataAnnotations.Schema;
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

namespace Ballware.Generic.Tenant.Data.Postgres.Tests.MlModel;

class MlModelTrainEntry
{
    [Column("id")]
    public Guid Id { get; set; }
    
    [Column("name")]
    public string Name { get; set; }
    
    [Column("additional_param")]
    public int AdditionalParam { get; set; }
}

public class PostgresMlModelProviderTest : DatabaseBackedBaseTest
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
    
    private Metadata.Entity Entity { get; set; } = null!;
    
    private Dictionary<string, object> Claims { get; set; } = null!;
    
    private ITenantSchemaProvider SchemaProvider { get; set; } = null!;
    
    [SetUp]
    public async Task Setup()
    {
        UserId = Guid.NewGuid();
        TenantId = Guid.NewGuid();
        TenantConnection = null;
        
        Tenant = new Metadata.Tenant()
        {
            Id = TenantId,
            Provider = "mssql",
        };
        
        Entity = new Metadata.Entity()
        {
            Application = "test",
            Identifier = "testentity",
            ListQuery = [
                new QueryEntry() { Identifier = "primary", Query = "select uuid as id, name, additional_param from testentity" },
                new QueryEntry() { Identifier = "count", Query = "select count (*) from testentity" }
            ],
            NewQuery = [
                new QueryEntry() { Identifier = "primary", Query = "select gen_random_uuid() as id, null as name, null as additional_param" }
            ],
            ByIdQuery = [
                new QueryEntry() { Identifier = "primary", Query = "select uuid as id, name, additional_param from testentity where tenant_id=@tenant_id and uuid = @id" }
            ],
            SaveStatement = [
                new QueryEntry() { Identifier = "primary", Query = "INSERT INTO testentity (uuid, tenant_id, name, additional_param, creator_id, create_stamp) VALUES (@id, @tenant_id, @name, @additional_param, @claim_sub::uuid, NOW()) ON CONFLICT (tenant_id, uuid) DO UPDATE SET name = @name, additional_param = @additional_param, last_changer_id = @claim_sub::uuid, last_change_stamp = NOW()" }
            ],
            RemoveStatement = "delete from testentity where tenant_id=@tenant_id and uuid=@id"
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

        ConnectionRepositoryMock = new Mock<ITenantConnectionRepository>();
        EntityRepositoryMock = new Mock<ITenantEntityRepository>();
        ScriptingExecutorMock = new Mock<IGenericEntityScriptingExecutor>();
        
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
        
        var entityModel = new PostgresTableModel()
        {
            TableName = "testentity",
            NoIdentity = false,
            CustomColumns = [
                new PostgresColumnModel() { ColumnName = "name", ColumnType = PostgresColumnType.String, MaxLength = 50, Nullable = true },
                new PostgresColumnModel() { ColumnName = "additional_param", ColumnType = PostgresColumnType.Int, Nullable = true }
            ],
            CustomIndexes = []
        };
            
        var serializedEntityModel = JsonSerializer.Serialize(entityModel, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await SchemaProvider.CreateOrUpdateEntityAsync(TenantId, serializedEntityModel, UserId);
    }

    [TearDown]
    public async Task TearDown()
    {
        SchemaProvider = new PostgresSchemaProvider(Configuration, ConnectionRepositoryMock.Object, EntityRepositoryMock.Object, new PostgresStorageProvider(ConnectionRepositoryMock.Object));

        await SchemaProvider.DropTenantAsync(TenantId, UserId);
    }
    
    [Test]
    [TenantConnection("mlmodeltenant1")]
    public async Task FetchTrainData_succeeds()
    {
        // Arrange
        PreparedBuilder.Services.AddSingleton(ScriptingExecutorMock.Object);
        
        var app = PreparedBuilder.Build();
        
        var genericProvider = new PostgresGenericProvider(new PostgresStorageProvider(ConnectionRepositoryMock.Object), app.Services);

        var expectedList = new List<MlModelTrainEntry>
        {
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 1",
                AdditionalParam = 100
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 2",
                AdditionalParam = 101
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 3",
                AdditionalParam = 102
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 4",
                AdditionalParam = 103
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 5",
                AdditionalParam = 104
            }
        };
        
        foreach (var entry in expectedList)
        {
            var newEntry = await genericProvider.NewAsync<dynamic>(Tenant, Entity, "primary", Claims);

            newEntry.id = entry.Id;
            newEntry.name = entry.Name;
            newEntry.additional_param = entry.AdditionalParam;

            await genericProvider.SaveAsync(Tenant, Entity, UserId, "primary", Claims, newEntry);
        }
        
        var mlModelprovider = new PostgresMlModelProvider(new PostgresStorageProvider(ConnectionRepositoryMock.Object));

        var mlModel = new Metadata.MlModel()
        {
            TrainSql = "select uuid as id, name, additional_param as AdditionalParam from testentity where tenant_id=@tenant_id order by name",
        };
        
        // Act
        var actualList = (await mlModelprovider.TrainDataByModelAsync<MlModelTrainEntry>(Tenant, mlModel)).ToList();
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(actualList.Count, Is.EqualTo(expectedList.Count));
            
            foreach (var (e, a) in expectedList.Zip(actualList))
            {
                Assert.That(a.Id, Is.EqualTo(e.Id));
                Assert.That(a.Name, Is.EqualTo(e.Name)); 
                Assert.That(a.AdditionalParam, Is.EqualTo(e.AdditionalParam)); 
            }
        });
    }
}