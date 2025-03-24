namespace Ballware.Generic.Metadata;

public class ExportCreatePayload
{
    public required string Application { get; set; }

    public required string Entity { get; set; }

    public required string Query { get; set; }

    public System.DateTimeOffset? ExpirationStamp { get; set; }

    public required string MediaType { get; set; }
}

public class Export
{
    public Guid Id { get; set; }
    
    public required string Application { get; set; }

    public required string Entity { get; set; }

    public required string Query { get; set; }

    public System.DateTimeOffset? ExpirationStamp { get; set; }

    public required string MediaType { get; set; }
}