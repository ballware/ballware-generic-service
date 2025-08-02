using AutoMapper;
using Ballware.Generic.Data.Repository;
using Ballware.Shared.Data.Ef.Repository;
using Microsoft.EntityFrameworkCore;

namespace Ballware.Generic.Data.Ef.Internal;

class TenantConnectionRepository : BaseRepository<Public.TenantConnection, Persistables.TenantConnection>, ITenantConnectionRepository
{
    private TenantDbContext TenantContext { get; }

    public TenantConnectionRepository(IMapper mapper, TenantDbContext dbContext) : base(mapper, dbContext, null)
    {
        TenantContext = dbContext;   
    }
    
    public virtual async Task<Public.TenantConnection?> ByIdAsync(Guid id)
    {
        var result = await TenantContext.TenantConnections.SingleOrDefaultAsync(t => t.Uuid == id);

        return result != null ? Mapper.Map<Public.TenantConnection>(result) : null;
    }
}