using System.Collections.Immutable;
using System.Data;
using System.Text;
using System.Text.Json;
using Ballware.Generic.Data.Public;
using Ballware.Generic.Data.Repository;
using Ballware.Generic.Metadata;
using Ballware.Generic.Scripting;
using Ballware.Generic.Tenant.Data.Postgres.Internal;
using Ballware.Generic.Tenant.Data.Postgres.Tests.Utils;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Moq;
using Newtonsoft.Json;
using Npgsql;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Ballware.Generic.Tenant.Data.Postgres.Tests.Generic;

[TestFixture]
public class PostgresGenericProviderTest : DatabaseBackedBaseTest
{
    private PostgresTenantConfiguration Configuration { get; set; } = null!;
    private Mock<ITenantConnectionRepository> ConnectionRepositoryMock { get; set; } = null!;
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
        
        var tenantModel = new PostgresTenantModel()
        {
            Schema = Schema,
            DatabaseObjects = []
        };
        
        var serializedTenantModel = JsonSerializer.Serialize(tenantModel, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        
        await using (var tenantDb = new NpgsqlConnection(Configuration.TenantMasterConnectionString))
        {
            await tenantDb.DropSchemaForUserAsync("tenant", Schema, User);
            await tenantDb.CloseAsync();
        }
    
        SchemaProvider = new PostgresSchemaProvider(Configuration, ConnectionRepositoryMock.Object, new PostgresStorageProvider(ConnectionRepositoryMock.Object));
            
        await SchemaProvider.CreateOrUpdateTenantAsync(TenantId, "postgres", serializedTenantModel, UserId);
    }

    [TearDown]
    public async Task TearDown()
    {
        SchemaProvider = new PostgresSchemaProvider(Configuration, ConnectionRepositoryMock.Object, new PostgresStorageProvider(ConnectionRepositoryMock.Object));

        await SchemaProvider.DropTenantAsync(TenantId, UserId);
    }
    
    [Test]
    [TenantConnection("generictenant1")]
    public async Task Entity_with_identity_succeeds()
    {
        ScriptingExecutorMock.Setup(s => s.ListScript(It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction?>(),
                It.IsAny<Metadata.Tenant>(), It.IsAny<Entity>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IEnumerable<object>>()))
            .Returns((IDbConnection _, IDbTransaction? _, Metadata.Tenant _, Entity _, string _, IDictionary<string, object> _, IEnumerable<object> items) => items);            
        
        ScriptingExecutorMock.Setup(s => s.ByIdScriptAsync(It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction?>(),
                It.IsAny<Metadata.Tenant>(), It.IsAny<Entity>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<object>()))
            .ReturnsAsync((IDbConnection _, IDbTransaction? _, Metadata.Tenant _, Entity _, string _, IDictionary<string, object> _, IDictionary<string, object> item) => item);            
        
        ScriptingExecutorMock.Setup(s => s.RemovePreliminaryCheckAsync(It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction?>(),
                It.IsAny<Metadata.Tenant>(), It.IsAny<Entity>(), UserId, It.IsAny<IDictionary<string, object>>(), It.IsAny<object>()))
            .ReturnsAsync((IDbConnection _, IDbTransaction? _, Metadata.Tenant _, Entity _, Guid? _, IDictionary<string, object> _, IDictionary<string, object> item) => (true, []));            
        
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
        
        var genericProvider = new PostgresGenericProvider(new PostgresStorageProvider(ConnectionRepositoryMock.Object), app.Services);

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
                new QueryEntry() { Identifier = "primary", Query = "select uuid as id, coltextline, colnumber from testentity where tenant_id=@tenant_id::uuid and uuid = @id::uuid" }
            ],
            SaveStatement = [
                new QueryEntry() { Identifier = "primary", Query = "INSERT INTO testentity (uuid, tenant_id, coltextline, colnumber, creator_id, create_stamp) VALUES (@id, @tenant_id, @coltextline, @colnumber, @claim_sub::uuid, NOW()) ON CONFLICT (tenant_id, uuid) DO UPDATE SET coltextline = @coltextline, colnumber = @colnumber, last_changer_id = @claim_sub::uuid, last_change_stamp = NOW()" }
            ],
            RemoveStatement = "delete from testentity where tenant_id=@tenant_id and uuid=@id"
        };

        var newEntry = await genericProvider.NewAsync<dynamic>(Tenant, entity, "notexistingforcedefault", Claims);

        Assert.That(newEntry, Is.Not.Null);

        await genericProvider.SaveAsync(Tenant, entity, UserId, "primary", Claims, newEntry);

        var byIdEntry = await genericProvider.ByIdAsync<dynamic>(Tenant, entity, "primary", Claims, newEntry.id);
        
        Assert.That(byIdEntry, Is.Not.Null);
        Assert.That(byIdEntry.coltextline, Is.EqualTo("test textline"));
        Assert.That(byIdEntry.colnumber, Is.EqualTo(3));
        
        var queryResult = await genericProvider.QueryAsync<dynamic>(Tenant, entity, "notexistingforcedefault", Claims,
            ImmutableDictionary<string, object>.Empty);
        
        Assert.That(queryResult, Is.Not.Null);
        Assert.That(queryResult.Count(), Is.EqualTo(1));
        
        var queryEntry = queryResult.First();
        
        Assert.That(queryEntry, Is.Not.Null);
        Assert.That(queryEntry.coltextline, Is.EqualTo("test textline"));
        Assert.That(queryEntry.colnumber, Is.EqualTo(3));
        
        byIdEntry.coltextline = "test textline updated";
        byIdEntry.colnumber = (object)7;
        
        await genericProvider.SaveAsync(Tenant, entity, UserId, "notexistingforcedefault", Claims, byIdEntry);

        byIdEntry = await genericProvider.ByIdAsync<dynamic>(Tenant, entity, "primary", Claims, newEntry.id);
        var scalarNumberColumnValue = await genericProvider.GetScalarValueAsync<int>(Tenant, entity, "colnumber", newEntry.id, 0);
        
        Assert.That(byIdEntry, Is.Not.Null);
        Assert.That(byIdEntry.coltextline, Is.EqualTo("test textline updated"));
        Assert.That(byIdEntry.colnumber, Is.EqualTo(7));
        Assert.That(scalarNumberColumnValue, Is.EqualTo(7));
        
        var countResult = await genericProvider.CountAsync(Tenant, entity, "count", Claims, ImmutableDictionary<string, object>.Empty);
        
        Assert.That(countResult, Is.EqualTo(1));
        
        var secondEntry = await genericProvider.NewAsync<dynamic>(Tenant, entity, "primary", Claims);

        Assert.That(secondEntry, Is.Not.Null);
        
        secondEntry.Coltextline = "test textline second entry";

        await genericProvider.SaveAsync(Tenant, entity, UserId, "primary", Claims, secondEntry);
        
        countResult = await genericProvider.CountAsync(Tenant, entity, "count", Claims, ImmutableDictionary<string, object>.Empty);
        
        Assert.That(countResult, Is.EqualTo(2)); 
        
        await genericProvider.RemoveAsync(Tenant, entity, UserId, Claims, queryEntry.id);
        
        scalarNumberColumnValue = await genericProvider.GetScalarValueAsync<int>(Tenant, entity, "Colnumber", queryEntry.id, -1);
        countResult = await genericProvider.CountAsync(Tenant, entity, "count", Claims, ImmutableDictionary<string, object>.Empty);
        
        Assert.That(countResult, Is.EqualTo(1));     
        Assert.That(scalarNumberColumnValue, Is.EqualTo(-1));
    }
    
    [Test]
    [TenantConnection("generictenant2")]
    public async Task Entity_import_export_succeeds()
    {
        ScriptingExecutorMock.Setup(s => s.ListScript(It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction?>(),
                It.IsAny<Metadata.Tenant>(), It.IsAny<Entity>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IEnumerable<object>>()))
            .Returns((IDbConnection _, IDbTransaction? _, Metadata.Tenant _, Entity _, string _, IDictionary<string, object> _, IEnumerable<object> items) => items);            
        
        ScriptingExecutorMock.Setup(s => s.ByIdScriptAsync(It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction?>(),
                It.IsAny<Metadata.Tenant>(), It.IsAny<Entity>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<object>()))
            .ReturnsAsync((IDbConnection _, IDbTransaction? _, Metadata.Tenant _, Entity _, string _, IDictionary<string, object> _, IDictionary<string, object> item) => item);            
        
        ScriptingExecutorMock.Setup(s => s.RemovePreliminaryCheckAsync(It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction?>(),
                It.IsAny<Metadata.Tenant>(), It.IsAny<Entity>(), UserId, It.IsAny<IDictionary<string, object>>(), It.IsAny<object>()))
            .ReturnsAsync((IDbConnection _, IDbTransaction? _, Metadata.Tenant _, Entity _, Guid? _, IDictionary<string, object> _, IDictionary<string, object> item) => (true, []));            
        
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
        
        var genericProvider = new PostgresGenericProvider(new PostgresStorageProvider(ConnectionRepositoryMock.Object), app.Services);

        var entity = new Metadata.Entity()
        {
            Application = "test",
            Identifier = "testentity",
            ListQuery = [
                new QueryEntry() { Identifier = "primary", Query = "select uuid as id, coltextline, colnumber from testentity" },
                new QueryEntry() { Identifier = "exportjson", Query = "select uuid as id, coltextline, colnumber from testentity where tenant_id=@tenant_id and uuid = ANY(@id::uuid[]) order by colnumber" }
            ],
            NewQuery = [],
            ByIdQuery = [
                new QueryEntry() { Identifier = "primary", Query = "select uuid as id, coltextline, colnumber from testentity where tenant_id=@tenant_id and uuid=@id" }
            ],
            SaveStatement = [
                new QueryEntry() { Identifier = "primary", Query = "INSERT INTO testentity (uuid, tenant_id, coltextline, colnumber, creator_id, create_stamp) VALUES (@id::uuid, @tenant_id, @coltextline, @colnumber, @claim_sub::uuid, NOW()) ON CONFLICT (tenant_id, uuid) DO UPDATE SET coltextline = @coltextline, colnumber = @colnumber, last_changer_id = @claim_sub::uuid, last_change_stamp = NOW()" }
            ],
            RemoveStatement = "delete from testentity where tenant_id=@tenant_id and uuid=@id",
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
                { "id", Guid.NewGuid() },
                { "coltextline", "text line 1" },
                { "colnumber", 11 }
            },
            new ()
            {
                { "id", Guid.NewGuid() },
                { "coltextline", "text line 2" },
                { "colnumber", 12 }
            },
            new ()
            {
                { "id", Guid.NewGuid() },
                { "coltextline", "text line 3" },
                { "colnumber", 13 }
            },
        };

        var serializedExpectedItems = JsonConvert.SerializeObject(expectedItems);
        
        await genericProvider.ImportAsync(Tenant, entity, UserId, "importjson", Claims, new MemoryStream(Encoding.UTF8.GetBytes(serializedExpectedItems)),
            item => Task.FromResult(true));
        
        var result = await genericProvider.ExportAsync(Tenant, entity, "exportjson", Claims, new Dictionary<string, object>()
        {
            { "id", new StringValues(expectedItems.Select(item => item["id"].ToString()).ToArray()) }
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