using System.Runtime.Serialization;

namespace Ballware.Generic.Metadata;

public class QueryEntry
{
    public required string Identifier { get; set; }
    public required string Query { get; set; }
}

public enum CustomFunctionTypes
{
    [EnumMember(Value = @"add")]
    Add = 0,

    [EnumMember(Value = @"edit")]
    Edit = 1,

    [EnumMember(Value = @"default_add")]
    DefaultAdd = 2,

    [EnumMember(Value = @"default_view")]
    DefaultView = 3,

    [EnumMember(Value = @"default_edit")]
    DefaultEdit = 4,

    [EnumMember(Value = @"external")]
    External = 5,

    [EnumMember(Value = @"export")]
    Export = 6,

    [EnumMember(Value = @"import")]
    Import = 7,
}

public class CustomFunctionOptions
{
    public string? Format { get; set; }

    public string? Delimiter { get; set; }
}

public class CustomFunctionEntry
{
    public required string Id { get; set; }
    public required CustomFunctionTypes Type { get; set; }
    public CustomFunctionOptions? Options { get; set; }
}

public class Entity
{
    public IEnumerable<QueryEntry> ListQuery { get; set; }
    public IEnumerable<QueryEntry> NewQuery { get; set; }
    public IEnumerable<QueryEntry> ByIdQuery { get; set; }
    public IEnumerable<QueryEntry> SaveStatement { get; set; }
    
    public IEnumerable<CustomFunctionEntry> CustomFunctions { get; set; }
    
    public string? ScalarValueQuery { get; set; }
    
    public string? RemoveStatement { get; set; }
    
    public required string Application { get; set; }
    public required string Identifier { get; set; }
    
    public string? ByIdScript { get; set; }
    
    public string? ListScript { get; set; }
    
    public string? BeforeSaveScript { get; set; }
    
    public string? SaveScript { get; set; }
    
    public string? RemovePreliminaryCheckScript { get; set; }
    
    public string? RemoveScript { get; set; }
    
    public string? ExtendedRightsCheckScript { get; set; }
    
    public string? StateColumn { get; set; }
    
    public string? StateAllowedScript { get; set; }
}