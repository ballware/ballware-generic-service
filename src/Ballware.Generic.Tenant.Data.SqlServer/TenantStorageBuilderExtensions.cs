using Ballware.Generic.Scripting;
using Ballware.Generic.Tenant.Data.SqlServer.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Tenant.Data.SqlServer;

public static class TenantStorageBuilderExtensions
{
    public static TenantStorageBuilder AddSqlServerTenantDataStorage(this TenantStorageBuilder builder)
    {
        builder.Services.AddSingleton<SqlServerStorageProvider>();
        builder.Services.AddSingleton<SqlServerGenericProvider>();
        builder.Services.AddSingleton<ITenantDataAdapter, SqlServerGenericScriptingDataAdapter>();
        
        builder.RegisterStorageProvider<SqlServerStorageProvider>("mssql");
        builder.RegisterGenericProvider<SqlServerGenericProvider>("mssql");

        return builder;
    }
}