using Ballware.Shared.Data.Public;

namespace Ballware.Generic.Data.Public;

public class TenantEntity : IEditable
{
    public Guid Id { get; set; }
    
    public string? Entity { get; set; }
    
    public string? Model { get; set; }
}