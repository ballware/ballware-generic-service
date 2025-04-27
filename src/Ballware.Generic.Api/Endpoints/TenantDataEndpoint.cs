using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Ballware.Generic.Tenant.Data;
using Ballware.Generic.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ballware.Generic.Api.Endpoints;

public static class TenantDataEndpoint
{
    private static readonly string DefaultQuery = "primary";

    public static IEndpointRouteBuilder MapTenantServiceDataApi(this IEndpointRouteBuilder app,
        string basePath,
        string apiTag = "Tenant",
        string apiOperationPrefix = "Tenant",
        string authorizationScope = "serviceApi",
        string apiGroup = "service")
    {
        app.MapGet(basePath + "/reportdatasourcesfortenant/{tenantId}", HandleReportDatasourcesForTenant)
            .RequireAuthorization(authorizationScope)
            .Produces<IEnumerable<ReportDatasourceDefinition>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "ReportDatasources")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query report datasources for tenant");

        return app;
    }

    public static async Task<IResult> HandleReportDatasourcesForTenant(ClaimsPrincipal user,
        IMetadataAdapter metadataAdapter, ITenantStorageProvider storageProvider, Guid tenantId)
    {
        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        
        var tenantConnectionString = await storageProvider.GetConnectionStringAsync(tenantId);

        var schemaDefinitions = new List<ReportDatasourceDefinition>();
        
        var tenantLookupsSchemaDefinition = new ReportDatasourceDefinition
        {
            Name = "Lookups",
            ConnectionString = tenantConnectionString,
            Tables = (await metadataAdapter.MetadataForLookupsByTenantAsync(tenantId))
                  .Where(l => !l.HasParam)
                  .Select(l =>
                  {
                      l.ListQuery = storageProvider.ApplyTenantPlaceholderAsync(tenantId, l.ListQuery, TenantPlaceholderOptions.Create().WithReplaceTenantId().WithReplaceClaims()).GetAwaiter().GetResult();

                      return l;
                  })
                  .Select(l => new ReportDatasourceTable
                  {
                      Name = l.Identifier,
                      Query = l.ListQuery
                  })
        };

        schemaDefinitions.Add(tenantLookupsSchemaDefinition);
        
        foreach (var schemaDefinition in tenant?.ReportDatasourceDefinitions ?? new List<ReportDatasourceDefinition>())
        {
            schemaDefinition.ConnectionString = tenantConnectionString;

            foreach (var table in schemaDefinition.Tables ?? new List<ReportDatasourceTable>())
            {
                if (!string.IsNullOrEmpty(table.Entity))
                {
                    var entityMeta = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, table.Entity);

                    table.Query = storageProvider.ApplyTenantPlaceholderAsync(tenantId,
                        entityMeta.ListQuery.FirstOrDefault(q => q.Identifier == (table.Query ?? DefaultQuery))?.Query,
                        TenantPlaceholderOptions.Create().WithReplaceTenantId()).GetAwaiter().GetResult();
                }
            }

            schemaDefinitions.Add(schemaDefinition);
        }

        return Results.Ok(schemaDefinitions);
    }
}