using AutoMapper;
using Ballware.Generic.Data.Repository;
using Microsoft.EntityFrameworkCore;

namespace Ballware.Generic.Data.Ef.Internal;

class TenantEntityRepository : BaseRepository<Public.TenantEntity, Persistables.TenantEntity>, ITenantEntityRepository
{
    public TenantEntityRepository(IMapper mapper, TenantDbContext dbContext) : base(mapper, dbContext) { }
    
    public async Task<Public.TenantEntity?> ByEntityAsync(Guid tenant, string entity)
    {
        var result = await Context.TenantEntities.SingleOrDefaultAsync(t => t.TenantId == tenant && t.Entity == entity);

        return result != null ? Mapper.Map<Public.TenantEntity>(result) : null;
    }
}