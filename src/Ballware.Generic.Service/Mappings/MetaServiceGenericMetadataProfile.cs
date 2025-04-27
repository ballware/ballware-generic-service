using AutoMapper;

namespace Ballware.Generic.Service.Mappings;

public class MetaServiceGenericMetadataProfile : Profile
{
    public MetaServiceGenericMetadataProfile()
    {
        CreateMap<Ballware.Meta.Client.ServiceTenant, Ballware.Generic.Metadata.Tenant>();
        CreateMap<Ballware.Meta.Client.ServiceEntity, Ballware.Generic.Metadata.Entity>();
        CreateMap<Ballware.Meta.Client.Lookup, Ballware.Generic.Metadata.Lookup>();
        CreateMap<Ballware.Meta.Client.MlModel, Ballware.Generic.Metadata.MlModel>();
        CreateMap<Ballware.Meta.Client.Statistic, Ballware.Generic.Metadata.Statistic>();
        CreateMap<Ballware.Meta.Client.ProcessingStateSelectListEntry, Ballware.Generic.Metadata.ProcessingStateSelectListEntry>();
        CreateMap<Ballware.Meta.Client.ProcessingStateSelectListEntry, Ballware.Generic.Metadata.ProcessingState>();
        CreateMap<Ballware.Meta.Client.Notification, Ballware.Generic.Metadata.Notification>();
        CreateMap<Ballware.Meta.Client.NotificationTrigger, Ballware.Generic.Metadata.NotificationTrigger>();
        CreateMap<Ballware.Generic.Metadata.NotificationTrigger, Ballware.Meta.Client.NotificationTrigger>();
        CreateMap<Ballware.Generic.Metadata.JobCreatePayload, Ballware.Meta.Client.JobCreatePayload>();
        CreateMap<Ballware.Generic.Metadata.JobUpdatePayload, Ballware.Meta.Client.JobUpdatePayload>();
        CreateMap<Ballware.Generic.Metadata.ExportCreatePayload, Ballware.Meta.Client.ExportCreatePayload>();
    }
}