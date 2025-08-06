using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Dapper;
using Npgsql;

namespace Ballware.Generic.Tenant.Data.Postgres.Internal;

static class PostgresDbConnectionExtensions
{
    private static readonly string TableExistsQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=@schema AND TABLE_NAME=@table";
    private static readonly string TableQuery = "SELECT TABLE_NAME AS TableName FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=@schema AND TABLE_TYPE='BASE TABLE'";
    private static readonly string CustomTypeQuery = "SELECT t.typname AS name FROM pg_catalog.pg_type t INNER JOIN pg_catalog.pg_namespace n ON t.typnamespace = n.oid WHERE n.nspname = @schema AND t.typtype = 'd'";
    private static readonly string CustomFunctionQuery = "SELECT p.proname AS name FROM pg_catalog.pg_proc p INNER JOIN pg_catalog.pg_namespace n ON p.pronamespace = n.oid WHERE n.nspname = @schema";
    private static readonly string CustomViewQuery = "SELECT viewname AS name FROM pg_catalog.pg_views WHERE schemaname = @schema";
    private static readonly string IndexQuery = "SELECT i.relname AS IndexName, idx.indisunique AS \"Unique\", string_agg(a.attname, ',') AS IndexColumns FROM pg_class t, pg_class i, pg_index idx, pg_attribute a, pg_namespace s WHERE t.oid = idx.indrelid AND i.oid = idx.indexrelid AND a.attrelid = t.oid AND a.attnum = ANY(idx.indkey) AND t.relnamespace = s.oid AND s.nspname = @schema AND t.relname = @table AND (i.relname LIKE 'idx_%' OR i.relname LIKE 'uidx_%') GROUP BY i.relname, idx.indisunique";
    private static readonly string ColumnQuery = "SELECT column_name AS ColumnName, data_type AS ColumnType, character_maximum_length AS MaxLength, CASE WHEN is_nullable='YES' THEN 1 ELSE 0 END AS Nullable FROM information_schema.columns WHERE table_schema=@schema AND table_name=@table";
    private static readonly IEnumerable<string> EntityMandatoryColumns = ["id", "uuid", "tenant_id", "creator_id", "create_stamp", "last_changer_id", "last_change_stamp"];

    private static readonly Regex ValidIdentifierRegex = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    public static void ValidateIdentifier(string identifier, string paramName)
    {
        if (string.IsNullOrEmpty(identifier) || !ValidIdentifierRegex.IsMatch(identifier))
        {
            throw new ArgumentException($"PostgreSQL identifier contains invalid characters or is empty: {identifier}", paramName);
        }
    }
    
    private static string CreateColumnTypeDefinition(PostgresColumnModel column)
    {
        if (column.ColumnType == PostgresColumnType.String)
        {
            if (column.MaxLength != null && column.MaxLength != -1)
            {
                return column.ColumnType + $"({column.MaxLength})";
            }
            
            return column.ColumnType + $"(4000)";
        }
        
        return column.ColumnType.ToString();
    }

    private static string CreateIndexName(string tableName, PostgresIndexModel index)
    {
        if (!string.IsNullOrEmpty(index.IndexName))
        {
            return index.IndexName;
        }

        return $"{(index.Unique ? "uidx" : "idx")}_{tableName}_{string.Join("_", index.ColumnNames).ToLowerInvariant()}";
    }

    // Identity Spalten anpassen
    private static string CreateMandatoryColumns(bool noIdentity)
    {
        var columnList = new List<string>();
    
        columnList.Add($"id bigserial primary key");
    
        if (!noIdentity)
        {
            columnList.Add($"uuid uuid not null");
        }
    
        columnList.Add($"tenant_id uuid not null");
        columnList.Add($"creator_id uuid");
        columnList.Add($"create_stamp timestamp");
        columnList.Add($"last_changer_id uuid");
        columnList.Add($"last_change_stamp timestamp");

        return string.Join(", ", columnList);
    }

    private static bool TableExists(this IDbConnection db, string schema, string table)
    {
        return db.ExecuteScalar<long>(TableExistsQuery, new { schema = schema, table = table }) > 0;
    }
    
    private static void CreateTable(this IDbConnection db, string schema, PostgresTableModel table)
    {
        ValidateIdentifier(schema, nameof(schema));
        ValidateIdentifier(table.TableName, nameof(table.TableName));
        
        var columns = CreateMandatoryColumns(table.NoIdentity);
        
        // @SuppressWarnings("squid:S2077")
        db.Execute($"CREATE TABLE \"{schema}\".\"{table.TableName}\" ({columns})");

        if (!table.NoIdentity)
        {
            db.CreateIndex(table.TableName, new PostgresIndexModel()
            {
                Unique = true,
                ColumnNames = ["uuid", "tenant_id"],
            });
        }
    }
    
    // DDL Anpassungen
    private static void AddColumn(this IDbConnection db, string table, PostgresColumnModel add)
    {
        ValidateIdentifier(table, nameof(table));
        ValidateIdentifier(add.ColumnName, nameof(add.ColumnName));
        
        // @SuppressWarnings("squid:S2077")
        db.Execute($"ALTER TABLE \"{table}\" ADD COLUMN \"{add.ColumnName}\" {CreateColumnTypeDefinition(add)}");
        
        if (!add.Nullable)
        {
            // @SuppressWarnings("squid:S2077")
            db.Execute($"ALTER TABLE \"{table}\" ALTER COLUMN \"{add.ColumnName}\" SET NOT NULL");
        }
    }
    
    private static void AlterColumn(this IDbConnection db, string table, PostgresColumnModel existing, PostgresColumnModel changed)
    {
        ValidateIdentifier(table, nameof(table));
        ValidateIdentifier(existing.ColumnName, nameof(existing.ColumnName));
        ValidateIdentifier(changed.ColumnName, nameof(changed.ColumnName));
        
        if (existing.ColumnType != changed.ColumnType || existing.Nullable != changed.Nullable ||
            existing.MaxLength != changed.MaxLength)
        {
            // @SuppressWarnings("squid:S2077")
            db.Execute($"ALTER TABLE \"{table}\" ALTER COLUMN \"{changed.ColumnName}\" TYPE {CreateColumnTypeDefinition(changed)}");
            
            if (!changed.Nullable)
            {
                // @SuppressWarnings("squid:S2077")
                db.Execute($"ALTER TABLE \"{table}\" ALTER COLUMN \"{changed.ColumnName}\" SET NOT NULL");
            }
            else 
            {
                // @SuppressWarnings("squid:S2077")
                db.Execute($"ALTER TABLE \"{table}\" ALTER COLUMN \"{changed.ColumnName}\" DROP NOT NULL");
            }
        }
    }

    private static void DropColumn(this IDbConnection db, string table, PostgresColumnModel drop)
    {
        ValidateIdentifier(table, nameof(table));
        ValidateIdentifier(drop.ColumnName, nameof(drop.ColumnName));
        
        // @SuppressWarnings("squid:S2077")
        db.Execute($"ALTER TABLE \"{table}\" DROP COLUMN \"{drop.ColumnName}\"");
    }
    
    // Schema/User-Verwaltung anpassen
    public static async Task CreateSchemaForUserAsync(this IDbConnection db, string catalog, string schema, string username, string password)
    {
        await db.ExecuteAsync($"CREATE USER \"{username}\" WITH PASSWORD '{password}'");
    
        if (!"public".Equals(schema, StringComparison.OrdinalIgnoreCase))
        {
            await db.ExecuteAsync($"CREATE SCHEMA {schema} AUTHORIZATION \"{username}\"");
        }
    
        await db.ExecuteAsync($"GRANT ALL PRIVILEGES ON SCHEMA {schema} TO \"{username}\"");
        await db.ExecuteAsync($"ALTER USER \"{username}\" SET search_path TO {schema}");
    }

    public static async Task DropSchemaForUserAsync(this IDbConnection db, string catalog, string schema, string username)
    {
        if (!"public".Equals(schema, StringComparison.OrdinalIgnoreCase))
        {   
            var existingViews = db.GetCustomViewNames(schema);
            
            foreach (var existingView in existingViews)
            {
                db.DropView(schema, existingView);
            }
            
            var existingTables = db.GetTableNames(schema);
            
            foreach (var existingTable in existingTables)
            {
                db.DropTable(schema, existingTable);
            }
            
            var existingFunctions = db.GetCustomFunctionNames(schema);
            
            foreach (var existingFunction in existingFunctions)
            {
                db.DropFunction(schema, existingFunction);
            }

            var existingTypes = db.GetCustomTypeNames(schema);
            
            foreach (var existingType in existingTypes)
            {
                db.DropType(schema, existingType);
            }
            
            await db.ExecuteAsync($"DROP SCHEMA IF EXISTS {schema}");
        }

        await db.ExecuteAsync($"DROP USER IF EXISTS \"{username}\"");
    }
    
    private static IEnumerable<string> GetTableNames(this IDbConnection db, string schema)
    {
        return db.Query<string>(
            TableQuery, 
            new { schema = schema });
    }

    private static IEnumerable<string> GetCustomTypeNames(this IDbConnection db, string schema)
    {
        return db.Query<string>(
            CustomTypeQuery, 
            new { schema = schema });
    }
    
    private static IEnumerable<string> GetCustomFunctionNames(this IDbConnection db, string schema)
    {
        return db.Query<string>(
            CustomFunctionQuery, 
            new { schema = schema });
    }
    
    private static IEnumerable<string> GetCustomViewNames(this IDbConnection db, string schema)
    {
        return db.Query<string>(
            CustomViewQuery, 
            new { schema = schema });
    }
    
    private static IEnumerable<PostgresColumnModel> GetTableColumns(this IDbConnection db, string schema,
        string tableName)
    {
        return db.Query<PostgresColumnModel>(
            ColumnQuery, 
            new { schema = schema, table = tableName });
    }
    
    private static IEnumerable<PostgresIndexModel> GetCustomTableIndices(this IDbConnection db, string schema, string tableName)
    {
        return db.Query<PostgresIndexModel>(
            IndexQuery,
            new { schema = schema, table = tableName });
    }

    private static void DropIndex(this IDbConnection db, 
        [SuppressMessage("CodeQuality", "S1172:Unused method parameters should be removed", Justification = "Parameter may be used in future")] 
        string tableName, 
        string indexName)
    {
        db.Execute($"DROP INDEX IF EXISTS {indexName}");
    }

    private static void CreateIndex(this IDbConnection db, string tableName, PostgresIndexModel index)
    {
        var indexName = CreateIndexName(tableName, index);

        db.Execute(
            $"CREATE{(index.Unique ? " UNIQUE" : "")} INDEX {indexName} ON {tableName} ({string.Join(", ", index.ColumnNames)})");
    }
    
    public static void CreateOrUpdateTable(this IDbConnection db, string schema, PostgresTableModel model)
    {
        if (!db.TableExists(schema, model.TableName))
        {
            db.CreateTable(schema, model);
        }
        
        var existingIndices = db.GetCustomTableIndices(schema, model.TableName);

        foreach (var existingIndex in existingIndices)
        {
            db.DropIndex(model.TableName, CreateIndexName(model.TableName, existingIndex));
        }

        var existingColumns = db.GetTableColumns(schema, model.TableName).ToList();
        
        var addedColumns = model.CustomColumns
            .Where(c => existingColumns.FirstOrDefault(x => x.ColumnName.Equals(c.ColumnName, StringComparison.OrdinalIgnoreCase)) == null && !EntityMandatoryColumns.Contains(c.ColumnName)).ToList();
        var removedColumns = existingColumns
            .Where(t => !EntityMandatoryColumns.Contains(t.ColumnName) && model.CustomColumns.FirstOrDefault(c => c.ColumnName.Equals(t.ColumnName, StringComparison.OrdinalIgnoreCase)) == null).ToList();
        var existingNonMandatoryColumns = model.CustomColumns
            .Where(c => existingColumns.FirstOrDefault(x => x.ColumnName.Equals(c.ColumnName, StringComparison.OrdinalIgnoreCase)) != null
                        && !EntityMandatoryColumns.Contains(c.ColumnName)).ToList();

        addedColumns.ForEach(field => db.AddColumn(model.TableName, field));
        removedColumns.ForEach(field => db.DropColumn(model.TableName, field));
        existingNonMandatoryColumns.ForEach(field => db.AlterColumn(model.TableName, existingColumns.First(x => x.ColumnName.Equals(field.ColumnName, StringComparison.OrdinalIgnoreCase)), field));

        if (!model.NoIdentity)
        {
            db.CreateIndex(model.TableName, new PostgresIndexModel()
            {
                Unique = true,
                ColumnNames = ["uuid", "tenant_id"]
            });
        }
        
        foreach (var sqlServerIndexModel in model.CustomIndexes)
        {
            db.CreateIndex(model.TableName, sqlServerIndexModel);
        }
    }

    public static void CreateFunction(this IDbConnection db, string schema, string name, string sql, bool overwrite = true)
    {
        if (overwrite)
        {
            try
            {
                db.DropFunction(schema, name);
            }
            catch (PostgresException)
            {
                // For postgres the drop could fail if function is referenced,
                // then Execute will replace function via create or replace logic
            }
        }

        db.Execute(sql);
    }

    public static void DropFunction(this IDbConnection db, string schema, string name)
    {
        db.Execute($"DROP FUNCTION IF EXISTS {schema}.{name}");
    }

    public static void CreateView(this IDbConnection db, string schema, string name, string sql, bool overwrite = true)
    {
        if (overwrite)
        {
            db.DropView(schema, name);
        }

        db.Execute(sql);
    }

    public static void DropView(this IDbConnection db, string schema, string name)
    {
        db.Execute($"DROP VIEW IF EXISTS {schema}.{name}");
    }

    public static void CreateType(this IDbConnection db, string schema, string name, string sql, bool overwrite = true)
    {
        if (overwrite)
        {
            db.DropType(schema, name);
        }

        db.Execute(sql);
    }

    public static void DropType(this IDbConnection db, string schema, string name)
    {
        db.Execute($"DROP TYPE IF EXISTS {schema}.{name}");
    }

    public static void CreateTable(this IDbConnection db, string schema, string name, string sql, bool overwrite = true)
    {
        if (overwrite)
        {
            db.DropTable(schema, name);
        }

        db.Execute(sql);
    }

    public static void DropTable(this IDbConnection db, string schema, string name)
    {
        db.Execute($"DROP TABLE IF EXISTS {schema}.{name}");
    }
}