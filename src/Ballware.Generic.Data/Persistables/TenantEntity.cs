using System.ComponentModel.DataAnnotations;
using Ballware.Shared.Data.Persistables;

namespace Ballware.Generic.Data.Persistables;

public class TenantEntity : IEntity, IAuditable
{
    public long? Id { get; set; }
    public Guid Uuid { get; set; }
    
    [Required]
    public Guid TenantId { get; set; }
    
    [Required]
    public string? Entity { get; set; }
    
    public string? Model { get; set; }
    
    public Guid? CreatorId { get; set; }
    public DateTime? CreateStamp { get; set; }
    public Guid? LastChangerId { get; set; }
    public DateTime? LastChangeStamp { get; set; }
}