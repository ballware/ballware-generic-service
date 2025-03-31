using System.Data;
using Ballware.Generic.Data.Repository;
using Microsoft.Data.SqlClient;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

class SqlServerStorageProvider : ITenantStorageProvider
{
    private ITenantConnectionRepository ConnectionRepository { get; }
    
    public SqlServerStorageProvider(ITenantConnectionRepository connectionRepository)
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
        
        var connection = new SqlConnection(tenantConnection.ConnectionString);

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
}