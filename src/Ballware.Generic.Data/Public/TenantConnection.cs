namespace Ballware.Generic.Data.Public;

public class TenantConnection : IEditable
{
    public Guid Id { get; set; }
    
    public string? Provider { get; set; }
    
    public string? Schema { get; set; }
    public string? ConnectionString { get; set; }   
    public string? Model { get; set; }
}