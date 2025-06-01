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
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Ballware.Generic.Tenant.Data.SqlServer.Tests.Generic;

[TestFixture]
public class SqlServerGenericProviderTest : DatabaseBackedBaseTest
{
    private SqlServerTenantConfiguration Configuration { get; set; } = null!;
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
        
        SchemaProvider = new SqlServerSchemaProvider(Configuration, ConnectionRepositoryMock.Object, new SqlServerStorageProvider(ConnectionRepositoryMock.Object));
            
        await SchemaProvider.CreateOrUpdateTenantAsync(TenantId, "mssql", serializedTenantModel, UserId);
    }

    [TearDown]
    public async Task TearDown()
    {
        await using var tenantDb = new SqlConnection(Configuration.TenantMasterConnectionString);
        await tenantDb.DropSchemaForUserAsync("tenant", Schema, User);
        await tenantDb.CloseAsync();
    }
    
    [Test]
    [TenantConnection("generictenant1")]
    public async Task Entity_with_identity_succeeds()
    {
        using var listener = new SqlClientListener();

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

        var newEntry = await genericProvider.NewAsync<dynamic>(Tenant, entity, "notexistingforcedefault", Claims);

        Assert.That(newEntry, Is.Not.Null);

        await genericProvider.SaveAsync(Tenant, entity, UserId, "primary", Claims, newEntry);

        var byIdEntry = await genericProvider.ByIdAsync<dynamic>(Tenant, entity, "primary", Claims, newEntry.Id);
        
        Assert.That(byIdEntry, Is.Not.Null);
        Assert.That(byIdEntry.Coltextline, Is.EqualTo("test textline"));
        Assert.That(byIdEntry.Colnumber, Is.EqualTo(3));
        
        var queryResult = await genericProvider.QueryAsync<dynamic>(Tenant, entity, "notexistingforcedefault", Claims,
            ImmutableDictionary<string, object>.Empty);
        
        Assert.That(queryResult, Is.Not.Null);
        Assert.That(queryResult.Count(), Is.EqualTo(1));
        
        var queryEntry = queryResult.First();
        
        Assert.That(queryEntry, Is.Not.Null);
        Assert.That(queryEntry.Coltextline, Is.EqualTo("test textline"));
        Assert.That(queryEntry.Colnumber, Is.EqualTo(3));
        
        byIdEntry.Coltextline = "test textline updated";
        byIdEntry.Colnumber = (object)7;
        
        await genericProvider.SaveAsync(Tenant, entity, UserId, "notexistingforcedefault", Claims, byIdEntry);

        byIdEntry = await genericProvider.ByIdAsync<dynamic>(Tenant, entity, "primary", Claims, newEntry.Id);
        
        Assert.That(byIdEntry, Is.Not.Null);
        Assert.That(byIdEntry.Coltextline, Is.EqualTo("test textline updated"));
        Assert.That(byIdEntry.Colnumber, Is.EqualTo(7));
        
        var countResult = await genericProvider.CountAsync(Tenant, entity, "count", Claims, ImmutableDictionary<string, object>.Empty);
        
        Assert.That(countResult, Is.EqualTo(1));
        
        var secondEntry = await genericProvider.NewAsync<dynamic>(Tenant, entity, "primary", Claims);

        Assert.That(secondEntry, Is.Not.Null);
        
        secondEntry.Coltextline = "test textline second entry";

        await genericProvider.SaveAsync(Tenant, entity, UserId, "primary", Claims, secondEntry);
        
        countResult = await genericProvider.CountAsync(Tenant, entity, "count", Claims, ImmutableDictionary<string, object>.Empty);
        
        Assert.That(countResult, Is.EqualTo(2)); 
        
        await genericProvider.RemoveAsync(Tenant, entity, UserId, Claims, queryEntry.Id);
        
        countResult = await genericProvider.CountAsync(Tenant, entity, "count", Claims, ImmutableDictionary<string, object>.Empty);
        
        Assert.That(countResult, Is.EqualTo(1));        
    }
}