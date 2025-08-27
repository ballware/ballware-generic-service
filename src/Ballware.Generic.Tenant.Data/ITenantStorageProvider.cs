using System.Data;

namespace Ballware.Generic.Tenant.Data;

public interface ITenantStorageProvider
{
    Task<string> GetProviderAsync(Guid tenant);
    Task<string> GetConnectionStringAsync(Guid tenant);
    Task<IDbConnection> OpenConnectionAsync(Guid tenant);
    Task<string> ApplyTenantPlaceholderAsync(Guid tenant, string source, TenantPlaceholderOptions options);

    Task<T> TransferToVariablesAsync<T>(Guid tenant, T target, IDictionary<string, object>? source, string prefix = "")
        where T : IDictionary<string, object>;

    Task<IDictionary<string, object>> DropComplexMemberAsync(Guid tenant, IDictionary<string, object> input);
    Task<IDictionary<string, object>> NormalizeJsonMemberAsync(Guid tenant, IDictionary<string, object> input);
}