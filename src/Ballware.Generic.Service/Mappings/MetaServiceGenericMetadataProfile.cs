using AutoMapper;

namespace Ballware.Generic.Service.Mappings;

public class MetaServiceGenericMetadataProfile : Profile
{
    public MetaServiceGenericMetadataProfile()
    {
        CreateMap<Ballware.Meta.Client.ServiceTenant, Ballware.Generic.Metadata.Tenant>();
        CreateMap<Ballware.Meta.Client.ServiceEntityQueryEntry, Ballware.Generic.Metadata.QueryEntry>();
        CreateMap<Ballware.Meta.Client.ServiceEntityCustomFunction, Ballware.Generic.Metadata.CustomFunctionEntry>();
        CreateMap<Ballware.Meta.Client.ServiceEntityCustomFunctionOptions, Ballware.Generic.Metadata.CustomFunctionOptions>();
        
        CreateMap<Ballware.Meta.Client.ServiceTenantReportDatasourceDefinition,
            Ballware.Generic.Metadata.ReportDatasourceDefinition>();
        CreateMap<Ballware.Meta.Client.ServiceTenantReportDatasourceTable,
            Ballware.Generic.Metadata.ReportDatasourceTable>();
        
        CreateMap<Ballware.Meta.Client.ServiceEntity, Ballware.Generic.Metadata.Entity>()
            .ForMember(dst => dst.Identifier, opt => opt.MapFrom(source => source.Entity))
            .ForMember(dst => dst.ExtendedRightsCheckScript, 
                opt => opt.MapFrom(source => source.CustomScripts.ExtendedRightsCheck));
        CreateMap<Ballware.Meta.Client.Lookup, Ballware.Generic.Metadata.Lookup>();
        CreateMap<Ballware.Meta.Client.MlModel, Ballware.Generic.Metadata.MlModel>();
        CreateMap<Ballware.Meta.Client.Statistic, Ballware.Generic.Metadata.Statistic>();
        CreateMap<Ballware.Meta.Client.ProcessingStateSelectListEntry, Ballware.Generic.Metadata.ProcessingStateSelectListEntry>();
        CreateMap<Ballware.Meta.Client.ProcessingStateSelectListEntry, Ballware.Generic.Metadata.ProcessingState>();
        CreateMap<Ballware.Meta.Client.Notification, Ballware.Generic.Metadata.Notification>();
        CreateMap<Ballware.Generic.Metadata.JobCreatePayload, Ballware.Meta.Client.JobCreatePayload>();
        CreateMap<Ballware.Generic.Metadata.JobUpdatePayload, Ballware.Meta.Client.JobUpdatePayload>();
        CreateMap<Ballware.Generic.Metadata.ExportCreatePayload, Ballware.Meta.Client.ExportCreatePayload>();
    }
}