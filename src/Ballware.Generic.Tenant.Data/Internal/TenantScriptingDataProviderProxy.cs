using Ballware.Generic.Scripting;

namespace Ballware.Generic.Tenant.Data.Internal;

class TenantScriptingDataProviderProxy : IScriptingTenantDataProvider
{
    private IProviderRegistry ProviderRegistry { get; }

    public TenantScriptingDataProviderProxy(IProviderRegistry providerRegistry)
    {
        ProviderRegistry = providerRegistry;
    }


    public IEnumerable<dynamic> RawQuery(IScriptingEntityUserContext context, string table, string columns, string where, object p)
    {
        var provider = ProviderRegistry.GetScriptingDataProvider(context.Tenant.Provider);
        
        return provider.RawQuery(context, table, columns, where, p);
    }

    public int RawCount(IScriptingEntityUserContext context, string table, string where, object p)
    {
        var provider = ProviderRegistry.GetScriptingDataProvider(context.Tenant.Provider);
        
        return provider.RawCount(context, table, where, p);
    }

    public void RawDelete(IScriptingEntityUserContext context, string table, string where, object p)
    {
        var provider = ProviderRegistry.GetScriptingDataProvider(context.Tenant.Provider);
        
        provider.RawDelete(context, table, where, p);
    }

    public void RawInsert(IScriptingEntityUserContext context, string table, string columns, string values, object p)
    {
        var provider = ProviderRegistry.GetScriptingDataProvider(context.Tenant.Provider);
        
        provider.RawInsert(context, table, columns, values, p);
    }

    public void RawUpdate(IScriptingEntityUserContext context, string table, string columns, string where, object p)
    {
        var provider = ProviderRegistry.GetScriptingDataProvider(context.Tenant.Provider);
        
        provider.RawUpdate(context, table, columns, where, p);
    }

    public object? QueryScalarValue(IScriptingEntityUserContext context, string column, IDictionary<string, object> p)
    {
        var provider = ProviderRegistry.GetScriptingDataProvider(context.Tenant.Provider);
        
        return provider.QueryScalarValue(context, column, p);
    }

    public long Count(IScriptingEntityUserContext context, string queryIdentifier, IDictionary<string, object> p)
    {
        var provider = ProviderRegistry.GetScriptingDataProvider(context.Tenant.Provider);
        
        return provider.Count(context, queryIdentifier, p);
    }

    public IEnumerable<dynamic> QueryList(IScriptingEntityUserContext context, string queryIdentifier, IDictionary<string, object> p)
    {
        var provider = ProviderRegistry.GetScriptingDataProvider(context.Tenant.Provider);
        
        return provider.QueryList(context, queryIdentifier, p);
    }

    public dynamic? QuerySingle(IScriptingEntityUserContext context, string queryIdentifier, IDictionary<string, object> p)
    {
        var provider = ProviderRegistry.GetScriptingDataProvider(context.Tenant.Provider);
        
        return provider.QuerySingle(context, queryIdentifier, p);
    }

    public dynamic? QueryNew(IScriptingEntityUserContext context, string queryIdentifier, IDictionary<string, object> p)
    {
        var provider = ProviderRegistry.GetScriptingDataProvider(context.Tenant.Provider);
        
        return provider.QueryNew(context, queryIdentifier, p);
    }

    public void Save(IScriptingEntityUserContext context, string statementIdentifier, IDictionary<string, object> p)
    {
        var provider = ProviderRegistry.GetScriptingDataProvider(context.Tenant.Provider);
        
        provider.Save(context, statementIdentifier, p);
    }

    public (bool Result, IEnumerable<string> Messages) Remove(IScriptingEntityUserContext context, IDictionary<string, object> p)
    {
        var provider = ProviderRegistry.GetScriptingDataProvider(context.Tenant.Provider);
        
        return provider.Remove(context, p);
    }
}