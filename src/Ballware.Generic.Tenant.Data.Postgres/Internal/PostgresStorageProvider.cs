using System.Data;
using Ballware.Generic.Data.Repository;
using Npgsql;

namespace Ballware.Generic.Tenant.Data.Postgres.Internal;

class PostgresStorageProvider : ITenantStorageProvider
{
    private ITenantConnectionRepository ConnectionRepository { get; }
    
    public PostgresStorageProvider(ITenantConnectionRepository connectionRepository)
    {
        ConnectionRepository = connectionRepository;
    }

    public async Task<string> GetConnectionStringAsync(Guid tenant)
    {
        var tenantConnection = await ConnectionRepository.ByIdAsync(tenant);

        if (tenantConnection == null || tenantConnection.ConnectionString == null)
        {
            throw new ArgumentException($"Tenant {tenant} does not exist");
        }
        
        return tenantConnection.ConnectionString;
    }

    public async Task<IDbConnection> OpenConnectionAsync(Guid tenant)
    {
        var tenantConnection = await ConnectionRepository.ByIdAsync(tenant);

        if (tenantConnection == null)
        {
            throw new ArgumentException($"Tenant {tenant} does not exist");
        }
        
        var connection = new NpgsqlConnection(tenantConnection.ConnectionString);

        await connection.OpenAsync();

        return connection;
    }

    public async Task<string> ApplyTenantPlaceholderAsync(Guid tenant, string source, TenantPlaceholderOptions options)
    {
        var tenantConnection = await ConnectionRepository.ByIdAsync(tenant);

        if (tenantConnection == null)
        {
            throw new ArgumentException($"Tenant {tenant} does not exist");
        }
        
        if (!string.IsNullOrEmpty(source))
        {
            source = source.Replace("[ballwareschema]", tenantConnection.Schema ?? "dbo");
        }

        if (!string.IsNullOrEmpty(source) && options.ReplaceTenantId)
        {
            source = source.Replace("@tenantId", $"'{tenantConnection.Id}'");
        }

        if (!string.IsNullOrEmpty(source) && options.ReplaceClaims)
        {
            source = source.Replace("@claims", "''");
        }

        return source;
    }

    public async Task<T> TransferToVariablesAsync<T>(Guid tenant, T target, IDictionary<string, object>? source, string prefix = "") where T : IDictionary<string, object>
    {
        return await Task.FromResult(Utils.TransferToSqlVariables(target, source, prefix));
    }

    public async Task<IDictionary<string, object>> DropComplexMemberAsync(Guid tenant, IDictionary<string, object> input)
    {
        return await Task.FromResult(Utils.DropComplexMember(input));
    }

    public async Task<IDictionary<string, object>> NormalizeJsonMemberAsync(Guid tenant, IDictionary<string, object> input)
    {
        return await Task.FromResult(Utils.NormalizeJsonMember(input));
    }
}