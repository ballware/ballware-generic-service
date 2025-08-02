using AutoMapper;
using Ballware.Generic.Data.Repository;
using Ballware.Shared.Data.Ef.Repository;
using Microsoft.EntityFrameworkCore;

namespace Ballware.Generic.Data.Ef.Internal;

class TenantEntityRepository : TenantableBaseRepository<Public.TenantEntity, Persistables.TenantEntity>, ITenantEntityRepository
{
    private TenantDbContext TenantContext { get; }

    public TenantEntityRepository(IMapper mapper, TenantDbContext dbContext) : base(mapper, dbContext, null)
    {
        TenantContext = dbContext;  
    }
    
    public async Task<Public.TenantEntity?> ByEntityAsync(Guid tenantId, string entity)
    {
        var result = await TenantContext.TenantEntities.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Entity == entity);

        return result != null ? Mapper.Map<Public.TenantEntity>(result) : null;
    }
}