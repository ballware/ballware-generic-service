using AutoMapper;

namespace Ballware.Generic.Data.Ef.Internal;

class StorageMappingProfile : Profile
{
    public StorageMappingProfile()
    {
        CreateMap<Public.TenantConnection, Persistables.TenantConnection>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Uuid, opt => opt.MapFrom(src => src.Id));

        CreateMap<Persistables.TenantConnection, Public.TenantConnection>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Uuid));
        
        CreateMap<Public.TenantEntity, Persistables.TenantEntity>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Uuid, opt => opt.MapFrom(src => src.Id));

        CreateMap<Persistables.TenantEntity, Public.TenantEntity>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Uuid));
    }
}