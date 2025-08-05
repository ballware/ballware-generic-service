using System.Data;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;

namespace Ballware.Generic.Tenant.Data.Postgres.Internal;

class PostgresColumnType
{
    private static readonly Dictionary<string, PostgresColumnType> Types = new();
    
    private readonly string _literalValue;

    [JsonConstructor]
    private PostgresColumnType(string literalValue)
    {
        _literalValue = literalValue;
        
        Types.Add(literalValue, this);
    }

    public override string ToString()
    {
        return _literalValue;
    }

    public static PostgresColumnType Parse(string literalValue)
    {
        return Types.GetValueOrDefault(literalValue, PostgresColumnType.Unknown);
    }
    
    public static readonly PostgresColumnType Unknown = new PostgresColumnType("unknown");
    public static readonly PostgresColumnType Long = new PostgresColumnType("bigint");
    public static readonly PostgresColumnType Uuid = new PostgresColumnType("uuid");
    public static readonly PostgresColumnType Bool = new PostgresColumnType("boolean");
    public static readonly PostgresColumnType Int = new PostgresColumnType("integer");
    public static readonly PostgresColumnType Float = new PostgresColumnType("real");
    public static readonly PostgresColumnType Date = new PostgresColumnType("date");
    public static readonly PostgresColumnType Datetime = new PostgresColumnType("timestamp");
    public static readonly PostgresColumnType String = new PostgresColumnType("varchar");
    public static readonly PostgresColumnType Text = new PostgresColumnType("text");

    public static PostgresColumnType Custom(string literalValue)
    {
        return new PostgresColumnType(literalValue);
    }
}

class PostgresColumnTypeHandler : SqlMapper.TypeHandler<PostgresColumnType>
{
    public override PostgresColumnType Parse(object value)
    {
        return PostgresColumnType.Parse(value.ToString() ?? "");
    }

    public override void SetValue(IDbDataParameter parameter, PostgresColumnType? value)
    {
        parameter.Value = value?.ToString();
    }
}

class PostgresColumnTypeConverter : JsonConverter<PostgresColumnType>
{
    public override PostgresColumnType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return PostgresColumnType.Parse(reader.GetString());
    }

    public override void Write(Utf8JsonWriter writer, PostgresColumnType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value?.ToString());
    }
}

class PostgresColumnModel
{
    public required string ColumnName { get; set; }    
    
    [JsonConverter(typeof(PostgresColumnTypeConverter))]
    public required PostgresColumnType ColumnType { get; set; }
    
    public int? MaxLength { get; set; }
    public bool Nullable { get; set; }
}

class PostgresIndexModel
{
    public string? IndexName { get; set; }
    public bool Unique { get; set; }
    public required IEnumerable<string> ColumnNames { get; set; }
}

class PostgresTableModel
{
    public required string TableName { get; set; }
    public bool NoIdentity { get; set; }
    public required IEnumerable<PostgresColumnModel> CustomColumns { get; set; }
    public required IEnumerable<PostgresIndexModel> CustomIndexes { get; set; }
    
    public static PostgresTableModel Empty =>
        new()
        {
            TableName = string.Empty,
            CustomColumns = [],
            CustomIndexes = [],
        };
}

[JsonConverter(typeof(JsonStringEnumConverter))]
enum PostgresDatabaseObjectTypes
{
    [EnumMember(Value = "unknown")]
    Unknown = 0,
    [EnumMember(Value = "table")]
    Table = 1,
    [EnumMember(Value = "view")]
    View = 2,
    [EnumMember(Value = "function")]
    Function = 3,
    [EnumMember(Value = "type")]
    Type = 4,
    [EnumMember(Value = "statement")]
    Statement = 5
}

class PostgresDatabaseObjectModel
{
    public required string Name { get; set; }
    public required PostgresDatabaseObjectTypes Type { get; set; }
    public required string Sql { get; set; }
    public required bool Execute { get; set; }
}

class PostgresTenantModel
{
    public string? Server { get; set; }
    public string? Catalog { get; set; }
    public string? Schema { get; set; }
    public IEnumerable<PostgresDatabaseObjectModel>? DatabaseObjects { get; set; }
    
    public static PostgresTenantModel Empty => new ()
    {
        DatabaseObjects = [],
    };
}