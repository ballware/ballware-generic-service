using System.Data;
using Ballware.Generic.Data.Repository;
using Ballware.Generic.Tenant.Data.Commons.Utils;

namespace Ballware.Generic.Tenant.Data.Commons.Provider;

public abstract class CommonStorageProvider : ITenantStorageProvider
{
    protected ITenantConnectionRepository ConnectionRepository { get; }
    
    public CommonStorageProvider(ITenantConnectionRepository connectionRepository)
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

    public abstract Task<IDbConnection> OpenConnectionAsync(Guid tenant);

    public abstract Task<string> ApplyTenantPlaceholderAsync(Guid tenant, string source,
        TenantPlaceholderOptions options);


    public abstract Task<T> TransferToVariablesAsync<T>(Guid tenant, T target, IDictionary<string, object>? source,
        string prefix = "") where T : IDictionary<string, object>;

    public async Task<IDictionary<string, object>> DropComplexMemberAsync(Guid tenant, IDictionary<string, object> input)
    {
        return await Task.FromResult(ComplexDataUtils.DropComplexMember(input));
    }

    public async Task<IDictionary<string, object>> NormalizeJsonMemberAsync(Guid tenant, IDictionary<string, object> input)
    {
        return await Task.FromResult(ComplexDataUtils.NormalizeJsonMember(input));
    }
}