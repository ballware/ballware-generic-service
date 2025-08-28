using System.Collections.Immutable;
using System.Data;
using System.Text.Json;
using Ballware.Generic.Data.Public;
using Ballware.Generic.Data.Repository;
using Ballware.Generic.Metadata;
using Ballware.Generic.Scripting;
using Ballware.Generic.Tenant.Data.SqlServer.Internal;
using Ballware.Generic.Tenant.Data.SqlServer.Tests.Utils;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Ballware.Generic.Tenant.Data.SqlServer.Tests.Statistic;

class StatisticResultEntry
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int AdditionalParam { get; set; }
}

public class SqlServerStatisticProviderTest : DatabaseBackedBaseTest
{
    private SqlServerTenantConfiguration Configuration { get; set; } = null!;
    private Mock<ITenantConnectionRepository> ConnectionRepositoryMock { get; set; } = null!;
    private Mock<ITenantEntityRepository> EntityRepositoryMock { get; set; } = null!;
    private Mock<IGenericEntityScriptingExecutor> EntityScriptingExecutorMock { get; set; } = null!;
    private Mock<IStatisticScriptingExecutor> StatisticScriptingExecutorMock { get; set; } = null!;

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
                new QueryEntry() { Identifier = "primary", Query = "select Uuid as Id, Name, AdditionalParam from testentity" },
                new QueryEntry() { Identifier = "count", Query = "select count (*) from testentity" }
            ],
            NewQuery = [
                new QueryEntry() { Identifier = "primary", Query = "select Id=NEWID(), Name=null, AdditionalParam=null" }
            ],
            ByIdQuery = [
                new QueryEntry() { Identifier = "primary", Query = "select Uuid as Id, Name, AdditionalParam from testentity where TenantId=@tenantId and Uuid = @id" }
            ],
            SaveStatement = [
                new QueryEntry() { Identifier = "primary", Query = "update testentity set Name=@Name, AdditionalParam=@AdditionalParam, LastChangerId = @claim_sub, LastChangeStamp = GETDATE() where TenantId=@tenantId and Uuid=@Id; if @@ROWCOUNT=0 begin insert into testentity (Uuid, TenantId, Name, AdditionalParam, CreatorId, CreateStamp) select @Id, @tenantId, @Name, @AdditionalParam, @claim_sub, GETDATE() where not exists (select * from testentity where TenantId=@tenantId and Uuid=@Id) end" }
            ],
            RemoveStatement = "delete from testentity where TenantId=@tenantId and Uuid=@id"
        };

        Claims = new Dictionary<string, object> { { "sub", UserId.ToString() } };

        var testMethodInfo = TestContext.CurrentContext.Test.Method;

        var testConnectionAttribute = testMethodInfo?.GetCustomAttributes<TenantConnectionAttribute>(false).FirstOrDefault();

        Schema = testConnectionAttribute?.Schema ?? "dbo";
        User = testConnectionAttribute?.User ?? $"tenant_{TenantId.ToString().ToLower()}";
        
        SqlMapper.AddTypeHandler(new SqlServerColumnTypeHandler());
        
        Configuration = new SqlServerTenantConfiguration()
        {
            TenantMasterConnectionString = MasterConnectionString/*PreparedBuilder.Configuration.GetConnectionString("TenantConnection")*/,
            UseContainedDatabase = false
        };

        ConnectionRepositoryMock = new Mock<ITenantConnectionRepository>();
        EntityScriptingExecutorMock = new Mock<IGenericEntityScriptingExecutor>();
        EntityRepositoryMock = new Mock<ITenantEntityRepository>();
        StatisticScriptingExecutorMock = new Mock<IStatisticScriptingExecutor>();
        
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
        
        var tenantModel = new SqlServerTenantModel()
        {
            Schema = Schema,
            DatabaseObjects = []
        };
        
        var serializedTenantModel = JsonSerializer.Serialize(tenantModel, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        {
            await using var tenantDb = new SqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", Schema, User);
            await tenantDb.CloseAsync();
        }
        
        SchemaProvider = new SqlServerSchemaProvider(Configuration, ConnectionRepositoryMock.Object, EntityRepositoryMock.Object, new SqlServerStorageProvider(ConnectionRepositoryMock.Object));
            
        await SchemaProvider.CreateOrUpdateTenantAsync(TenantId, "mssql", serializedTenantModel, UserId);
        
        var entityModel = new SqlServerTableModel()
        {
            TableName = "testentity",
            NoIdentity = false,
            CustomColumns = [
                new SqlServerColumnModel() { ColumnName = "Name", ColumnType = SqlServerColumnType.String, MaxLength = 50, Nullable = true },
                new SqlServerColumnModel() { ColumnName = "AdditionalParam", ColumnType = SqlServerColumnType.Int, Nullable = true }
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
        SchemaProvider = new SqlServerSchemaProvider(Configuration, ConnectionRepositoryMock.Object, EntityRepositoryMock.Object, new SqlServerStorageProvider(ConnectionRepositoryMock.Object));

        await SchemaProvider.DropTenantAsync(TenantId, UserId);
    }
    
    [Test]
    [TenantConnection("statistictenant1")]
    public async Task FetchData_succeeds()
    {
        // Arrange
        using var listener = new SqlClientListener();

        StatisticScriptingExecutorMock
            .Setup(m => m.FetchScript<StatisticResultEntry>(
                It.IsAny<IDbConnection>(),
                It.IsAny<IDbTransaction?>(), 
                It.IsAny<Metadata.Tenant>(),
                It.IsAny<Metadata.Statistic>(), 
                It.IsAny<Guid>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IEnumerable<StatisticResultEntry>>()))
            .Returns((IDbConnection db, 
                IDbTransaction? transaction, 
                Metadata.Tenant tenant,
                Metadata.Statistic statistic,
                Guid userId,
                IDictionary<string, object> claims, 
                IEnumerable<StatisticResultEntry> results) =>
            {
                return results;
            });
        
        PreparedBuilder.Services.AddSingleton(EntityScriptingExecutorMock.Object);
        PreparedBuilder.Services.AddSingleton(StatisticScriptingExecutorMock.Object);
        
        var app = PreparedBuilder.Build();
        
        var genericProvider = new SqlServerGenericProvider(new SqlServerStorageProvider(ConnectionRepositoryMock.Object), app.Services);

        var expectedList = new List<StatisticResultEntry>
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
            var newEntry = await genericProvider.NewAsync<dynamic>(Tenant, Entity, "primary", UserId, Claims);

            newEntry.Id = entry.Id;
            newEntry.Name = entry.Name;
            newEntry.AdditionalParam = entry.AdditionalParam;

            await genericProvider.SaveAsync(Tenant, Entity, "primary", UserId, Claims, newEntry);
        }
        
        var statisticProvider = new SqlServerStatisticProvider(new SqlServerStorageProvider(ConnectionRepositoryMock.Object), app.Services);

        var statistic = new Metadata.Statistic()
        {
            Identifier = "statistic1",
            FetchSql = "select Uuid as Id, Name, AdditionalParam from testentity where TenantId=@tenantId order by Name",
        };
        
        // Act
        var actualList = (await statisticProvider.FetchDataAsync<StatisticResultEntry>(Tenant, statistic, UserId, ImmutableDictionary<string, object>.Empty, ImmutableDictionary<string, object>.Empty)).ToList();
        
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