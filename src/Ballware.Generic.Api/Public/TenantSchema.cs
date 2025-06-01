namespace Ballware.Generic.Api.Public;

public class TenantSchema
{
    public Guid UserId { get; set; }
    public required string Provider { get; set; }
    public required string SerializedTenantModel { get; set; }
}