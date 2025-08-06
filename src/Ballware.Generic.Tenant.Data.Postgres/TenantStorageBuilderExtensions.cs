using Ballware.Generic.Scripting;
using Ballware.Generic.Tenant.Data.Postgres.Internal;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Tenant.Data.Postgres;

public static class TenantStorageBuilderExtensions
{
    private static readonly string ProviderPostgres = "postgres";
    
    public static TenantStorageBuilder AddPostgresTenantDataStorage(this TenantStorageBuilder builder, string tenantMasterConnectionString)
    {
        if (string.IsNullOrWhiteSpace(tenantMasterConnectionString))
            throw new ArgumentNullException(nameof(tenantMasterConnectionString));
        
        builder.Services.AddSingleton<PostgresTenantConfiguration>(new PostgresTenantConfiguration()
        {
            TenantMasterConnectionString = tenantMasterConnectionString
        });
        
        builder.Services.AddScoped<PostgresStorageProvider>();
        builder.Services.AddScoped<PostgresGenericProvider>();
        builder.Services.AddScoped<PostgresSchemaProvider>();
        builder.Services.AddScoped<PostgresLookupProvider>();
        builder.Services.AddScoped<PostgresMlModelProvider>();
        builder.Services.AddScoped<PostgresStatisticProvider>();
        builder.Services.AddScoped<ITenantDataAdapter, PostgresGenericScriptingDataAdapter>();
        
        builder.RegisterStorageProvider<PostgresStorageProvider>(ProviderPostgres);
        builder.RegisterGenericProvider<PostgresGenericProvider>(ProviderPostgres);
        builder.RegisterLookupProvider<PostgresLookupProvider>(ProviderPostgres);
        builder.RegisterMlModelProvider<PostgresMlModelProvider>(ProviderPostgres);
        builder.RegisterStatisticProvider<PostgresStatisticProvider>(ProviderPostgres);
        builder.RegisterSchemaProvider<PostgresSchemaProvider>(ProviderPostgres);

        SqlMapper.AddTypeHandler(new PostgresColumnTypeHandler());
        
        return builder;
    }
}