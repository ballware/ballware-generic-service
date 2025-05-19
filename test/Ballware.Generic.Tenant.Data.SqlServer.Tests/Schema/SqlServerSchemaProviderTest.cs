using System.Text.Json;
using Ballware.Generic.Data.Public;
using Ballware.Generic.Data.Repository;
using Ballware.Generic.Tenant.Data.SqlServer.Internal;
using Ballware.Generic.Tenant.Data.SqlServer.Tests.Utils;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Ballware.Generic.Tenant.Data.SqlServer.Tests.Schema;

[TestFixture]
public class SqlServerSchemaProviderTest : DatabaseBackedBaseTest
{
    private SqlServerTenantConfiguration Configuration { get; set; } = null!;
    private Mock<ITenantConnectionRepository> ConnectionRepositoryMock { get; set; } = null!;
    private ITenantConnectionRepository ConnectionRepository => ConnectionRepositoryMock.Object;
    private ITenantStorageProvider TenantStorageProvider { get; set; } = null!;
    
    [SetUp]
    public void Setup()
    {
        SqlMapper.AddTypeHandler(new SqlServerColumnTypeHandler());
        
        Configuration = new SqlServerTenantConfiguration()
        {
            TenantMasterConnectionString = MasterConnectionString,
            UseContainedDatabase = false
        };

        ConnectionRepositoryMock = new Mock<ITenantConnectionRepository>();
    }
    
    [Test]
    public async Task Create_tenant_without_objects_succeeds()
    {
        using var listener = new SqlClientListener();
        
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        TenantConnection createdConnection = null;
        
        ConnectionRepositoryMock.Setup(m => m.NewAsync("primary", It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync((string _, IDictionary<string, object> _) => new TenantConnection()
            {
                Id = Guid.NewGuid()
            });
        ConnectionRepositoryMock.Setup(m => m.ByIdAsync(tenantId))
            .ReturnsAsync((Guid _) => null);
        ConnectionRepositoryMock.Setup(m =>
                m.SaveAsync(userId, "primary", It.IsAny<IDictionary<string, object>>(), It.IsAny<TenantConnection>()))
            .Callback((Guid? _, string _, IDictionary<string, object> _, TenantConnection connection) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(connection, Is.Not.Null);
                    Assert.That(connection.Id, Is.EqualTo(tenantId));
                    Assert.That(connection.Schema, Is.EqualTo("faketenant1"));
                    
                    createdConnection = connection;
                });    
            });
        
        var tenantModel = new SqlServerTenantModel()
        {
            Schema = "faketenant1",
            DatabaseObjects = []
        };
        
        var serializedTenantModel = JsonSerializer.Serialize(tenantModel);

        {
            await using var tenantDb = new SqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", "faketenant1", $"tenant_{tenantId.ToString().ToLower()}");
            await tenantDb.CloseAsync();
        }
        
        try
        {
            var provider = new SqlServerSchemaProvider(Configuration, ConnectionRepositoryMock.Object, new SqlServerStorageProvider(ConnectionRepositoryMock.Object));
            
            await provider.CreateOrUpdateTenantAsync(tenantId, "mssql", serializedTenantModel, userId);
            
            ConnectionRepositoryMock.Setup(m => m.ByIdAsync(tenantId))
                .ReturnsAsync((Guid _) => createdConnection);
            
            provider = new SqlServerSchemaProvider(Configuration, ConnectionRepositoryMock.Object, new SqlServerStorageProvider(ConnectionRepositoryMock.Object));
            
            await provider.DropTenantAsync(tenantId, userId);
        }
        finally
        {
            await using var tenantDb = new SqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", "faketenant1", $"tenant_{tenantId.ToString().ToLower()}");
            await tenantDb.CloseAsync();
        }
    }
    
    [Test]
    public async Task Create_tenant_with_objects_succeeds()
    {
        using var listener = new SqlClientListener();
        
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        TenantConnection createdConnection = null; 
        
        ConnectionRepositoryMock.Setup(m => m.NewAsync("primary", It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync((string _, IDictionary<string, object> _) => new TenantConnection()
            {
                Id = Guid.NewGuid()
            });
        ConnectionRepositoryMock.Setup(m => m.ByIdAsync(tenantId))
            .ReturnsAsync((Guid _) => null);
        ConnectionRepositoryMock.Setup(m =>
                m.SaveAsync(userId, "primary", It.IsAny<IDictionary<string, object>>(), It.IsAny<TenantConnection>()))
            .Callback((Guid? _, string _, IDictionary<string, object> _, TenantConnection connection) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(connection, Is.Not.Null);
                    Assert.That(connection.Id, Is.EqualTo(tenantId));
                    Assert.That(connection.Schema, Is.EqualTo("faketenant2"));
                    
                    createdConnection = connection;
                });    
            });
        
        var tenantModel = new SqlServerTenantModel()
        {
            Schema = "faketenant2",
            DatabaseObjects = [
                new SqlServerDatabaseObjectModel()
                {
                    Type = SqlServerDatabaseObjectTypes.Table,
                    Name = "faketable",
                    Sql = "create table [faketable] (Id bigint identity primary key, Fakevalue nvarchar(50))",
                    ExecuteOnSave = true
                },
                new SqlServerDatabaseObjectModel()
                {
                    Type = SqlServerDatabaseObjectTypes.Type,
                    Name = "faketype",
                    Sql = "create type faketenant2.customtype from nvarchar(20) not null",
                    ExecuteOnSave = true
                },
                new SqlServerDatabaseObjectModel()
                {
                    Type = SqlServerDatabaseObjectTypes.Function,
                    Name = "udf_split",
                    Sql = "create function udf_split(@concatenated nvarchar(max)) returns @table table (Value nvarchar(max)) as begin insert into @table select Value from string_split(@concatenated, '|') return end",
                    ExecuteOnSave = true
                },
                new SqlServerDatabaseObjectModel()
                {
                    Type = SqlServerDatabaseObjectTypes.Function,
                    Name = "udf_scalar",
                    Sql = "create function udf_scalar() returns float as begin return 3.154 end",
                    ExecuteOnSave = true
                },
                new SqlServerDatabaseObjectModel()
                {
                    Type = SqlServerDatabaseObjectTypes.View,
                    Name = "view_fake",
                    Sql = "create view view_fake as select Id, Fakevalue, Scalar = faketenant2.udf_scalar() from faketable",
                    ExecuteOnSave = true
                }
            ]
        };
        
        var serializedTenantModel = JsonSerializer.Serialize(tenantModel);

        {
            await using var tenantDb = new SqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", "faketenant2", $"tenant_{tenantId.ToString().ToLower()}");
            await tenantDb.CloseAsync();
        }
        
        try
        {
            var provider = new SqlServerSchemaProvider(Configuration, ConnectionRepositoryMock.Object, new SqlServerStorageProvider(ConnectionRepositoryMock.Object));

            await provider.CreateOrUpdateTenantAsync(tenantId, "mssql", serializedTenantModel, userId);
            
            ConnectionRepositoryMock.Setup(m => m.ByIdAsync(tenantId))
                .ReturnsAsync((Guid _) => createdConnection);
            
            provider = new SqlServerSchemaProvider(Configuration, ConnectionRepositoryMock.Object, new SqlServerStorageProvider(ConnectionRepositoryMock.Object));

            await provider.DropTenantAsync(tenantId, userId);
        }
        finally
        {
            await using var tenantDb = new SqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", "faketenant2", $"tenant_{tenantId.ToString().ToLower()}");
            await tenantDb.CloseAsync();
        }
    }
    
    [Test]
    public async Task Update_tenant_with_objects_succeeds()
    {
        using var listener = new SqlClientListener();
        
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        TenantConnection createdConnection = null; 
        
        ConnectionRepositoryMock.Setup(m => m.NewAsync("primary", It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync((string _, IDictionary<string, object> _) => new TenantConnection()
            {
                Id = Guid.NewGuid()
            });
        ConnectionRepositoryMock.Setup(m => m.ByIdAsync(tenantId))
            .ReturnsAsync((Guid _) => null);
        ConnectionRepositoryMock.Setup(m =>
                m.SaveAsync(userId, "primary", It.IsAny<IDictionary<string, object>>(), It.IsAny<TenantConnection>()))
            .Callback((Guid? _, string _, IDictionary<string, object> _, TenantConnection connection) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(connection, Is.Not.Null);
                    Assert.That(connection.Id, Is.EqualTo(tenantId));
                    Assert.That(connection.Schema, Is.EqualTo("faketenant3"));
                    
                    createdConnection = connection;
                });    
            });
        
        var tenantModel = new SqlServerTenantModel()
        {
            Schema = "faketenant3",
            DatabaseObjects = [
                new SqlServerDatabaseObjectModel()
                {
                    Type = SqlServerDatabaseObjectTypes.Table,
                    Name = "faketable",
                    Sql = "create table [faketable] (Id bigint identity primary key, Fakevalue nvarchar(50))",
                    ExecuteOnSave = true
                },
                new SqlServerDatabaseObjectModel()
                {
                    Type = SqlServerDatabaseObjectTypes.Type,
                    Name = "faketype",
                    Sql = "create type faketenant3.customtype from nvarchar(20) not null",
                    ExecuteOnSave = true
                },
                new SqlServerDatabaseObjectModel()
                {
                    Type = SqlServerDatabaseObjectTypes.Function,
                    Name = "udf_split",
                    Sql = "create function udf_split(@concatenated nvarchar(max)) returns @table table (Value nvarchar(max)) as begin insert into @table select Value from string_split(@concatenated, '|') return end",
                    ExecuteOnSave = true
                },
                new SqlServerDatabaseObjectModel()
                {
                    Type = SqlServerDatabaseObjectTypes.Function,
                    Name = "udf_scalar",
                    Sql = "create function udf_scalar() returns float as begin return 3.154 end",
                    ExecuteOnSave = true
                },
                new SqlServerDatabaseObjectModel()
                {
                    Type = SqlServerDatabaseObjectTypes.View,
                    Name = "view_fake",
                    Sql = "create view view_fake as select Id, Fakevalue, Scalar = faketenant3.udf_scalar() from Faketable",
                    ExecuteOnSave = true
                }
            ]
        };
        
        {
            await using var tenantDb = new SqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", "faketenant3", $"tenant_{tenantId.ToString().ToLower()}");
            await tenantDb.CloseAsync();
        }
        
        try
        {
            var provider = new SqlServerSchemaProvider(Configuration, ConnectionRepositoryMock.Object, new SqlServerStorageProvider(ConnectionRepositoryMock.Object));
            var serializedTenantModel = JsonSerializer.Serialize(tenantModel);
            
            await provider.CreateOrUpdateTenantAsync(tenantId, "mssql", serializedTenantModel, userId);
            
            ConnectionRepositoryMock.Setup(m => m.ByIdAsync(tenantId))
                .ReturnsAsync((Guid _) => createdConnection);
            
            ConnectionRepositoryMock.Setup(m => m.SaveAsync(It.IsAny<Guid?>(), "primary", It.IsAny<IDictionary<string, object>>(), It.IsAny<TenantConnection>()))
                .Callback((Guid? _, string _, IDictionary<string, object> _, TenantConnection tenantConnection) => createdConnection = tenantConnection);
            
            provider = new SqlServerSchemaProvider(Configuration, ConnectionRepositoryMock.Object, new SqlServerStorageProvider(ConnectionRepositoryMock.Object));

            foreach (var sqlServerDatabaseObjectModel in tenantModel.DatabaseObjects)
            {
                sqlServerDatabaseObjectModel.ExecuteOnSave = false;
            }
            
            var udfScalar = tenantModel.DatabaseObjects
                .First(obj => obj.Type == SqlServerDatabaseObjectTypes.Function && obj.Name == "udf_scalar");

            udfScalar.Sql = "create function udf_scalar() returns float as begin return 0.815 end";
            udfScalar.ExecuteOnSave = true;

            serializedTenantModel = JsonSerializer.Serialize(tenantModel);
            
            await provider.CreateOrUpdateTenantAsync(tenantId, "mssql", serializedTenantModel, userId);
            
            tenantModel.DatabaseObjects = [];
            
            serializedTenantModel = JsonSerializer.Serialize(tenantModel);
            
            await provider.CreateOrUpdateTenantAsync(tenantId, "mssql", serializedTenantModel, userId);
            
            await provider.DropTenantAsync(tenantId, userId);
        }
        finally
        {
            await using var tenantDb = new SqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", "faketenant3", $"tenant_{tenantId.ToString().ToLower()}");
            await tenantDb.CloseAsync();
        }
    }
    
    [Test]
    public async Task Create_entity_with_identity_succeeds()
    {
        using var listener = new SqlClientListener();
        
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        TenantConnection createdConnection = null;
        
        ConnectionRepositoryMock.Setup(m => m.NewAsync("primary", It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync((string _, IDictionary<string, object> _) => new TenantConnection()
            {
                Id = Guid.NewGuid()
            });
        ConnectionRepositoryMock.Setup(m => m.ByIdAsync(tenantId))
            .ReturnsAsync((Guid _) => null);
        ConnectionRepositoryMock.Setup(m =>
                m.SaveAsync(userId, "primary", It.IsAny<IDictionary<string, object>>(), It.IsAny<TenantConnection>()))
            .Callback((Guid? _, string _, IDictionary<string, object> _, TenantConnection connection) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(connection, Is.Not.Null);
                    Assert.That(connection.Id, Is.EqualTo(tenantId));
                    Assert.That(connection.Schema, Is.EqualTo("faketenant4"));
                    
                    createdConnection = connection;
                });    
            });
        
        var tenantModel = new SqlServerTenantModel()
        {
            Schema = "faketenant4",
            DatabaseObjects = []
        };
        
        var serializedTenantModel = JsonSerializer.Serialize(tenantModel);

        {
            await using var tenantDb = new SqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", "faketenant4", $"tenant_{tenantId.ToString().ToLower()}");
            await tenantDb.CloseAsync();
        }
        
        try
        {
            var provider = new SqlServerSchemaProvider(Configuration, ConnectionRepositoryMock.Object, new SqlServerStorageProvider(ConnectionRepositoryMock.Object));
            
            await provider.CreateOrUpdateTenantAsync(tenantId, "mssql", serializedTenantModel, userId);
            
            ConnectionRepositoryMock.Setup(m => m.ByIdAsync(tenantId))
                .ReturnsAsync((Guid _) => createdConnection);
            
            provider = new SqlServerSchemaProvider(Configuration, ConnectionRepositoryMock.Object, new SqlServerStorageProvider(ConnectionRepositoryMock.Object));

            var entityModel = new SqlServerTableModel()
            {
                TableName = "fakeentity",
                NoIdentity = false,
                CustomColumns = [],
                CustomIndexes = []
            };
            
            var serializedEntityModel = JsonSerializer.Serialize(entityModel);
            
            await provider.CreateOrUpdateEntityAsync(tenantId, serializedEntityModel, userId);
            await provider.DropEntityAsync(tenantId, "fakeentity", userId);
            
            await provider.DropTenantAsync(tenantId, userId);
        }
        finally
        {
            await using var tenantDb = new SqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", "faketenant4", $"tenant_{tenantId.ToString().ToLower()}");
            await tenantDb.CloseAsync();
        }
    }
}