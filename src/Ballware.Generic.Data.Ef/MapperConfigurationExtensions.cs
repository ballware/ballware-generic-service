using Ballware.Generic.Data.Ef.Mapping;
using Mapster;

namespace Ballware.Generic.Data.Ef;

public static class MapperConfigurationExtensions
{
    public static TypeAdapterConfig AddBallwareTenantStorageMappings(
        this TypeAdapterConfig configuration)
    {
        new StorageMappingProfile().Register(configuration);

        return configuration;
    }
}