namespace Ballware.Generic.Api.Public;

public class EntitySchema
{
    public Guid UserId { get; set; }
    public required string SerializedEntityModel { get; set; }
}