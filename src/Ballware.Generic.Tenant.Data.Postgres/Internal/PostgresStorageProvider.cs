using System.Data;
using Ballware.Generic.Data.Repository;
using Ballware.Generic.Tenant.Data.Commons.Provider;
using Npgsql;

namespace Ballware.Generic.Tenant.Data.Postgres.Internal;

class PostgresStorageProvider : CommonStorageProvider
{
    public PostgresStorageProvider(ITenantConnectionRepository connectionRepository)
        : base(connectionRepository)
    {
    }

    public override async Task<IDbConnection> OpenConnectionAsync(Guid tenant)
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

    public override async Task<string> ApplyTenantPlaceholderAsync(Guid tenant, string source, TenantPlaceholderOptions options)
    {
        var tenantConnection = await ConnectionRepository.ByIdAsync(tenant);

        if (tenantConnection == null)
        {
            throw new ArgumentException($"Tenant {tenant} does not exist");
        }
        
        if (!string.IsNullOrEmpty(source))
        {
            source = source.Replace("[ballwareschema]", tenantConnection.Schema ?? "public");
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

    public override async Task<T> TransferToVariablesAsync<T>(Guid tenant, T target, IDictionary<string, object>? source, string prefix = "")
    {
        return await Task.FromResult(Utils.TransferToSqlVariables(target, source, prefix));
    }
}