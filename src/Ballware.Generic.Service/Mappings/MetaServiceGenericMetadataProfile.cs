using AutoMapper;

namespace Ballware.Generic.Service.Mappings;

public class MetaServiceGenericMetadataProfile : Profile
{
    public MetaServiceGenericMetadataProfile()
    {
        CreateMap<Ballware.Meta.Service.Client.ServiceTenant, Ballware.Generic.Metadata.Tenant>();
        CreateMap<Ballware.Meta.Service.Client.ServiceEntityQueryEntry, Ballware.Generic.Metadata.QueryEntry>();
        CreateMap<Ballware.Meta.Service.Client.ServiceEntityCustomFunction, Ballware.Generic.Metadata.CustomFunctionEntry>();
        CreateMap<Ballware.Meta.Service.Client.ServiceEntityCustomFunctionOptions, Ballware.Generic.Metadata.CustomFunctionOptions>();
        
        CreateMap<Ballware.Meta.Service.Client.ServiceTenantReportDatasourceDefinition,
            Ballware.Generic.Metadata.ReportDatasourceDefinition>();
        CreateMap<Ballware.Meta.Service.Client.ServiceTenantReportDatasourceTable,
            Ballware.Generic.Metadata.ReportDatasourceTable>();
        CreateMap<Ballware.Meta.Service.Client.ServiceTenantReportDatasourceRelation,
            Ballware.Generic.Metadata.ReportDatasourceRelation>();
        
        CreateMap<Ballware.Meta.Service.Client.ServiceEntity, Ballware.Generic.Metadata.Entity>()
            .ForMember(dst => dst.Identifier, opt => opt.MapFrom(source => source.Entity))
            .ForMember(dst => dst.ExtendedRightsCheckScript, 
                opt => opt.MapFrom(source => source.CustomScripts.ExtendedRightsCheck));
        CreateMap<Ballware.Meta.Service.Client.Lookup, Ballware.Generic.Metadata.Lookup>();
        CreateMap<Ballware.Meta.Service.Client.MlModel, Ballware.Generic.Metadata.MlModel>();
        CreateMap<Ballware.Meta.Service.Client.Statistic, Ballware.Generic.Metadata.Statistic>();
        CreateMap<Ballware.Meta.Service.Client.ProcessingStateSelectListEntry, Ballware.Generic.Metadata.ProcessingStateSelectListEntry>();
        CreateMap<Ballware.Meta.Service.Client.ProcessingStateSelectListEntry, Ballware.Generic.Metadata.ProcessingState>();
        CreateMap<Ballware.Meta.Service.Client.Notification, Ballware.Generic.Metadata.Notification>();
        CreateMap<Ballware.Meta.Service.Client.Export, Ballware.Generic.Metadata.Export>();
        CreateMap<Ballware.Generic.Metadata.JobCreatePayload, Ballware.Meta.Service.Client.JobCreatePayload>();
        CreateMap<Ballware.Generic.Metadata.JobUpdatePayload, Ballware.Meta.Service.Client.JobUpdatePayload>();
        CreateMap<Ballware.Generic.Metadata.ExportCreatePayload, Ballware.Meta.Service.Client.ExportCreatePayload>();
    }
}