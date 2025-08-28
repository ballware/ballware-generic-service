using Ballware.Generic.Scripting;
using Ballware.Generic.Tenant.Data.SqlServer.Configuration;
using Ballware.Generic.Tenant.Data.SqlServer.Internal;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Tenant.Data.SqlServer;

public static class TenantStorageBuilderExtensions
{
    private static readonly string ProviderMssql = "mssql";
    
    public static TenantStorageBuilder AddSqlServerTenantDataStorage(this TenantStorageBuilder builder, string tenantMasterConnectionString, SqlServerTenantStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(tenantMasterConnectionString))
            throw new ArgumentNullException(nameof(tenantMasterConnectionString));
        
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var useContainedDatabase = options.UseContainedDatabase;
    
        builder.Services.AddSingleton<SqlServerTenantConfiguration>(new SqlServerTenantConfiguration()
        {
            TenantMasterConnectionString = tenantMasterConnectionString,
            UseContainedDatabase = useContainedDatabase
        });
        
        builder.Services.AddScoped<SqlServerStorageProvider>();
        builder.Services.AddScoped<SqlServerGenericProvider>();
        builder.Services.AddScoped<SqlServerSchemaProvider>();
        builder.Services.AddScoped<SqlServerLookupProvider>();
        builder.Services.AddScoped<SqlServerMlModelProvider>();
        builder.Services.AddScoped<SqlServerStatisticProvider>();
        builder.Services.AddScoped<IScriptingTenantDataAdapter, SqlServerGenericScriptingDataAdapter>();
        
        builder.RegisterStorageProvider<SqlServerStorageProvider>(ProviderMssql);
        builder.RegisterGenericProvider<SqlServerGenericProvider>(ProviderMssql);
        builder.RegisterLookupProvider<SqlServerLookupProvider>(ProviderMssql);
        builder.RegisterMlModelProvider<SqlServerMlModelProvider>(ProviderMssql);
        builder.RegisterStatisticProvider<SqlServerStatisticProvider>(ProviderMssql);
        builder.RegisterSchemaProvider<SqlServerSchemaProvider>(ProviderMssql);

        SqlMapper.AddTypeHandler(new SqlServerColumnTypeHandler());
        SqlMapper.AddTypeHandler(new SqlServerComplexTypeHandler());
        
        return builder;
    }
}