using System.Data;
using System.Diagnostics.CodeAnalysis;
using Dapper;
using Npgsql;

namespace Ballware.Generic.Tenant.Data.Postgres.Internal;

static class PostgresDbConnectionExtensions
{
    private static readonly string TenantIdColumnName = "tenant_id";
    private static readonly string TableExistsQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=@schema AND TABLE_NAME=@table";
    private static readonly string TableQuery = "SELECT TABLE_NAME AS TableName FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=@schema AND TABLE_TYPE='BASE TABLE'";
    private static readonly string CustomTypeQuery = "SELECT t.typname AS name FROM pg_catalog.pg_type t INNER JOIN pg_catalog.pg_namespace n ON t.typnamespace = n.oid WHERE n.nspname = @schema AND t.typtype = 'd'";
    private static readonly string CustomFunctionQuery = "SELECT p.proname AS name FROM pg_catalog.pg_proc p INNER JOIN pg_catalog.pg_namespace n ON p.pronamespace = n.oid WHERE n.nspname = @schema";
    private static readonly string CustomViewQuery = "SELECT viewname AS name FROM pg_catalog.pg_views WHERE schemaname = @schema";
    private static readonly string IndexQuery = "SELECT i.relname AS IndexName, idx.indisunique AS \"Unique\", string_agg(a.attname, ',') AS IndexColumns FROM pg_class t, pg_class i, pg_index idx, pg_attribute a, pg_namespace s WHERE t.oid = idx.indrelid AND i.oid = idx.indexrelid AND a.attrelid = t.oid AND a.attnum = ANY(idx.indkey) AND t.relnamespace = s.oid AND s.nspname = @schema AND t.relname = @table AND (i.relname LIKE 'idx_%' OR i.relname LIKE 'uidx_%') GROUP BY i.relname, idx.indisunique";
    private static readonly string ColumnQuery = "SELECT column_name AS ColumnName, data_type AS ColumnType, character_maximum_length AS MaxLength, CASE WHEN is_nullable='YES' THEN 1 ELSE 0 END AS Nullable FROM information_schema.columns WHERE table_schema=@schema AND table_name=@table";
    private static readonly IEnumerable<string> EntityMandatoryColumns = ["id", "uuid", TenantIdColumnName, "creator_id", "create_stamp", "last_changer_id", "last_change_stamp"];
    
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
        return db.ExecuteScalar<long>(TableExistsQuery, new { schema, table }) > 0;
    }
    
    private static void CreateTable(this IDbConnection db, string schema, PostgresTableModel table)
    {
        PostgresValidator.ValidateTableAndColumnIdentifier(schema, nameof(schema));
        PostgresValidator.ValidateTableAndColumnIdentifier(table.TableName, nameof(table.TableName));
        
        var columns = CreateMandatoryColumns(table.NoIdentity);
        
        db.Execute($"CREATE TABLE \"{schema}\".\"{table.TableName}\" ({columns})"); // NOSONAR - S2077 Validation existing
    }
    
    // DDL Anpassungen
    private static void AddColumn(this IDbConnection db, string table, PostgresColumnModel add)
    {
        PostgresValidator.ValidateTableAndColumnIdentifier(table, nameof(table));
        PostgresValidator.ValidateTableAndColumnIdentifier(add.ColumnName, nameof(add.ColumnName));
        
        db.Execute($"ALTER TABLE \"{table}\" ADD COLUMN \"{add.ColumnName}\" {CreateColumnTypeDefinition(add)}");  // NOSONAR - S2077 Validation existing
        
        if (!add.Nullable)
        {
            db.Execute($"ALTER TABLE \"{table}\" ALTER COLUMN \"{add.ColumnName}\" SET NOT NULL");  // NOSONAR - S2077 Validation existing
        }
    }
    
    private static void AlterColumn(this IDbConnection db, string table, PostgresColumnModel existing, PostgresColumnModel changed)
    {
        PostgresValidator.ValidateTableAndColumnIdentifier(table, nameof(table));
        PostgresValidator.ValidateTableAndColumnIdentifier(existing.ColumnName, nameof(existing.ColumnName));
        PostgresValidator.ValidateTableAndColumnIdentifier(changed.ColumnName, nameof(changed.ColumnName));
        
        if (existing.ColumnType != changed.ColumnType || existing.Nullable != changed.Nullable ||
            existing.MaxLength != changed.MaxLength)
        {
            db.Execute($"ALTER TABLE \"{table}\" ALTER COLUMN \"{changed.ColumnName}\" TYPE {CreateColumnTypeDefinition(changed)}");  // NOSONAR - S2077 Validation existing
            
            if (!changed.Nullable)
            {
                db.Execute($"ALTER TABLE \"{table}\" ALTER COLUMN \"{changed.ColumnName}\" SET NOT NULL");  // NOSONAR - S2077 Validation existing
            }
            else 
            {
                db.Execute($"ALTER TABLE \"{table}\" ALTER COLUMN \"{changed.ColumnName}\" DROP NOT NULL");  // NOSONAR - S2077 Validation existing
            }
        }
    }

    private static void DropColumn(this IDbConnection db, string table, PostgresColumnModel drop)
    {
        PostgresValidator.ValidateTableAndColumnIdentifier(table, nameof(table));
        PostgresValidator.ValidateTableAndColumnIdentifier(drop.ColumnName, nameof(drop.ColumnName));
        
        db.Execute($"ALTER TABLE \"{table}\" DROP COLUMN \"{drop.ColumnName}\""); // NOSONAR - S2077 Validation existing
    }
    
    public static async Task CreateSchemaForUserAsync(this IDbConnection db, string catalog, string schema, string username, string password)
    {
        PostgresValidator.ValidateSchemaName(schema, nameof(schema));
        PostgresValidator.ValidateUserName(username, nameof(username));
        
        await db.ExecuteAsync($"CREATE USER \"{username}\" WITH PASSWORD '{password}'"); // NOSONAR - S2077 Validation existing
    
        if (!"public".Equals(schema, StringComparison.OrdinalIgnoreCase))
        {
            await db.ExecuteAsync($"CREATE SCHEMA {schema} AUTHORIZATION \"{username}\""); // NOSONAR - S2077 Validation existing 
        }
    
        await db.ExecuteAsync($"GRANT ALL PRIVILEGES ON SCHEMA {schema} TO \"{username}\""); // NOSONAR - S2077 Validation existing
        await db.ExecuteAsync($"GRANT CONNECT ON DATABASE {catalog} TO \"{username}\""); // NOSONAR - S2077 Validation existing
        await db.ExecuteAsync($"ALTER USER \"{username}\" SET search_path TO {schema}"); // NOSONAR - S2077 Validation existing
    }

    public static async Task DropSchemaForUserAsync(this IDbConnection db, string catalog, string schema, string username)
    {
        PostgresValidator.ValidateSchemaName(schema, nameof(schema));
        PostgresValidator.ValidateUserName(username, nameof(username));
        
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
            
            await db.ExecuteAsync($"DROP SCHEMA IF EXISTS {schema}"); // NOSONAR - S2077 Validation existing
        }

        try
        {
            await db.ExecuteAsync(
                $"REVOKE ALL PRIVILEGES ON DATABASE {catalog} FROM \"{username}\""); // NOSONAR - S2077 Validation existing    
        }
        catch (PostgresException)
        {
            // Could possibly fail if the user is not existing
        }
        
        await db.ExecuteAsync($"DROP USER IF EXISTS \"{username}\""); // NOSONAR - S2077 Validation existing
    }
    
    private static IEnumerable<string> GetTableNames(this IDbConnection db, string schema)
    {
        return db.Query<string>(
            TableQuery, 
            new { schema });
    }

    private static IEnumerable<string> GetCustomTypeNames(this IDbConnection db, string schema)
    {
        return db.Query<string>(
            CustomTypeQuery, 
            new { schema });
    }
    
    private static IEnumerable<string> GetCustomFunctionNames(this IDbConnection db, string schema)
    {
        return db.Query<string>(
            CustomFunctionQuery, 
            new { schema });
    }
    
    private static IEnumerable<string> GetCustomViewNames(this IDbConnection db, string schema)
    {
        return db.Query<string>(
            CustomViewQuery, 
            new { schema });
    }
    
    private static IEnumerable<PostgresColumnModel> GetTableColumns(this IDbConnection db, string schema,
        string tableName)
    {
        return db.Query<PostgresColumnModel>(
            ColumnQuery, 
            new { schema, table = tableName });
    }
    
    private static IEnumerable<PostgresIndexModel> GetCustomTableIndices(this IDbConnection db, string schema, string tableName)
    {
        return db.Query<PostgresIndexModel>(
            IndexQuery,
            new { schema, table = tableName });
    }

    private static void DropIndex(this IDbConnection db, 
        [SuppressMessage("CodeQuality", "S1172:Unused method parameters should be removed", Justification = "Parameter may be used in future")] 
        string tableName, 
        string indexName)
    {
        PostgresValidator.ValidateIndexName(indexName, nameof(indexName));
        
        db.Execute($"DROP INDEX IF EXISTS {indexName}"); // NOSONAR - S2077 Validation existing
    }

    private static void CreateIndex(this IDbConnection db, string tableName, PostgresIndexModel index)
    {
        var columnNames = index.ColumnNames.ToList();
        
        if (columnNames.Count == 0)
        {
            throw new ArgumentException("Index must have at least one column.");
        }

        if (!columnNames.Contains(TenantIdColumnName))
        {
            columnNames.Insert(0, TenantIdColumnName);
        }
        
        var indexName = CreateIndexName(tableName, index);

        PostgresValidator.ValidateTableAndColumnIdentifier(tableName, nameof(tableName));
        PostgresValidator.ValidateIndexName(indexName, nameof(indexName));
        
        db.Execute($"CREATE{(index.Unique ? " UNIQUE" : "")} INDEX {indexName} ON {tableName} ({string.Join(", ", columnNames)})"); // NOSONAR - S2077 Validation existing
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

        db.CreateIndex(model.TableName, new PostgresIndexModel()
        {
            Unique = false,
            ColumnNames = [TenantIdColumnName]
        });
        
        if (!model.NoIdentity)
        {
            db.CreateIndex(model.TableName, new PostgresIndexModel()
            {
                Unique = true,
                ColumnNames = ["uuid", TenantIdColumnName]
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
        PostgresValidator.ValidateSchemaName(schema, nameof(schema));
        PostgresValidator.ValidateFunctionName(name, nameof(name));
        
        db.Execute($"DROP FUNCTION IF EXISTS {schema}.{name}"); // NOSONAR - S2077 Validation existing
    }

    public static void CreateView(this IDbConnection db, string schema, string name, string sql, bool overwrite = true)
    {
        PostgresValidator.ValidateSchemaName(schema, nameof(schema));
        PostgresValidator.ValidateViewName(name, nameof(name));
        
        if (overwrite)
        {
            db.DropView(schema, name);
        }

        db.Execute(sql);
    }

    public static void DropView(this IDbConnection db, string schema, string name)
    {
        PostgresValidator.ValidateSchemaName(schema, nameof(schema));
        PostgresValidator.ValidateViewName(name, nameof(name));
        
        db.Execute($"DROP VIEW IF EXISTS {schema}.{name}"); // NOSONAR - S2077 Validation existing
    }

    public static void CreateType(this IDbConnection db, string schema, string name, string sql, bool overwrite = true)
    {
        PostgresValidator.ValidateSchemaName(schema, nameof(schema));
        PostgresValidator.ValidateDomainName(name, nameof(name));
        
        if (overwrite)
        {
            db.DropType(schema, name);
        }

        db.Execute(sql);
    }

    public static void DropType(this IDbConnection db, string schema, string name)
    {
        PostgresValidator.ValidateSchemaName(schema, nameof(schema));
        PostgresValidator.ValidateDomainName(name, nameof(name));
        
        db.Execute($"DROP TYPE IF EXISTS {schema}.{name}"); // NOSONAR - S2077 Validation existing
    }

    public static void CreateTable(this IDbConnection db, string schema, string name, string sql, bool overwrite = true)
    {
        PostgresValidator.ValidateSchemaName(schema, nameof(schema));
        PostgresValidator.ValidateTableAndColumnIdentifier(name, nameof(name));
        
        if (overwrite)
        {
            db.DropTable(schema, name);
        }

        db.Execute(sql);
    }

    public static void DropTable(this IDbConnection db, string schema, string name)
    {
        PostgresValidator.ValidateSchemaName(schema, nameof(schema));
        PostgresValidator.ValidateTableAndColumnIdentifier(name, nameof(name));
        
        db.Execute($"DROP TABLE IF EXISTS {schema}.{name}"); // NOSONAR - S2077 Validation existing
    }
}