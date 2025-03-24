using System.Data;
using System.Runtime.Serialization;
using Dapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

class SqlServerColumnType
{
    private static readonly Dictionary<string, SqlServerColumnType> Types = new();
    
    private readonly string _literalValue;

    [JsonConstructor]
    private SqlServerColumnType(string literalValue)
    {
        _literalValue = literalValue;
        
        Types.Add(literalValue, this);
    }

    public override string ToString()
    {
        return _literalValue;
    }

    public static SqlServerColumnType Parse(string literalValue)
    {
        return Types.GetValueOrDefault(literalValue, SqlServerColumnType.Unknown);
    }

    public static SqlServerColumnType Unknown = new SqlServerColumnType("unknown");
    public static SqlServerColumnType Long = new SqlServerColumnType("bigint");
    public static SqlServerColumnType Uuid = new SqlServerColumnType("uniqueidentifier");
    public static SqlServerColumnType Bool = new SqlServerColumnType("bit");
    public static SqlServerColumnType Int = new SqlServerColumnType("int");
    public static SqlServerColumnType Float = new SqlServerColumnType("float");
    public static SqlServerColumnType Datetime = new SqlServerColumnType("datetime");
    public static SqlServerColumnType String = new SqlServerColumnType("nvarchar");

    public static SqlServerColumnType Custom(string literalValue)
    {
        return new SqlServerColumnType(literalValue);
    }
}

class SqlServerColumnTypeHandler : SqlMapper.TypeHandler<SqlServerColumnType>
{
    public override SqlServerColumnType Parse(object value)
    {
        return SqlServerColumnType.Parse(value.ToString() ?? "");
    }

    public override void SetValue(IDbDataParameter parameter, SqlServerColumnType? value)
    {
        parameter.Value = value?.ToString();
    }
}

class SqlServerColumnTypeConverter : JsonConverter<SqlServerColumnType>
{
    public override void WriteJson(JsonWriter writer, SqlServerColumnType? value, JsonSerializer serializer)
    {
        writer.WriteValue(value?.ToString());
    }

    public override SqlServerColumnType? ReadJson(JsonReader reader, Type objectType, SqlServerColumnType? existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        return SqlServerColumnType.Parse(reader.Value.ToString());
    }
}

class SqlServerColumnModel
{
    public required string ColumnName { get; set; }    
    
    [JsonConverter(typeof(SqlServerColumnTypeConverter))]
    public required SqlServerColumnType ColumnType { get; set; }
    
    public int? MaxLength { get; set; }
    public bool Nullable { get; set; }
}

class SqlServerIndexModel
{
    public string? IndexName { get; set; }
    public bool Unique { get; set; }
    public required IEnumerable<string> ColumnNames { get; set; }
}

class SqlServerTableModel
{
    public required string TableName { get; set; }
    public bool NoIdentity { get; set; }
    public required IEnumerable<SqlServerColumnModel> CustomColumns { get; set; }
    public required IEnumerable<SqlServerIndexModel> CustomIndexes { get; set; }
    
    public static SqlServerTableModel Empty =>
        new()
        {
            TableName = string.Empty,
            CustomColumns = [],
            CustomIndexes = [],
        };
}

[JsonConverter(typeof(StringEnumConverter))]
enum SqlServerDatabaseObjectTypes
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

class SqlServerDatabaseObjectModel
{
    public required string Name { get; set; }
    public required SqlServerDatabaseObjectTypes Type { get; set; }
    public required string Sql { get; set; }
    public required bool ExecuteOnSave { get; set; }
}

class SqlServerTenantModel
{
    public string? Server { get; set; }
    public string? Catalog { get; set; }
    public string? Schema { get; set; }
    public IEnumerable<SqlServerDatabaseObjectModel>? DatabaseObjects { get; set; }
    
    public static SqlServerTenantModel Empty => new ()
    {
        DatabaseObjects = [],
    };
}