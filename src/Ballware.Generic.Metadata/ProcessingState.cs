namespace Ballware.Generic.Metadata;

public class ProcessingState
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int State { get; set; }
}