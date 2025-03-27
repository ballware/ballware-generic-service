using Ballware.Generic.Scripting;
using Ballware.Generic.Tenant.Data.SqlServer.Internal;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Tenant.Data.SqlServer;

public static class TenantStorageBuilderExtensions
{
    public static TenantStorageBuilder AddSqlServerTenantDataStorage(this TenantStorageBuilder builder, string tenantMasterConnectionString)
    {
        builder.Services.AddSingleton<SqlServerTenantConfiguration>(new SqlServerTenantConfiguration()
        {
            TenantMasterConnectionString = tenantMasterConnectionString
        });
        
        builder.Services.AddScoped<SqlServerStorageProvider>();
        builder.Services.AddScoped<SqlServerGenericProvider>();
        builder.Services.AddScoped<SqlServerSchemaProvider>();
        builder.Services.AddScoped<SqlServerLookupProvider>();
        builder.Services.AddScoped<SqlServerMlModelProvider>();
        builder.Services.AddScoped<SqlServerStatisticProvider>();
        builder.Services.AddScoped<ITenantDataAdapter, SqlServerGenericScriptingDataAdapter>();
        
        builder.RegisterStorageProvider<SqlServerStorageProvider>("mssql");
        builder.RegisterGenericProvider<SqlServerGenericProvider>("mssql");
        builder.RegisterLookupProvider<SqlServerLookupProvider>("mssql");
        builder.RegisterMlModelProvider<SqlServerMlModelProvider>("mssql");
        builder.RegisterStatisticProvider<SqlServerStatisticProvider>("mssql");

        SqlMapper.AddTypeHandler(new SqlServerColumnTypeHandler());
        
        return builder;
    }
}