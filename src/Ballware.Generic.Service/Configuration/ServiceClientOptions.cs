using System.ComponentModel.DataAnnotations;

namespace Ballware.Generic.Service.Configuration;

public class ServiceClientOptions
{
    [Required]
    public required string ServiceUrl { get; set; }
    
    [Required]
    public required string TokenEndpoint { get; set; }
    
    [Required]
    public required string ClientId { get; set; }
    
    [Required]
    public required string ClientSecret { get; set; }
    
    [Required]
    public required string Scopes { get; set; }
}