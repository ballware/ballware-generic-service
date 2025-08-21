using System.Collections.Immutable;
using System.Text.Json;
using Ballware.Generic.Data.Public;
using Ballware.Generic.Data.Repository;
using Ballware.Generic.Tenant.Data.Commons.Utils;
using Dapper;
using Npgsql;

namespace Ballware.Generic.Tenant.Data.Postgres.Internal;

class PostgresSchemaProvider : ITenantSchemaProvider
{
    private string DefaultSchema { get; } = "public";
    
    private PostgresTenantConfiguration Configuration { get; }
    private ITenantConnectionRepository Repository { get; }
    private ITenantStorageProvider StorageProvider { get; }
    
    public PostgresSchemaProvider(PostgresTenantConfiguration configuration, ITenantConnectionRepository tenantConnectionRepository, ITenantStorageProvider tenantStorageProvider)
    {
        Configuration = configuration;  
        Repository = tenantConnectionRepository;
        StorageProvider = tenantStorageProvider;
    }
    
    public async Task CreateOrUpdateEntityAsync(Guid tenant, string serializedEntityModel, Guid? userId)
    {
        var connection = await Repository.ByIdAsync(tenant);

        if (connection != null)
        {
            using var tenantDb = await StorageProvider.OpenConnectionAsync(tenant);
            
            var tableModel = JsonSerializer.Deserialize<PostgresTableModel>(serializedEntityModel, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }) ?? PostgresTableModel.Empty;
            
            tenantDb.CreateOrUpdateTable(connection.Schema ?? DefaultSchema, tableModel);
        }
    }

    public async Task DropEntityAsync(Guid tenant, string identifier, Guid? userId)
    {
        var connection = await Repository.ByIdAsync(tenant);

        if (connection != null)
        {
            using var tenantDb = await StorageProvider.OpenConnectionAsync(tenant);
            
            tenantDb.DropTable(connection.Schema ?? DefaultSchema, identifier);
        }
    }

    public async Task CreateOrUpdateTenantAsync(Guid tenant, string provider, string serializedTenantModel, Guid? userId)
    {   
        var nextTenantModel = JsonSerializer.Deserialize<PostgresTenantModel>(serializedTenantModel, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }) ?? PostgresTenantModel.Empty;
        
        var tenantConnection = await Repository.ByIdAsync(tenant);
        
        if (tenantConnection is null)
        {
            tenantConnection = await CreateTenantAsync(tenant, nextTenantModel, userId);
        }
        
        var previousTenantModel = JsonSerializer.Deserialize<PostgresTenantModel>(tenantConnection.Model ?? "{}", new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }) ?? PostgresTenantModel.Empty; 
        
        await using var tenantDb = new NpgsqlConnection(tenantConnection.ConnectionString);

        var droppedTypes = GetDroppedItems(previousTenantModel.DatabaseObjects ?? [], nextTenantModel.DatabaseObjects ?? [], PostgresDatabaseObjectTypes.Type);
        var changedTypes = GetAddedOrChangedItems(previousTenantModel.DatabaseObjects ?? [], nextTenantModel.DatabaseObjects ?? [],
            PostgresDatabaseObjectTypes.Type);
        
        var droppedFunctions = GetDroppedItems(previousTenantModel.DatabaseObjects ?? [], nextTenantModel.DatabaseObjects ?? [], PostgresDatabaseObjectTypes.Function);
        var changedFunctions = GetAddedOrChangedItems(previousTenantModel.DatabaseObjects ?? [], nextTenantModel.DatabaseObjects ?? [],
            PostgresDatabaseObjectTypes.Function);
        
        var droppedTables = GetDroppedItems(previousTenantModel.DatabaseObjects ?? [], nextTenantModel.DatabaseObjects ?? [], PostgresDatabaseObjectTypes.Table);
        var changedTables = GetAddedOrChangedItems(previousTenantModel.DatabaseObjects ?? [], nextTenantModel.DatabaseObjects ?? [],
            PostgresDatabaseObjectTypes.Table);
        
        var droppedViews = GetDroppedItems(previousTenantModel.DatabaseObjects ?? [], nextTenantModel.DatabaseObjects ?? [], PostgresDatabaseObjectTypes.View);
        var changedViews = GetAddedOrChangedItems(previousTenantModel.DatabaseObjects ?? [], nextTenantModel.DatabaseObjects ?? [],
            PostgresDatabaseObjectTypes.View);
        
        foreach (var dropped in droppedViews)
        {
            tenantDb.DropView(tenantConnection.Schema ?? DefaultSchema, dropped);
        }
        
        foreach (var dropped in droppedTables)
        {
            tenantDb.DropTable(tenantConnection.Schema ?? DefaultSchema, dropped);
        }
        
        foreach (var dropped in droppedFunctions)
        {
            tenantDb.DropFunction(tenantConnection.Schema ?? DefaultSchema, dropped);
        }
        
        foreach (var dropped in droppedTypes)
        {
            tenantDb.DropType(tenantConnection.Schema ?? DefaultSchema, dropped);
        }
        
        foreach (var changed in changedTypes)
        {
            tenantDb.CreateType(tenantConnection.Schema ?? DefaultSchema, changed.Name, changed.Sql);
        }
        
        foreach (var changed in changedFunctions)
        {
            tenantDb.CreateFunction(tenantConnection.Schema ?? DefaultSchema, changed.Name, changed.Sql);
        }
        
        foreach (var changed in changedTables)
        {
            tenantDb.CreateTable(tenantConnection.Schema ?? DefaultSchema, changed.Name, changed.Sql);
        }
        
        foreach (var changed in changedViews)
        {
            tenantDb.CreateView(tenantConnection.Schema ?? DefaultSchema, changed.Name, changed.Sql);
        }
        
        var changedStatements = nextTenantModel.DatabaseObjects?
            .Where(obj => obj.Type == PostgresDatabaseObjectTypes.Statement && obj.Execute);
        
        foreach (var changed in changedStatements ?? [])
        {
            await tenantDb.ExecuteAsync(changed.Sql);
        }
        
        tenantConnection.Model = JsonSerializer.Serialize(nextTenantModel, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        
        await Repository.SaveAsync(userId, "primary", ImmutableDictionary<string, object>.Empty, tenantConnection);
    }

    private static IEnumerable<string> GetDroppedItems(IEnumerable<PostgresDatabaseObjectModel> previous,
        IEnumerable<PostgresDatabaseObjectModel> next, PostgresDatabaseObjectTypes type)
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
    
    private static IEnumerable<PostgresDatabaseObjectModel> GetAddedOrChangedItems(IEnumerable<PostgresDatabaseObjectModel> previous,
        IEnumerable<PostgresDatabaseObjectModel> next, PostgresDatabaseObjectTypes type)
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
        var tenantConnection = await Repository.ByIdAsync(tenant);

        if (tenantConnection != null)
        {
            var tenantAccessConnectionStringBuilder = new NpgsqlConnectionStringBuilder(tenantConnection.ConnectionString);
            
            var masterConnectionStringBuilder =
                new NpgsqlConnectionStringBuilder(Configuration.TenantMasterConnectionString);
            var tenantCreationConnectionStringBuilder =
                new NpgsqlConnectionStringBuilder(tenantConnection.ConnectionString)
                {
                    Username = masterConnectionStringBuilder.Username,
                    Password = masterConnectionStringBuilder.Password,
                };
            
            await using var tenantDb = new NpgsqlConnection(tenantCreationConnectionStringBuilder.ToString());

            await tenantDb.DropSchemaForUserAsync(tenantCreationConnectionStringBuilder.Database, tenantConnection.Schema ?? DefaultSchema, tenantAccessConnectionStringBuilder.Username);

            await Repository.RemoveAsync(userId, ImmutableDictionary<string, object>.Empty,
                new Dictionary<string, object>([
                    new KeyValuePair<string, object>("Id", tenant)
                ]));
        }
    }

    private async Task<TenantConnection> CreateTenantAsync(Guid tenant, PostgresTenantModel tenantModel, Guid? userId)
    {
        var tenantConnection = await Repository.NewAsync("primary", ImmutableDictionary<string, object>.Empty);

        tenantConnection.Id = tenant;
        
        var masterConnectionStringBuilder =
            new NpgsqlConnectionStringBuilder(Configuration.TenantMasterConnectionString);
        
        var user = $"tenant_{tenant.ToString("N").ToLower()}";
        var password = CommonPasswordGenerator.GenerateTenantPassword();
        
        tenantModel.Server ??= masterConnectionStringBuilder.Host;
        tenantModel.Catalog ??= masterConnectionStringBuilder.Database;
        tenantModel.Schema ??= DefaultSchema;
        
        var tenantCreationConnectionStringBuilder =
            new NpgsqlConnectionStringBuilder(Configuration.TenantMasterConnectionString)
            {
                Host = tenantModel.Server,
                Database = tenantModel.Catalog,
            };
        
        var tenantConnectionString = tenantCreationConnectionStringBuilder.ToString();

        await using var tenantDb = new NpgsqlConnection(tenantConnectionString);

        await tenantDb.CreateSchemaForUserAsync(tenantModel.Catalog, tenantModel.Schema, user, password);

        tenantCreationConnectionStringBuilder.Username = user;
        tenantCreationConnectionStringBuilder.Password = password;
        
        tenantConnection.Provider = "postgres";
        tenantConnection.Schema = tenantModel.Schema;
        tenantConnection.ConnectionString = tenantCreationConnectionStringBuilder.ToString();
        
        await Repository.SaveAsync(userId, "primary", ImmutableDictionary<string, object>.Empty, tenantConnection);
        
        return tenantConnection;
    }
}