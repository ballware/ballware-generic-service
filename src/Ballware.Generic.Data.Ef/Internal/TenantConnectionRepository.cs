using AutoMapper;
using Ballware.Generic.Data.Repository;
using Microsoft.EntityFrameworkCore;

namespace Ballware.Generic.Data.Ef.Internal;

class TenantConnectionRepository : BaseRepository<Public.TenantConnection, Persistables.TenantConnection>, ITenantConnectionRepository
{
    public TenantConnectionRepository(IMapper mapper, TenantDbContext dbContext) : base(mapper, dbContext) { }
    
    public virtual async Task<Public.TenantConnection?> ByIdAsync(Guid id)
    {
        var result = await Context.TenantConnections.SingleOrDefaultAsync(t => t.Uuid == id);

        return result != null ? Mapper.Map<Public.TenantConnection>(result) : null;
    }
}