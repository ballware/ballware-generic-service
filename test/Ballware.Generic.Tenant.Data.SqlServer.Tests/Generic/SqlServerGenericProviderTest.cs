using System.Collections.Immutable;
using System.Data;
using System.Text;
using System.Text.Json;
using Ballware.Generic.Data.Public;
using Ballware.Generic.Data.Repository;
using Ballware.Generic.Metadata;
using Ballware.Generic.Scripting;
using Ballware.Generic.Tenant.Data.SqlServer.Internal;
using Ballware.Generic.Tenant.Data.SqlServer.Tests.Utils;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Moq;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Ballware.Generic.Tenant.Data.SqlServer.Tests.Generic;

[TestFixture]
public class SqlServerGenericProviderTest : DatabaseBackedBaseTest
{
    private SqlServerTenantConfiguration Configuration { get; set; } = null!;
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
        TenantConnection = null;
        
        Tenant = new Metadata.Tenant()
        {
            Id = TenantId,
            Provider = "mssql",
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
        
        var tenantModel = new SqlServerTenantModel()
        {
            Schema = Schema,
            DatabaseObjects = []
        };
        
        var serializedTenantModel = JsonSerializer.Serialize(tenantModel, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        
        await using (var tenantDb = new SqlConnection(Configuration.TenantMasterConnectionString))
        {
            await tenantDb.DropSchemaForUserAsync("tenant", Schema, User);
            await tenantDb.CloseAsync();
        }
    
        SchemaProvider = new SqlServerSchemaProvider(Configuration, ConnectionRepositoryMock.Object, EntityRepositoryMock.Object, new SqlServerStorageProvider(ConnectionRepositoryMock.Object));
            
        await SchemaProvider.CreateOrUpdateTenantAsync(TenantId, "mssql", serializedTenantModel, UserId);
    }

    [TearDown]
    public async Task TearDown()
    {
        SchemaProvider = new SqlServerSchemaProvider(Configuration, ConnectionRepositoryMock.Object, EntityRepositoryMock.Object, new SqlServerStorageProvider(ConnectionRepositoryMock.Object));

        await SchemaProvider.DropTenantAsync(TenantId, UserId);
    }
    
    [Test]
    [TenantConnection("generictenant1")]
    public async Task Entity_with_identity_succeeds()
    {
        using var listener = new SqlClientListener();

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

        var entityModel = new SqlServerTableModel()
        {
            TableName = "testentity",
            NoIdentity = false,
            CustomColumns = [
                new SqlServerColumnModel() { ColumnName = "Coltextline", ColumnType = SqlServerColumnType.String, MaxLength = 50, Nullable = true },
                new SqlServerColumnModel() { ColumnName = "Colnumber", ColumnType = SqlServerColumnType.Int, Nullable = true }
            ],
            CustomIndexes = []
        };
            
        var serializedEntityModel = JsonSerializer.Serialize(entityModel, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await SchemaProvider.CreateOrUpdateEntityAsync(TenantId, serializedEntityModel, UserId);
        
        var genericProvider = new SqlServerGenericProvider(new SqlServerStorageProvider(ConnectionRepositoryMock.Object), app.Services);

        var entity = new Metadata.Entity()
        {
            Application = "test",
            Identifier = "testentity",
            ListQuery = [
                new QueryEntry() { Identifier = "primary", Query = "select Uuid as Id, Coltextline, Colnumber from testentity" },
                new QueryEntry() { Identifier = "count", Query = "select count (*) from testentity" }
            ],
            NewQuery = [
                new QueryEntry() { Identifier = "primary", Query = "select Id=NEWID(), Coltextline = 'test textline', Colnumber = 3" }
            ],
            ByIdQuery = [
                new QueryEntry() { Identifier = "primary", Query = "select Uuid as Id, Coltextline, Colnumber from testentity where TenantId=@tenantId and Uuid = @id" }
            ],
            SaveStatement = [
                new QueryEntry() { Identifier = "primary", Query = "update testentity set ColTextline=@ColTextline, Colnumber=@Colnumber, LastChangerId = @claim_sub, LastChangeStamp = GETDATE() where TenantId=@tenantId and Uuid=@Id; if @@ROWCOUNT=0 begin insert into testentity (Uuid, TenantId, Coltextline, Colnumber, CreatorId, CreateStamp) select @Id, @tenantId, @Coltextline, @Colnumber, @claim_sub, GETDATE() where not exists (select * from testentity where TenantId=@tenantId and Uuid=@Id) end" }
            ],
            RemoveStatement = "delete from testentity where TenantId=@tenantId and Uuid=@id"
        };

        var newEntry = await genericProvider.NewAsync<dynamic>(Tenant, entity, "notexistingforcedefault", UserId, Claims);

        Assert.That(newEntry, Is.Not.Null);

        await genericProvider.SaveAsync(Tenant, entity, "primary", UserId, Claims, newEntry);

        var byIdEntry = await genericProvider.ByIdAsync<dynamic>(Tenant, entity, "primary", UserId, Claims, newEntry.Id);
        
        Assert.That(byIdEntry, Is.Not.Null);
        Assert.That(byIdEntry.Coltextline, Is.EqualTo("test textline"));
        Assert.That(byIdEntry.Colnumber, Is.EqualTo(3));
        
        var queryResult = await genericProvider.QueryAsync<dynamic>(Tenant, entity, "notexistingforcedefault", UserId, Claims,
            ImmutableDictionary<string, object>.Empty);
        
        Assert.That(queryResult, Is.Not.Null);
        Assert.That(queryResult.Count(), Is.EqualTo(1));
        
        var queryEntry = queryResult.First();
        
        Assert.That(queryEntry, Is.Not.Null);
        Assert.That(queryEntry.Coltextline, Is.EqualTo("test textline"));
        Assert.That(queryEntry.Colnumber, Is.EqualTo(3));
        
        byIdEntry.Coltextline = "test textline updated";
        byIdEntry.Colnumber = (object)7;
        
        await genericProvider.SaveAsync(Tenant, entity, "notexistingforcedefault", UserId, Claims, byIdEntry);

        byIdEntry = await genericProvider.ByIdAsync<dynamic>(Tenant, entity, "primary", UserId, Claims, newEntry.Id);
        var scalarNumberColumnValue = await genericProvider.GetScalarValueAsync<int>(Tenant, entity, "Colnumber", newEntry.Id, 0);
        
        Assert.That(byIdEntry, Is.Not.Null);
        Assert.That(byIdEntry.Coltextline, Is.EqualTo("test textline updated"));
        Assert.That(byIdEntry.Colnumber, Is.EqualTo(7));
        Assert.That(scalarNumberColumnValue, Is.EqualTo(7));
        
        var countResult = await genericProvider.CountAsync(Tenant, entity, "count", UserId, Claims, ImmutableDictionary<string, object>.Empty);
        
        Assert.That(countResult, Is.EqualTo(1));
        
        var secondEntry = await genericProvider.NewAsync<dynamic>(Tenant, entity, "primary", UserId, Claims);

        Assert.That(secondEntry, Is.Not.Null);
        
        secondEntry.Coltextline = "test textline second entry";

        await genericProvider.SaveAsync(Tenant, entity, "primary", UserId, Claims, secondEntry);
        
        countResult = await genericProvider.CountAsync(Tenant, entity, "count", UserId, Claims, ImmutableDictionary<string, object>.Empty);
        
        Assert.That(countResult, Is.EqualTo(2)); 
        
        await genericProvider.RemoveAsync(Tenant, entity, UserId, Claims, queryEntry.Id);
        
        scalarNumberColumnValue = await genericProvider.GetScalarValueAsync<int>(Tenant, entity, "Colnumber", queryEntry.Id, -1);
        countResult = await genericProvider.CountAsync(Tenant, entity, "count", UserId, Claims, ImmutableDictionary<string, object>.Empty);
        
        Assert.That(countResult, Is.EqualTo(1));     
        Assert.That(scalarNumberColumnValue, Is.EqualTo(-1));
    }
    
    [Test]
    [TenantConnection("generictenant2")]
    public async Task Entity_import_export_succeeds()
    {
        using var listener = new SqlClientListener();

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

        var entityModel = new SqlServerTableModel()
        {
            TableName = "testentity",
            NoIdentity = false,
            CustomColumns = [
                new SqlServerColumnModel() { ColumnName = "Coltextline", ColumnType = SqlServerColumnType.String, MaxLength = 50, Nullable = true },
                new SqlServerColumnModel() { ColumnName = "Colnumber", ColumnType = SqlServerColumnType.Int, Nullable = true }
            ],
            CustomIndexes = []
        };
            
        var serializedEntityModel = JsonSerializer.Serialize(entityModel, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await SchemaProvider.CreateOrUpdateEntityAsync(TenantId, serializedEntityModel, UserId);
        
        var genericProvider = new SqlServerGenericProvider(new SqlServerStorageProvider(ConnectionRepositoryMock.Object), app.Services);

        var entity = new Metadata.Entity()
        {
            Application = "test",
            Identifier = "testentity",
            ListQuery = [
                new QueryEntry() { Identifier = "primary", Query = "select Uuid as Id, Coltextline, Colnumber from testentity" },
                new QueryEntry() { Identifier = "exportjson", Query = "select Uuid as Id, Coltextline, Colnumber from testentity where TenantId=@tenantId and Uuid in @id" }
            ],
            NewQuery = [],
            ByIdQuery = [
                new QueryEntry() { Identifier = "primary", Query = "select Uuid as Id, Coltextline, Colnumber from testentity where TenantId=@tenantId and Uuid = @id" }
            ],
            SaveStatement = [
                new QueryEntry() { Identifier = "primary", Query = "update testentity set ColTextline=@ColTextline, Colnumber=@Colnumber, LastChangerId = @claim_sub, LastChangeStamp = GETDATE() where TenantId=@tenantId and Uuid=@Id; if @@ROWCOUNT=0 begin insert into testentity (Uuid, TenantId, Coltextline, Colnumber, CreatorId, CreateStamp) select @Id, @tenantId, @Coltextline, @Colnumber, @claim_sub, GETDATE() where not exists (select * from testentity where TenantId=@tenantId and Uuid=@Id) end" }
            ],
            RemoveStatement = "delete from testentity where TenantId=@tenantId and Uuid=@id",
            CustomFunctions = [
                new CustomFunctionEntry()
                {
                    Id = "importjson", Options = new CustomFunctionOptions()
                    {
                        Format = "json"
                    },
                    Type = CustomFunctionTypes.Import
                },
                new CustomFunctionEntry()
                {
                    Id = "exportjson", Options = new CustomFunctionOptions()
                    {
                        Format = "json"
                    },
                    Type = CustomFunctionTypes.Export
                }
            ]
        };

        var expectedItems = new List<Dictionary<string, object>>()
        {
            new ()
            {
                { "Id", Guid.NewGuid() },
                { "Coltextline", "text line 1" },
                { "Colnumber", 11 }
            },
            new ()
            {
                { "Id", Guid.NewGuid() },
                { "Coltextline", "text line 2" },
                { "Colnumber", 12 }
            },
            new ()
            {
                { "Id", Guid.NewGuid() },
                { "Coltextline", "text line 3" },
                { "Colnumber", 13 }
            },
        };

        var serializedExpectedItems = JsonConvert.SerializeObject(expectedItems);
        
        await genericProvider.ImportAsync(Tenant, entity, "importjson", UserId, Claims, new MemoryStream(Encoding.UTF8.GetBytes(serializedExpectedItems)),
            item => Task.FromResult(true));
        
        var result = await genericProvider.ExportAsync(Tenant, entity, "exportjson", UserId, Claims, new Dictionary<string, object>()
        {
            { "id", new StringValues(expectedItems.Select(item => item["Id"].ToString()).ToArray()) }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.MediaType, Is.EqualTo("application/json"));
            Assert.That(result.FileName, Is.EqualTo("exportjson.json"));
            
            var actualSerializedItems = Encoding.UTF8.GetString(result.Data, 0, result.Data.Length);
            
            Assert.That(actualSerializedItems, Is.EqualTo(serializedExpectedItems));
            
        });
    }
}