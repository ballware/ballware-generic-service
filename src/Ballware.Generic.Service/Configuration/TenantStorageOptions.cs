using System.ComponentModel.DataAnnotations;

namespace Ballware.Generic.Service.Configuration;

public class TenantStorageOptions
{
    [Required]
    public required string Provider { get; set; }
    
    [Required]
    public required string ConnectionString { get; set; }
}