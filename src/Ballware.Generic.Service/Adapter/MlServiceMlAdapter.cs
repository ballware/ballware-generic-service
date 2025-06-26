using AutoMapper;
using Ballware.Generic.Metadata;
using Ballware.Ml.Client;

namespace Ballware.Generic.Service.Adapter;

public class MlServiceMlAdapter : IMlAdapter
{
    private IMapper Mapper { get; }
    private BallwareMlClient MlClient { get; }
    
    public MlServiceMlAdapter(IMapper mapper, BallwareMlClient mlClient)
    {
        Mapper = mapper;
        MlClient = mlClient;
    }

    public async Task<object> ConsumeByIdentifierBehalfOfUserAsync(Guid tenant, Guid user, string model, IDictionary<string, object> query)
    {
        return await MlClient.MlModelConsumeByIdentifierBehalfOfUserAsync(tenant, user, model, query);
    }
}