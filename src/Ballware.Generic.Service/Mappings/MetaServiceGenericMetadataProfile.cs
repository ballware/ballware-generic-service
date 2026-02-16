using Mapster;

namespace Ballware.Generic.Service.Mappings;

public class MetaServiceGenericMetadataProfile : IRegister
{
    public void Register(TypeAdapterConfig config)
    {   
        config.NewConfig<Ballware.Meta.Service.Client.ServiceTenant, Ballware.Generic.Metadata.Tenant>();
        config.NewConfig<Ballware.Meta.Service.Client.ServiceEntityQueryEntry, Ballware.Generic.Metadata.QueryEntry>();
        config.NewConfig<Ballware.Meta.Service.Client.ServiceEntityCustomFunction, Ballware.Generic.Metadata.CustomFunctionEntry>();
        config.NewConfig<Ballware.Meta.Service.Client.ServiceEntityCustomFunctionOptions, Ballware.Generic.Metadata.CustomFunctionOptions>();
        
        config.NewConfig<Ballware.Meta.Service.Client.ServiceTenantReportDatasourceDefinition,
            Ballware.Generic.Metadata.ReportDatasourceDefinition>();
        config.NewConfig<Ballware.Meta.Service.Client.ServiceTenantReportDatasourceTable,
            Ballware.Generic.Metadata.ReportDatasourceTable>();
        config.NewConfig<Ballware.Meta.Service.Client.ServiceTenantReportDatasourceRelation,
            Ballware.Generic.Metadata.ReportDatasourceRelation>();
        
        config.NewConfig<Ballware.Meta.Service.Client.ServiceEntity, Ballware.Generic.Metadata.Entity>()
            .Map(dst => dst.Identifier, source => source.Entity)
            .Map(dst => dst.ExtendedRightsCheckScript, 
                source => source.CustomScripts.ExtendedRightsCheck);
        config.NewConfig<Ballware.Meta.Service.Client.Lookup, Ballware.Generic.Metadata.Lookup>();
        config.NewConfig<Ballware.Meta.Service.Client.Statistic, Ballware.Generic.Metadata.Statistic>();
        config.NewConfig<Ballware.Meta.Service.Client.ProcessingStateSelectListEntry, Ballware.Generic.Metadata.ProcessingStateSelectListEntry>();
        config.NewConfig<Ballware.Meta.Service.Client.ProcessingStateSelectListEntry, Ballware.Generic.Metadata.ProcessingState>();
        config.NewConfig<Ballware.Meta.Service.Client.Export, Ballware.Generic.Metadata.Export>();
        config.NewConfig<Ballware.Generic.Metadata.JobCreatePayload, Ballware.Meta.Service.Client.JobCreatePayload>();
        config.NewConfig<Ballware.Generic.Metadata.JobUpdatePayload, Ballware.Meta.Service.Client.JobUpdatePayload>();
        config.NewConfig<Ballware.Generic.Metadata.ExportCreatePayload, Ballware.Meta.Service.Client.ExportCreatePayload>();
    }
}