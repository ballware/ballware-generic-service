using Mapster;

namespace Ballware.Generic.Data.Ef.Mapping;

class StorageMappingProfile : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Public.TenantConnection, Persistables.TenantConnection>()
            .Ignore(dest => dest.Id!)
            .Map(dest => dest.Uuid, src => src.Id);
        
        config.NewConfig<Persistables.TenantConnection, Public.TenantConnection>()
            .Map(dest => dest.Id, src => src.Uuid);
        
        config.NewConfig<Public.TenantEntity, Persistables.TenantEntity>()
            .Ignore(dest => dest.Id!)
            .Map(dest => dest.Uuid, src => src.Id);
        
        config.NewConfig<Persistables.TenantEntity, Public.TenantEntity>()
            .Map(dest => dest.Id, src => src.Uuid);
    }
}