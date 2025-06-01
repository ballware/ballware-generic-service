using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ballware.Generic.Data.Persistables;

[Table("TenantConnection")]
public class TenantConnection : IEntity, IAuditable
{
    public long? Id { get; set; }
    public Guid Uuid { get; set; }
    
    [StringLength(50)]
    public string? Provider { get; set; }
    
    [StringLength(50)]
    public string? Schema { get; set; }
    
    [StringLength(500)]
    public string? ConnectionString { get; set; }
    
    public string? Model { get; set; }
    
    public Guid? CreatorId { get; set; }
    public DateTime? CreateStamp { get; set; }
    public Guid? LastChangerId { get; set; }
    public DateTime? LastChangeStamp { get; set; }
}