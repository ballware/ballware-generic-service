using System.Collections.Immutable;
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

namespace Ballware.Generic.Tenant.Data.SqlServer.Tests.Lookup;

class LookupEntry
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public Guid AdditionalParam { get; set; }
}

public class SqlServerLookupProviderTest : DatabaseBackedBaseTest
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
        
        ScriptingExecutorMock = new Mock<IGenericEntityScriptingExecutor>();
        EntityRepositoryMock = new Mock<ITenantEntityRepository>();
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
                new SqlServerColumnModel() { ColumnName = "AdditionalParam", ColumnType = SqlServerColumnType.Uuid, Nullable = true }
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
    [TenantConnection("lookuptenant1")]
    public async Task Lookup_succeeds()
    {
        // Arrange
        using var listener = new SqlClientListener();
        
        PreparedBuilder.Services.AddSingleton(ScriptingExecutorMock.Object);
        
        var app = PreparedBuilder.Build();
        
        var genericProvider = new SqlServerGenericProvider(new SqlServerStorageProvider(ConnectionRepositoryMock.Object), app.Services);

        var expectedList = new List<LookupEntry>
        {
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 1"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 2"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 3"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 4"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 5"
            }
        };
        
        foreach (var entry in expectedList)
        {
            var newEntry = await genericProvider.NewAsync<dynamic>(Tenant, Entity, "primary", UserId, Claims);

            newEntry.Id = entry.Id;
            newEntry.Name = entry.Name;

            await genericProvider.SaveAsync(Tenant, Entity, "primary", UserId, Claims, newEntry);
        }
        
        var lookupProvider = new SqlServerLookupProvider(new SqlServerStorageProvider(ConnectionRepositoryMock.Object));

        var lookup = new Metadata.Lookup()
        {
            Identifier = "testentityLookup",
            ListQuery = "select Uuid as Id, Name from testentity where TenantId=@tenantId order by Name",
            ByIdQuery = "select Uuid as Id, Name from testentity where TenantId=@tenantId and Uuid=@id"
        };
        
        // Act
        var actualList = (await lookupProvider.SelectListForLookupAsync<LookupEntry>(Tenant, lookup, Claims)).ToList();
        var deprecatedActualList = (await lookupProvider.SelectListForLookupAsync<LookupEntry>(Tenant, lookup, ImmutableArray<string>.Empty)).ToList();
        
        var actualEntry = await lookupProvider.SelectByIdForLookupAsync<LookupEntry>(Tenant, lookup, Claims, expectedList[0].Id);
        var deprecatedActualEntry = await lookupProvider.SelectByIdForLookupAsync<LookupEntry>(Tenant, lookup, ImmutableArray<string>.Empty, expectedList[0].Id);
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(actualList.Count, Is.EqualTo(expectedList.Count));
            Assert.That(deprecatedActualList.Count, Is.EqualTo(expectedList.Count));
            
            foreach (var (e, a) in expectedList.Zip(actualList))
            {
                Assert.That(a.Id, Is.EqualTo(e.Id));
                Assert.That(a.Name, Is.EqualTo(e.Name)); 
            }
            
            foreach (var (e, a) in expectedList.Zip(deprecatedActualList))
            {
                Assert.That(a.Id, Is.EqualTo(e.Id));
                Assert.That(a.Name, Is.EqualTo(e.Name)); 
            }
            
            Assert.That(actualEntry, Is.Not.Null);
            Assert.That(actualEntry.Id, Is.EqualTo(expectedList[0].Id));
            Assert.That(actualEntry.Name, Is.EqualTo(expectedList[0].Name));
            
            Assert.That(deprecatedActualEntry, Is.Not.Null);
            Assert.That(deprecatedActualEntry.Id, Is.EqualTo(expectedList[0].Id));
            Assert.That(deprecatedActualEntry.Name, Is.EqualTo(expectedList[0].Name));
        });
    }
    
    [Test]
    [TenantConnection("lookuptenant2")]
    public async Task Lookup_no_entry()
    {
        // Arrange
        using var listener = new SqlClientListener();
        
        PreparedBuilder.Services.AddSingleton(ScriptingExecutorMock.Object);
        
        var app = PreparedBuilder.Build();
        
        var genericProvider = new SqlServerGenericProvider(new SqlServerStorageProvider(ConnectionRepositoryMock.Object), app.Services);

        var expectedList = new List<LookupEntry>
        {
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 1"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 2"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 3"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 4"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 5"
            }
        };
        
        foreach (var entry in expectedList)
        {
            var newEntry = await genericProvider.NewAsync<dynamic>(Tenant, Entity, "primary", UserId, Claims);

            newEntry.Id = entry.Id;
            newEntry.Name = entry.Name;

            await genericProvider.SaveAsync(Tenant, Entity, "primary", UserId, Claims, newEntry);
        }
        
        var lookupProvider = new SqlServerLookupProvider(new SqlServerStorageProvider(ConnectionRepositoryMock.Object));

        var lookup = new Metadata.Lookup()
        {
            Identifier = "testentityLookup",
            ListQuery = "select Uuid as Id, Name from testentity where TenantId=@tenantId order by Name",
            ByIdQuery = "select Uuid as Id, Name from testentity where TenantId=@tenantId and Uuid=@id"
        };
        
        // Act
        var notFoundEntry = await lookupProvider.SelectByIdForLookupAsync<LookupEntry>(Tenant, lookup, Claims, Guid.NewGuid());
        var deprecatedNotFoundEntry = await lookupProvider.SelectByIdForLookupAsync<LookupEntry>(Tenant, lookup, ImmutableArray<string>.Empty, Guid.NewGuid());
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(notFoundEntry, Is.Null);
            Assert.That(deprecatedNotFoundEntry, Is.Null);

            lookup.ByIdQuery = null;
            
            Assert.ThrowsAsync<ArgumentException>(async () => await lookupProvider.SelectByIdForLookupAsync<LookupEntry>(Tenant, lookup, Claims, expectedList[0].Id));
            Assert.ThrowsAsync<ArgumentException>(async () => await lookupProvider.SelectByIdForLookupAsync<LookupEntry>(Tenant, lookup, ImmutableArray<string>.Empty, expectedList[0].Id));
        });
    }
    
    [Test]
    [TenantConnection("lookuptenant3")]
    public async Task LookupWithParam_succeeds()
    {
        // Arrange
        using var listener = new SqlClientListener();
        
        PreparedBuilder.Services.AddSingleton(ScriptingExecutorMock.Object);
        
        var app = PreparedBuilder.Build();
        
        var genericProvider = new SqlServerGenericProvider(new SqlServerStorageProvider(ConnectionRepositoryMock.Object), app.Services);

        var additionalParam = Guid.NewGuid();
        
        var expectedList = new List<LookupEntry>
        {
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 1",
                AdditionalParam = additionalParam
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 2"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 3",
                AdditionalParam = additionalParam
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 4"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 5",
                AdditionalParam = additionalParam
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
        
        var lookupProvider = new SqlServerLookupProvider(new SqlServerStorageProvider(ConnectionRepositoryMock.Object));

        var lookup = new Metadata.Lookup()
        {
            Identifier = "testentityWithParamLookup",
            HasParam = true,
            ListQuery = "select Uuid as Id, Name from testentity where TenantId=@tenantId and AdditionalParam=@param order by Name",
            ByIdQuery = "select Uuid as Id, Name from testentity where TenantId=@tenantId and AdditionalParam=@param and Uuid=@id"
        };
        
        // Act
        var actualList = (await lookupProvider.SelectListForLookupWithParamAsync<LookupEntry>(Tenant, lookup, Claims, additionalParam.ToString())).ToList();
        var deprecatedActualList = (await lookupProvider.SelectListForLookupWithParamAsync<LookupEntry>(Tenant, lookup, ImmutableArray<string>.Empty, additionalParam.ToString())).ToList();
        
        var actualEntry = await lookupProvider.SelectByIdForLookupWithParamAsync<LookupEntry>(Tenant, lookup, Claims, expectedList[0].Id, additionalParam.ToString());
        var deprecatedActualEntry = await lookupProvider.SelectByIdForLookupWithParamAsync<LookupEntry>(Tenant, lookup, ImmutableArray<string>.Empty, expectedList[0].Id, additionalParam.ToString());
        
        // Assert
        Assert.Multiple(() =>
        {
            expectedList = expectedList.Where(x => x.AdditionalParam == additionalParam).ToList();
            
            Assert.That(actualList.Count, Is.EqualTo(expectedList.Count));
            Assert.That(deprecatedActualList.Count, Is.EqualTo(expectedList.Count));
            
            foreach (var (e, a) in expectedList.Zip(actualList))
            {
                Assert.That(a.Id, Is.EqualTo(e.Id));
                Assert.That(a.Name, Is.EqualTo(e.Name)); 
            }
            
            foreach (var (e, a) in expectedList.Zip(deprecatedActualList))
            {
                Assert.That(a.Id, Is.EqualTo(e.Id));
                Assert.That(a.Name, Is.EqualTo(e.Name)); 
            }
            
            Assert.That(actualEntry, Is.Not.Null);
            Assert.That(actualEntry.Id, Is.EqualTo(expectedList[0].Id));
            Assert.That(actualEntry.Name, Is.EqualTo(expectedList[0].Name));
            
            Assert.That(deprecatedActualEntry, Is.Not.Null);
            Assert.That(deprecatedActualEntry.Id, Is.EqualTo(expectedList[0].Id));
            Assert.That(deprecatedActualEntry.Name, Is.EqualTo(expectedList[0].Name));
        });
    }
    
    [Test]
    [TenantConnection("lookuptenant4")]
    public async Task LookupWithParam_no_entry()
    {
        // Arrange
        using var listener = new SqlClientListener();
        
        PreparedBuilder.Services.AddSingleton(ScriptingExecutorMock.Object);
        
        var app = PreparedBuilder.Build();
        
        var genericProvider = new SqlServerGenericProvider(new SqlServerStorageProvider(ConnectionRepositoryMock.Object), app.Services);

        var additionalParam = Guid.NewGuid();
        
        var expectedList = new List<LookupEntry>
        {
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 1",
                AdditionalParam = additionalParam
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 2"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 3",
                AdditionalParam = additionalParam
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 4"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 5",
                AdditionalParam = additionalParam
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
        
        var lookupProvider = new SqlServerLookupProvider(new SqlServerStorageProvider(ConnectionRepositoryMock.Object));

        var lookup = new Metadata.Lookup()
        {
            Identifier = "testentityWithParamLookup",
            HasParam = true,
            ListQuery = "select Uuid as Id, Name from testentity where TenantId=@tenantId and AdditionalParam=@param order by Name",
            ByIdQuery = "select Uuid as Id, Name from testentity where TenantId=@tenantId and AdditionalParam=@param and Uuid=@id"
        };
        
        // Act
        var notFoundEntry = await lookupProvider.SelectByIdForLookupWithParamAsync<LookupEntry>(Tenant, lookup, Claims, Guid.NewGuid(), additionalParam.ToString());
        var deprecatedNotFoundEntry = await lookupProvider.SelectByIdForLookupWithParamAsync<LookupEntry>(Tenant, lookup, ImmutableArray<string>.Empty, Guid.NewGuid(), additionalParam.ToString());
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(notFoundEntry, Is.Null);
            Assert.That(deprecatedNotFoundEntry, Is.Null);

            lookup.ByIdQuery = null;
            
            Assert.ThrowsAsync<ArgumentException>(async () => await lookupProvider.SelectByIdForLookupWithParamAsync<LookupEntry>(Tenant, lookup, Claims, expectedList[0].Id, additionalParam.ToString()));
            Assert.ThrowsAsync<ArgumentException>(async () => await lookupProvider.SelectByIdForLookupWithParamAsync<LookupEntry>(Tenant, lookup, ImmutableArray<string>.Empty, expectedList[0].Id, additionalParam.ToString()));
        });
    }
    
    [Test]
    [TenantConnection("lookuptenant5")]
    public async Task Autocomplete_succeeds()
    {
        // Arrange
        using var listener = new SqlClientListener();
        
        PreparedBuilder.Services.AddSingleton(ScriptingExecutorMock.Object);
        
        var app = PreparedBuilder.Build();
        
        var genericProvider = new SqlServerGenericProvider(new SqlServerStorageProvider(ConnectionRepositoryMock.Object), app.Services);

        var expectedList = new List<LookupEntry>
        {
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 1"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 2"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 3"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 4"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 5"
            }
        };
        
        foreach (var entry in expectedList)
        {
            var newEntry = await genericProvider.NewAsync<dynamic>(Tenant, Entity, "primary", UserId, Claims);

            newEntry.Id = entry.Id;
            newEntry.Name = entry.Name;

            await genericProvider.SaveAsync(Tenant, Entity, "primary", UserId, Claims, newEntry);
        }
        
        var lookupProvider = new SqlServerLookupProvider(new SqlServerStorageProvider(ConnectionRepositoryMock.Object));

        var lookup = new Metadata.Lookup()
        {
            Identifier = "testentityAutocomplete",
            ListQuery = "select Name from testentity where TenantId=@tenantId order by Name"
        };
        
        // Act
        var actualList = (await lookupProvider.AutocompleteForLookupAsync(Tenant, lookup, Claims)).ToList();
        var deprecatedActualList = (await lookupProvider.AutocompleteForLookupAsync(Tenant, lookup, ImmutableArray<string>.Empty)).ToList();
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(actualList.Count, Is.EqualTo(expectedList.Count));
            Assert.That(deprecatedActualList.Count, Is.EqualTo(expectedList.Count));
            
            foreach (var (e, a) in expectedList.Zip(actualList))
            {
                Assert.That(a, Is.EqualTo(e.Name)); 
            }
            
            foreach (var (e, a) in expectedList.Zip(deprecatedActualList))
            {
                Assert.That(a, Is.EqualTo(e.Name)); 
            }
        });
    }
    
    [Test]
    [TenantConnection("lookuptenant6")]
    public async Task AutocompleteWithParam_succeeds()
    {
        // Arrange
        using var listener = new SqlClientListener();
        
        PreparedBuilder.Services.AddSingleton(ScriptingExecutorMock.Object);
        
        var app = PreparedBuilder.Build();
        
        var genericProvider = new SqlServerGenericProvider(new SqlServerStorageProvider(ConnectionRepositoryMock.Object), app.Services);

        var additionalParam = Guid.NewGuid();
        
        var expectedList = new List<LookupEntry>
        {
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 1",
                AdditionalParam = additionalParam
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 2"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 3",
                AdditionalParam = additionalParam
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 4"
            },
            new ()
            {
                Id = Guid.NewGuid(),
                Name = "Name 5",
                AdditionalParam = additionalParam
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
        
        var lookupProvider = new SqlServerLookupProvider(new SqlServerStorageProvider(ConnectionRepositoryMock.Object));

        var lookup = new Metadata.Lookup()
        {
            Identifier = "testentityWithParamAutocomplete",
            HasParam = true,
            ListQuery = "select Name from testentity where TenantId=@tenantId and AdditionalParam=@param order by Name"
        };
        
        // Act
        var actualList = (await lookupProvider.AutocompleteForLookupWithParamAsync(Tenant, lookup, Claims, additionalParam.ToString())).ToList();
        var deprecatedActualList = (await lookupProvider.AutocompleteForLookupWithParamAsync(Tenant, lookup, ImmutableArray<string>.Empty, additionalParam.ToString())).ToList();
        
        // Assert
        Assert.Multiple(() =>
        {
            expectedList = expectedList.Where(x => x.AdditionalParam == additionalParam).ToList();
            
            Assert.That(actualList.Count, Is.EqualTo(expectedList.Count));
            Assert.That(deprecatedActualList.Count, Is.EqualTo(expectedList.Count));
            
            foreach (var (e, a) in expectedList.Zip(actualList))
            {
                Assert.That(a, Is.EqualTo(e.Name)); 
            }
            
            foreach (var (e, a) in expectedList.Zip(deprecatedActualList))
            {
                Assert.That(a, Is.EqualTo(e.Name)); 
            }
        });
    }
}