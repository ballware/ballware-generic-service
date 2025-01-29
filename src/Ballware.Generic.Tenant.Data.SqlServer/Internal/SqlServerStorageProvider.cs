using System.Data;
using Ballware.Meta.Client;
using Microsoft.Data.SqlClient;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

class SqlServerStorageProvider : ITenantStorageProvider
{
    public string GetConnectionString(ServiceTenant tenant)
    {
        return Utils.GetConnectionString(tenant);
    }

    public IDbConnection OpenConnection(ServiceTenant tenant)
    {
        var connection = new SqlConnection(Utils.GetConnectionString(tenant));

        connection.Open();

        return connection;
    }

    public async Task<IDbConnection> OpenConnectionAsync(ServiceTenant tenant)
    {
        var connection = new SqlConnection(Utils.GetConnectionString(tenant));

        await connection.OpenAsync();

        return connection;
    }

    public string ApplyTenantPlaceholder(ServiceTenant tenant, string source, TenantPlaceholderOptions options)
    {
        if (!string.IsNullOrEmpty(source))
        {
            source = source.Replace("[ballwareschema]", tenant.Schema ?? "dbo");
        }

        if (!string.IsNullOrEmpty(source) && options.ReplaceTenantId)
        {
            source = source.Replace("@tenantId", $"'{tenant.Id}'");
        }

        if (!string.IsNullOrEmpty(source) && options.ReplaceClaims)
        {
            source = source.Replace("@claims", "''");
        }

        return source;
    }

    public Task<string> ApplyTenantPlaceholderAsync(ServiceTenant tenant, string source, TenantPlaceholderOptions options)
    {
        return Task.FromResult(ApplyTenantPlaceholder(tenant, source, options));
    }
}