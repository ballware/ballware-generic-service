namespace Ballware.Generic.Metadata;

public class Tenant
{
    public Guid Id { get; set; }
    public required string Provider { get; set; }
    
    public string? ServerScriptDefinitions { get; set; }
    public string? RightsCheckScript { get; set; }
    public IEnumerable<ReportDatasourceDefinition> ReportDatasourceDefinitions { get; set; }
}