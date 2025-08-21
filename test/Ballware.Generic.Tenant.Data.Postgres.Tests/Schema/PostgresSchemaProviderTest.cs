using System.Text.Json;
using Ballware.Generic.Data.Public;
using Ballware.Generic.Data.Repository;
using Ballware.Generic.Tenant.Data.Postgres.Internal;
using Ballware.Generic.Tenant.Data.Postgres.Tests.Utils;
using Dapper;
using Moq;
using Npgsql;

namespace Ballware.Generic.Tenant.Data.Postgres.Tests.Schema;

[TestFixture]
public class PostgresSchemaProviderTest : DatabaseBackedBaseTest
{
    private PostgresTenantConfiguration Configuration { get; set; } = null!;
    private Mock<ITenantConnectionRepository> ConnectionRepositoryMock { get; set; } = null!;
    private Mock<ITenantEntityRepository> EntityRepositoryMock { get; set; } = null!;
    private ITenantConnectionRepository ConnectionRepository => ConnectionRepositoryMock.Object;
    private ITenantStorageProvider TenantStorageProvider { get; set; } = null!;
    
    [SetUp]
    public void Setup()
    {
        SqlMapper.AddTypeHandler(new PostgresColumnTypeHandler());
        
        Configuration = new PostgresTenantConfiguration()
        {
            TenantMasterConnectionString = MasterConnectionString
        };

        ConnectionRepositoryMock = new Mock<ITenantConnectionRepository>();
        EntityRepositoryMock = new Mock<ITenantEntityRepository>();
        
        EntityRepositoryMock.Setup(m => m.NewAsync(It.IsAny<Guid>(), "primary", It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync((Guid _, string _, IDictionary<string, object> _) => new TenantEntity()
            {
                Id = Guid.NewGuid()
            });     
    }
    
    [Test]
    public async Task Create_tenant_without_objects_succeeds()
    {
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
        
        var tenantModel = new PostgresTenantModel()
        {
            Schema = "faketenant1",
            DatabaseObjects = []
        };
        
        var serializedTenantModel = JsonSerializer.Serialize(tenantModel, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        {
            await using var tenantDb = new NpgsqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", "faketenant1", $"tenant_{tenantId.ToString("N").ToLower()}");
            await tenantDb.CloseAsync();
        }
        
        try
        {
            var provider = new PostgresSchemaProvider(Configuration, ConnectionRepositoryMock.Object, EntityRepositoryMock.Object, new PostgresStorageProvider(ConnectionRepositoryMock.Object));
            
            await provider.CreateOrUpdateTenantAsync(tenantId, "postgres", serializedTenantModel, userId);
            
            ConnectionRepositoryMock.Setup(m => m.ByIdAsync(tenantId))
                .ReturnsAsync((Guid _) => createdConnection);
            
            provider = new PostgresSchemaProvider(Configuration, ConnectionRepositoryMock.Object, EntityRepositoryMock.Object, new PostgresStorageProvider(ConnectionRepositoryMock.Object));
            
            await provider.DropTenantAsync(tenantId, userId);
        }
        finally
        {
            await using var tenantDb = new NpgsqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", "faketenant1", $"tenant_{tenantId.ToString("N").ToLower()}");
            await tenantDb.CloseAsync();
        }
    }
    
    [Test]
    public async Task Create_tenant_with_objects_succeeds()
    {   
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
        
        var tenantModel = new PostgresTenantModel()
        {
            Schema = "faketenant2",
            DatabaseObjects = [
                new PostgresDatabaseObjectModel()
                {
                    Type = PostgresDatabaseObjectTypes.Table,
                    Name = "faketable",
                    Sql = "create table faketable (id bigserial primary key, fakevalue varchar(50))",
                    Execute = true
                },
                new PostgresDatabaseObjectModel()
                {
                    Type = PostgresDatabaseObjectTypes.Type,
                    Name = "faketype",
                    Sql = "create domain faketenant2.customtype as varchar(20) not null",
                    Execute = true
                },
                new PostgresDatabaseObjectModel()
                {
                    Type = PostgresDatabaseObjectTypes.Function,
                    Name = "udf_split",
                    Sql = "create function udf_split(concatenated text) returns table (Value text) as $$ BEGIN RETURN QUERY SELECT unnest(string_to_array(concatenated, '|')); END; $$ LANGUAGE plpgsql;",
                    Execute = true
                },
                new PostgresDatabaseObjectModel()
                {
                    Type = PostgresDatabaseObjectTypes.Function,
                    Name = "udf_scalar",
                    Sql = "create function udf_scalar() returns real as $$ begin return 3.154; end; $$ LANGUAGE plpgsql;",
                    Execute = true
                },
                new PostgresDatabaseObjectModel()
                {
                    Type = PostgresDatabaseObjectTypes.View,
                    Name = "view_fake",
                    Sql = "create view view_fake as select id, fakevalue, faketenant2.udf_scalar() as scalar from faketable",
                    Execute = true
                }
            ]
        };
        
        var serializedTenantModel = JsonSerializer.Serialize(tenantModel, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        {
            await using var tenantDb = new NpgsqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", "faketenant2", $"tenant_{tenantId.ToString("N").ToLower()}");
            await tenantDb.CloseAsync();
        }
        
        try
        {
            var provider = new PostgresSchemaProvider(Configuration, ConnectionRepositoryMock.Object, EntityRepositoryMock.Object, new PostgresStorageProvider(ConnectionRepositoryMock.Object));

            await provider.CreateOrUpdateTenantAsync(tenantId, "postgres", serializedTenantModel, userId);
            
            ConnectionRepositoryMock.Setup(m => m.ByIdAsync(tenantId))
                .ReturnsAsync((Guid _) => createdConnection);
            
            provider = new PostgresSchemaProvider(Configuration, ConnectionRepositoryMock.Object, EntityRepositoryMock.Object, new PostgresStorageProvider(ConnectionRepositoryMock.Object));

            await provider.DropTenantAsync(tenantId, userId);
        }
        finally
        {
            await using var tenantDb = new NpgsqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", "faketenant2", $"tenant_{tenantId.ToString("N").ToLower()}");
            await tenantDb.CloseAsync();
        }
    }
    
    [Test]
    public async Task Update_tenant_with_objects_succeeds()
    {
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
        
        var tenantModel = new PostgresTenantModel()
        {
            Schema = "faketenant3",
            DatabaseObjects = [
                new PostgresDatabaseObjectModel()
                {
                    Type = PostgresDatabaseObjectTypes.Table,
                    Name = "faketable",
                    Sql = "create table faketable (id bigserial primary key, fakevalue varchar(50))",
                    Execute = true
                },
                new PostgresDatabaseObjectModel()
                {
                    Type = PostgresDatabaseObjectTypes.Type,
                    Name = "faketype",
                    Sql = "create domain faketenant3.customtype as varchar(20) not null",
                    Execute = true
                },
                new PostgresDatabaseObjectModel()
                {
                    Type = PostgresDatabaseObjectTypes.Function,
                    Name = "udf_split",
                    Sql = "create function udf_split(concatenated text) returns table (Value text) as $$ BEGIN RETURN QUERY SELECT unnest(string_to_array(concatenated, '|')); END; $$ LANGUAGE plpgsql;",
                    Execute = true
                },
                new PostgresDatabaseObjectModel()
                {
                    Type = PostgresDatabaseObjectTypes.Function,
                    Name = "udf_scalar",
                    Sql = "create function udf_scalar() returns real as $$ begin return 3.154; end; $$ LANGUAGE plpgsql;",
                    Execute = true
                },
                new PostgresDatabaseObjectModel()
                {
                    Type = PostgresDatabaseObjectTypes.View,
                    Name = "view_fake",
                    Sql = "create view view_fake as select id, fakevalue, faketenant3.udf_scalar() as scalar from faketable",
                    Execute = true
                }
            ]
        };
        
        {
            await using var tenantDb = new NpgsqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", "faketenant3", $"tenant_{tenantId.ToString("N").ToLower()}");
            await tenantDb.CloseAsync();
        }
        
        try
        {
            var provider = new PostgresSchemaProvider(Configuration, ConnectionRepositoryMock.Object, EntityRepositoryMock.Object, new PostgresStorageProvider(ConnectionRepositoryMock.Object));
            var serializedTenantModel = JsonSerializer.Serialize(tenantModel, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            await provider.CreateOrUpdateTenantAsync(tenantId, "postgres", serializedTenantModel, userId);
            
            ConnectionRepositoryMock.Setup(m => m.ByIdAsync(tenantId))
                .ReturnsAsync((Guid _) => createdConnection);
            
            ConnectionRepositoryMock.Setup(m => m.SaveAsync(It.IsAny<Guid?>(), "primary", It.IsAny<IDictionary<string, object>>(), It.IsAny<TenantConnection>()))
                .Callback((Guid? _, string _, IDictionary<string, object> _, TenantConnection tenantConnection) => createdConnection = tenantConnection);
            
            provider = new PostgresSchemaProvider(Configuration, ConnectionRepositoryMock.Object, EntityRepositoryMock.Object, new PostgresStorageProvider(ConnectionRepositoryMock.Object));

            foreach (var PostgresDatabaseObjectModel in tenantModel.DatabaseObjects)
            {
                PostgresDatabaseObjectModel.Execute = false;
            }
            
            var udfScalar = tenantModel.DatabaseObjects
                .First(obj => obj.Type == PostgresDatabaseObjectTypes.Function && obj.Name == "udf_scalar");

            udfScalar.Sql = "create or replace function udf_scalar() returns real as $$ begin return 0.815; end; $$ LANGUAGE plpgsql;";
            udfScalar.Execute = true;

            serializedTenantModel = JsonSerializer.Serialize(tenantModel, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            await provider.CreateOrUpdateTenantAsync(tenantId, "postgres", serializedTenantModel, userId);
            
            tenantModel.DatabaseObjects = [];
            
            serializedTenantModel = JsonSerializer.Serialize(tenantModel, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            await provider.CreateOrUpdateTenantAsync(tenantId, "postgres", serializedTenantModel, userId);
            
            await provider.DropTenantAsync(tenantId, userId);
        }
        finally
        {
            await using var tenantDb = new NpgsqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", "faketenant3", $"tenant_{tenantId.ToString("N").ToLower()}");
            await tenantDb.CloseAsync();
        }
    }
    
    [Test]
    public async Task Create_entity_with_identity_succeeds()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        TenantConnection createdConnection = null;
        TenantEntity createdEntity = null;
        
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
        
        EntityRepositoryMock.Setup(m => m.NewAsync(tenantId, "primary", It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync((Guid _, string _, IDictionary<string, object> _) => new TenantEntity()
            {
                Id = Guid.NewGuid()
            });  
        
        EntityRepositoryMock.Setup(m =>
                m.SaveAsync(tenantId, userId, "primary", It.IsAny<IDictionary<string, object>>(), It.IsAny<TenantEntity>()))
            .Callback((Guid _, Guid? _, string _, IDictionary<string, object> _, TenantEntity tenantEntity) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(tenantEntity, Is.Not.Null);
                    Assert.That(tenantEntity.Entity, Is.EqualTo("fakeentity"));

                    createdEntity = tenantEntity;
                });    
            });
        
        EntityRepositoryMock.Setup(m => m.ByEntityAsync(tenantId, It.IsAny<string>()))
            .ReturnsAsync((Guid _, string _) => createdEntity);
        
        var tenantModel = new PostgresTenantModel()
        {
            Schema = "faketenant4",
            DatabaseObjects = []
        };
        
        var serializedTenantModel = JsonSerializer.Serialize(tenantModel, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        {
            await using var tenantDb = new NpgsqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", "faketenant4", $"tenant_{tenantId.ToString("N").ToLower()}");
            await tenantDb.CloseAsync();
        }
        
        try
        {
            var provider = new PostgresSchemaProvider(Configuration, ConnectionRepositoryMock.Object, EntityRepositoryMock.Object, new PostgresStorageProvider(ConnectionRepositoryMock.Object));
            
            await provider.CreateOrUpdateTenantAsync(tenantId, "postgres", serializedTenantModel, userId);
            
            ConnectionRepositoryMock.Setup(m => m.ByIdAsync(tenantId))
                .ReturnsAsync((Guid _) => createdConnection);
            
            provider = new PostgresSchemaProvider(Configuration, ConnectionRepositoryMock.Object, EntityRepositoryMock.Object, new PostgresStorageProvider(ConnectionRepositoryMock.Object));

            var entityModel = new PostgresTableModel()
            {
                TableName = "fakeentity",
                NoIdentity = false,
                CustomColumns = [],
                CustomIndexes = []
            };
            
            var serializedEntityModel = JsonSerializer.Serialize(entityModel, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            await provider.CreateOrUpdateEntityAsync(tenantId, serializedEntityModel, userId);
            await provider.DropEntityAsync(tenantId, "fakeentity", userId);
            
            await provider.DropTenantAsync(tenantId, userId);
        }
        finally
        {
            await using var tenantDb = new NpgsqlConnection(Configuration.TenantMasterConnectionString);
            await tenantDb.DropSchemaForUserAsync("tenant", "faketenant4", $"tenant_{tenantId.ToString("N").ToLower()}");
            await tenantDb.CloseAsync();
        }
    }
}