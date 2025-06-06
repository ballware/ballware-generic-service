using AutoMapper;
using Ballware.Generic.Data.Ef.Internal;

namespace Ballware.Generic.Data.Ef;

public static class MapperConfigurationExtensions
{
    public static IMapperConfigurationExpression AddBallwareTenantStorageMappings(
        this IMapperConfigurationExpression configuration)
    {
        configuration.AddProfile<StorageMappingProfile>();

        return configuration;
    }
}