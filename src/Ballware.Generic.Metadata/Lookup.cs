namespace Ballware.Generic.Metadata;

public class Lookup
{
    public required string Identifier { get; set; }
    public required string ListQuery { get; set; }
    public string? ByIdQuery { get; set; }
    public bool HasParam { get; set; }
}