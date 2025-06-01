namespace Ballware.Generic.Metadata;

public class Statistic
{
    public required string Identifier { get; set; }
    public required string FetchSql { get; set; }
    public string? FetchScript { get; set; }
}