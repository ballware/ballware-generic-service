using System.Collections.Immutable;
using System.Text.Json;
using Ballware.Generic.Data.Public;
using Ballware.Generic.Data.Repository;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

class SqlServerSchemaProvider : ITenantSchemaProvider
{
    private string DefaultSchema { get; } = "dbo";   
    private string DefaultQueryIdentifier { get; } = "primary";
    
    private SqlServerTenantConfiguration Configuration { get; }
    private ITenantConnectionRepository TenantConnectionRepository { get; }
    private ITenantEntityRepository TenantEntityRepository { get; }
    private ITenantStorageProvider StorageProvider { get; }
    
    public SqlServerSchemaProvider(SqlServerTenantConfiguration configuration, ITenantConnectionRepository tenantConnectionRepository, ITenantEntityRepository tenantEntityRepository, ITenantStorageProvider tenantStorageProvider)
    {
        Configuration = configuration;  
        TenantConnectionRepository = tenantConnectionRepository;
        TenantEntityRepository = tenantEntityRepository;
        StorageProvider = tenantStorageProvider;
    }
    
    public async Task CreateOrUpdateEntityAsync(Guid tenant, string serializedEntityModel, Guid? userId)
    {
        var connection = await TenantConnectionRepository.ByIdAsync(tenant);

        if (connection != null)
        {
            using var tenantDb = await StorageProvider.OpenConnectionAsync(tenant);
            
            var tableModel = JsonSerializer.Deserialize<SqlServerTableModel>(serializedEntityModel, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }) ?? SqlServerTableModel.Empty;
            
            var entityModel = await TenantEntityRepository.ByEntityAsync(tenant, tableModel.TableName);

            if (entityModel == null)
            {
                entityModel = await TenantEntityRepository.NewAsync(tenant, DefaultQueryIdentifier, ImmutableDictionary<string, object>.Empty);
                entityModel.Entity = tableModel.TableName;
            }
            
            tenantDb.CreateOrUpdateTable(connection.Schema ?? DefaultSchema, tableModel);
            
            entityModel.Model = serializedEntityModel;
            
            await TenantEntityRepository.SaveAsync(tenant, userId, DefaultQueryIdentifier, ImmutableDictionary<string, object>.Empty, entityModel);
        }
    }

    public async Task DropEntityAsync(Guid tenant, string identifier, Guid? userId)
    {
        var connection = await TenantConnectionRepository.ByIdAsync(tenant);

        if (connection != null)
        {
            var entityModel = await TenantEntityRepository.ByEntityAsync(tenant, identifier);
            
            using var tenantDb = await StorageProvider.OpenConnectionAsync(tenant);
            
            tenantDb.DropTable(connection.Schema ?? DefaultSchema, identifier);
            
            if (entityModel != null)
            {
                await TenantEntityRepository.RemoveAsync(tenant, userId, ImmutableDictionary<string, object>.Empty,
                    new Dictionary<string, object>([
                        new KeyValuePair<string, object>("Id", entityModel.Id)
                    ]));
            }
        }
    }

    public async Task CreateOrUpdateTenantAsync(Guid tenant, string provider, string serializedTenantModel, Guid? userId)
    {   
        var nextTenantModel = JsonSerializer.Deserialize<SqlServerTenantModel>(serializedTenantModel, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }) ?? SqlServerTenantModel.Empty;
        
        var tenantConnection = await TenantConnectionRepository.ByIdAsync(tenant);
        
        if (tenantConnection is null)
        {
            tenantConnection = await CreateTenantAsync(tenant, nextTenantModel, userId);
        }
        
        var previousTenantModel = JsonSerializer.Deserialize<SqlServerTenantModel>(tenantConnection.Model ?? "{}", new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }) ?? SqlServerTenantModel.Empty; 
        
        await using var tenantDb = new SqlConnection(tenantConnection.ConnectionString);

        var droppedTypes = GetDroppedItems(previousTenantModel.DatabaseObjects ?? [], nextTenantModel.DatabaseObjects ?? [], SqlServerDatabaseObjectTypes.Type);
        var changedTypes = GetAddedOrChangedItems(previousTenantModel.DatabaseObjects ?? [], nextTenantModel.DatabaseObjects ?? [],
            SqlServerDatabaseObjectTypes.Type);
        
        foreach (var dropped in droppedTypes)
        {
            tenantDb.DropType(tenantConnection.Schema ?? DefaultSchema, dropped);
        }
        
        foreach (var changed in changedTypes)
        {
            tenantDb.CreateType(tenantConnection.Schema ?? DefaultSchema, changed.Name, changed.Sql);
        }
        
        var droppedFunctions = GetDroppedItems(previousTenantModel.DatabaseObjects ?? [], nextTenantModel.DatabaseObjects ?? [], SqlServerDatabaseObjectTypes.Function);
        var changedFunctions = GetAddedOrChangedItems(previousTenantModel.DatabaseObjects ?? [], nextTenantModel.DatabaseObjects ?? [],
            SqlServerDatabaseObjectTypes.Function);
        
        foreach (var dropped in droppedFunctions)
        {
            tenantDb.DropFunction(tenantConnection.Schema ?? DefaultSchema, dropped);
        }
        
        foreach (var changed in changedFunctions)
        {
            tenantDb.CreateFunction(tenantConnection.Schema ?? DefaultSchema, changed.Name, changed.Sql);
        }
        
        var droppedTables = GetDroppedItems(previousTenantModel.DatabaseObjects ?? [], nextTenantModel.DatabaseObjects ?? [], SqlServerDatabaseObjectTypes.Table);
        var changedTables = GetAddedOrChangedItems(previousTenantModel.DatabaseObjects ?? [], nextTenantModel.DatabaseObjects ?? [],
            SqlServerDatabaseObjectTypes.Table);
        
        foreach (var dropped in droppedTables)
        {
            tenantDb.DropTable(tenantConnection.Schema ?? DefaultSchema, dropped);
        }
        
        foreach (var changed in changedTables)
        {
            tenantDb.CreateTable(tenantConnection.Schema ?? DefaultSchema, changed.Name, changed.Sql);
        }
        
        var droppedViews = GetDroppedItems(previousTenantModel.DatabaseObjects ?? [], nextTenantModel.DatabaseObjects ?? [], SqlServerDatabaseObjectTypes.View);
        var changedViews = GetAddedOrChangedItems(previousTenantModel.DatabaseObjects ?? [], nextTenantModel.DatabaseObjects ?? [],
            SqlServerDatabaseObjectTypes.View);
        
        foreach (var dropped in droppedViews)
        {
            tenantDb.DropView(tenantConnection.Schema ?? DefaultSchema, dropped);
        }
        
        foreach (var changed in changedViews)
        {
            tenantDb.CreateView(tenantConnection.Schema ?? DefaultSchema, changed.Name, changed.Sql);
        }
        
        var changedStatements = nextTenantModel.DatabaseObjects?
            .Where(obj => obj.Type == SqlServerDatabaseObjectTypes.Statement && obj.Execute);
        
        foreach (var changed in changedStatements ?? [])
        {
            await tenantDb.ExecuteAsync(changed.Sql);
        }
        
        tenantConnection.Model = JsonSerializer.Serialize(nextTenantModel, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        
        await TenantConnectionRepository.SaveAsync(userId, DefaultQueryIdentifier, ImmutableDictionary<string, object>.Empty, tenantConnection);
    }

    private static IEnumerable<string> GetDroppedItems(IEnumerable<SqlServerDatabaseObjectModel> previous,
        IEnumerable<SqlServerDatabaseObjectModel> next, SqlServerDatabaseObjectTypes type)
    {
        var previousItems =
            previous
                .Where(obj => obj.Type == type)
                .Select(obj => obj.Name);
        
        var nextItems = 
            next
                .Where(obj => obj.Type == type)
                .Select(obj => obj.Name);
        
        var droppedItems = previousItems.Except(nextItems);
        
        return droppedItems;
    }
    
    private static IEnumerable<SqlServerDatabaseObjectModel> GetAddedOrChangedItems(IEnumerable<SqlServerDatabaseObjectModel> previous,
        IEnumerable<SqlServerDatabaseObjectModel> next, SqlServerDatabaseObjectTypes type)
    {
        var previousItems =
            previous
                .Where(obj => obj.Type == type)
                .ToDictionary(obj => obj.Name, obj => obj);
        
        var nextItems = 
            next
                .Where(obj => obj.Type == type && obj.Execute)
                .ToDictionary(obj => obj.Name, obj => obj);
        
        var changedItems = 
            nextItems
                .Where(n => !previousItems.ContainsKey(n.Key) || !previousItems[n.Key].Sql.Equals(n.Value.Sql))
                .Select(n => n.Value);
        
        return changedItems;
    }

    public async Task DropTenantAsync(Guid tenant, Guid? userId)
    {
        var tenantConnection = await TenantConnectionRepository.ByIdAsync(tenant);

        if (tenantConnection != null)
        {
            var tenantAccessConnectionStringBuilder = new SqlConnectionStringBuilder(tenantConnection.ConnectionString);
            
            var masterConnectionStringBuilder =
                new SqlConnectionStringBuilder(Configuration.TenantMasterConnectionString);
            var tenantCreationConnectionStringBuilder =
                new SqlConnectionStringBuilder(tenantConnection.ConnectionString)
                {
                    UserID = masterConnectionStringBuilder.UserID,
                    Password = masterConnectionStringBuilder.Password,
                };
            
            await using var tenantDb = new SqlConnection(tenantCreationConnectionStringBuilder.ToString());

            await tenantDb.DropSchemaForUserAsync(tenantCreationConnectionStringBuilder.InitialCatalog, tenantConnection.Schema ?? DefaultSchema, tenantAccessConnectionStringBuilder.UserID);

            await TenantConnectionRepository.RemoveAsync(userId, ImmutableDictionary<string, object>.Empty,
                new Dictionary<string, object>([
                    new KeyValuePair<string, object>("Id", tenant)
                ]));
        }
    }

    private async Task<TenantConnection> CreateTenantAsync(Guid tenant, SqlServerTenantModel tenantModel, Guid? userId)
    {
        var tenantConnection = await TenantConnectionRepository.NewAsync(DefaultQueryIdentifier, ImmutableDictionary<string, object>.Empty);

        tenantConnection.Id = tenant;
        
        var masterConnectionStringBuilder =
            new SqlConnectionStringBuilder(Configuration.TenantMasterConnectionString);
        
        var user = $"tenant_{tenant.ToString().ToLower()}";
        var password = Guid.NewGuid().ToString();
        
        tenantModel.Server ??= masterConnectionStringBuilder.DataSource;
        tenantModel.Catalog ??= masterConnectionStringBuilder.InitialCatalog;
        tenantModel.Schema ??= DefaultSchema;
        
        var tenantCreationConnectionStringBuilder =
            new SqlConnectionStringBuilder(Configuration.TenantMasterConnectionString)
            {
                DataSource = tenantModel.Server,
                InitialCatalog = tenantModel.Catalog,
            };
        
        var tenantConnectionString = tenantCreationConnectionStringBuilder.ToString();

        await using var tenantDb = new SqlConnection(tenantConnectionString);

        if (Configuration.UseContainedDatabase)
        {
            await tenantDb.CreateContainedSchemaForUserAsync(tenantModel.Catalog, tenantModel.Schema, user, password);
        }
        else
        {
            await tenantDb.CreateSchemaForUserAsync(tenantModel.Catalog, tenantModel.Schema, user, password);
        }

        tenantCreationConnectionStringBuilder.UserID = user;
        tenantCreationConnectionStringBuilder.Password = password;
        
        tenantConnection.Provider = "mssql";
        tenantConnection.Schema = tenantModel.Schema;
        tenantConnection.ConnectionString = tenantCreationConnectionStringBuilder.ToString();
        
        await TenantConnectionRepository.SaveAsync(userId, DefaultQueryIdentifier, ImmutableDictionary<string, object>.Empty, tenantConnection);
        
        return tenantConnection;
    }
}