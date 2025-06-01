namespace Ballware.Generic.Metadata;

public class NotificationTriggerCreatePayload
{
    public required Guid NotificationId { get; set; }
    
    public string? Params { get; set; }
}