using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

static class SqlServerDbConnectionExtensions
{
    private static readonly string TenantIdColumnName = "TenantId";
    private static readonly string TableExistsQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=@schema AND TABLE_NAME=@table";
    private static readonly string TableQuery = "SELECT TABLE_NAME AS TableName FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=@schema AND TABLE_TYPE='BASE TABLE'";
    private static readonly string CustomTypeQuery = "SELECT NAME FROM SYS.TYPES WHERE SCHEMA_ID = SCHEMA_ID(@schema) AND IS_USER_DEFINED=1";
    private static readonly string CustomFunctionQuery = "SELECT NAME FROM SYS.OBJECTS WHERE SCHEMA_ID = SCHEMA_ID(@schema) AND TYPE IN ('FN', 'TF', 'IF')";
    private static readonly string CustomViewQuery = "SELECT NAME FROM SYS.VIEWS WHERE SCHEMA_ID = SCHEMA_ID(@schema)";
    private static readonly string IndexQuery = "select IDX.name as IndexName, IDX.is_unique AS [Unique], IndexColumns = (select STRING_AGG(COL.Name, ',') from sys.index_columns IC inner join sys.columns COL on IC.object_id = COL.object_id and IC.column_id = COL.column_id where IDX.object_id = IC.object_id and IDX.index_id = IC.index_id) FROM sys.tables AS TBL\nINNER JOIN sys.schemas AS SCH ON TBL.schema_id = SCH.schema_id INNER JOIN sys.indexes AS IDX ON TBL.object_id = IDX.object_id WHERE (IDX.name like 'idx[_]%' or IDX.name like 'uidx[_]%') and SCH.name = @schema and TBL.name = @table";
    private static readonly string ColumnQuery = "select COLUMN_NAME as ColumnName, DATA_TYPE as ColumnType, CHARACTER_MAXIMUM_LENGTH as MaxLength, case when IS_NULLABLE='YES' then 1 else 0 end as Nullable from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA=@schema and TABLE_NAME=@table";
    private static readonly IEnumerable<string> EntityMandatoryColumns = ["Id", "Uuid", TenantIdColumnName, "CreatorId", "CreateStamp", "LastChangerId", "LastChangeStamp"];
    
    private static string CreateColumnTypeDefinition(SqlServerColumnModel column)
    {
        if (column.ColumnType == SqlServerColumnType.String)
        {
            if (column.MaxLength != null && column.MaxLength != -1)
            {
                return column.ColumnType + $"({column.MaxLength})";
            }
            
            return column.ColumnType + $"(4000)" + (column.Nullable ? " NULL" : " NOT NULL");
        }
        
        return column.ColumnType + (column.Nullable ? " NULL" : " NOT NULL");
    }

    private static string CreateIndexName(SqlServerIndexModel index)
    {
        if (!string.IsNullOrEmpty(index.IndexName))
        {
            return index.IndexName;
        }

        return $"{(index.Unique ? "uidx" : "idx")}_{string.Join("_", index.ColumnNames).ToLowerInvariant()}";
    }

    private static string CreateMandatoryColumns(bool noIdentity)
    {
        var columnList = new List<string>();
        
        columnList.Add($"Id bigint identity primary key");
        
        if (!noIdentity)
        {
            columnList.Add($"Uuid uniqueidentifier not null");
        }
        
        columnList.Add($"TenantId uniqueidentifier not null");
        columnList.Add($"CreatorId uniqueidentifier");
        columnList.Add($"CreateStamp datetime");
        columnList.Add($"LastChangerId uniqueidentifier");
        columnList.Add($"LastChangeStamp datetime");

        return string.Join(", ", columnList);
    }

    private static bool TableExists(this IDbConnection db, string schema, string table)
    {
        return db.ExecuteScalar<long>(TableExistsQuery, new { schema = schema, table = table }) > 0;
    }
    
    private static void CreateTable(this IDbConnection db, string schema, SqlServerTableModel table)
    {
        var columns = CreateMandatoryColumns(table.NoIdentity);
        
        db.Execute($"CREATE TABLE {schema}.{table.TableName} ({columns})");
    }
    
    private static void AddColumn(this IDbConnection db, string table, SqlServerColumnModel add)
    {
        db.Execute($"ALTER TABLE {table} ADD [{add.ColumnName}] {CreateColumnTypeDefinition(add)}");
    }
    
    private static void AlterColumn(this IDbConnection db, string table, SqlServerColumnModel existing, SqlServerColumnModel changed)
    {
        if (existing.ColumnType != changed.ColumnType || existing.Nullable != changed.Nullable ||
            existing.MaxLength != changed.MaxLength)
        {
            db.Execute($"ALTER TABLE {table} ALTER COLUMN [{changed.ColumnName}] {CreateColumnTypeDefinition(changed)}");    
        }
    }

    private static void DropColumn(this IDbConnection db, string table, SqlServerColumnModel drop)
    {
        db.Execute($"ALTER TABLE {table} DROP COLUMN [{drop.ColumnName}]");
    }
    
    public static async Task CreateContainedSchemaForUserAsync(this IDbConnection db, string catalog, string schema, string username, string password)
    {
        await db.ExecuteAsync($"use {catalog}");
        await db.ExecuteAsync($"CREATE USER [{username}] WITH PASSWORD = '{password}'");

        await db.ExecuteAsync($"GRANT CREATE TABLE TO [{username}]");
        await db.ExecuteAsync($"GRANT CREATE FUNCTION TO [{username}]");
        await db.ExecuteAsync($"GRANT CREATE VIEW TO [{username}]");
        await db.ExecuteAsync($"GRANT CREATE TYPE TO [{username}]");

        if (!"dbo".Equals(schema, StringComparison.OrdinalIgnoreCase))
        {
            await db.ExecuteAsync($"CREATE SCHEMA {schema} AUTHORIZATION [{username}]");
            await db.ExecuteAsync($"ALTER USER [{username}] WITH DEFAULT_SCHEMA=[{schema}]");
        }
        
        await db.ExecuteAsync(
            $"GRANT ALTER, SELECT, INSERT, UPDATE, DELETE, REFERENCES ON SCHEMA :: [{schema}] TO [{username}]");
    }

    public static async Task CreateSchemaForUserAsync(this IDbConnection db, string catalog, string schema, string username,
        string password)
    {
        await db.ExecuteAsync($"use master");
        await db.ExecuteAsync($"CREATE LOGIN [{username}] WITH PASSWORD = '{password}'");

        await db.ExecuteAsync($"use {catalog}");
        await db.ExecuteAsync($"CREATE USER [{username}] FOR LOGIN [{username}]");

        await db.ExecuteAsync($"GRANT CREATE TABLE TO [{username}]");
        await db.ExecuteAsync($"GRANT CREATE FUNCTION TO [{username}]");
        await db.ExecuteAsync($"GRANT CREATE VIEW TO [{username}]");
        await db.ExecuteAsync($"GRANT CREATE TYPE TO [{username}]");

        if (!"dbo".Equals(schema, StringComparison.OrdinalIgnoreCase))
        {
            await db.ExecuteAsync($"CREATE SCHEMA [{schema}]");
            await db.ExecuteAsync($"ALTER USER [{username}] WITH DEFAULT_SCHEMA=[{schema}]");
        }
        
        await db.ExecuteAsync(
            $"GRANT ALTER, SELECT, INSERT, UPDATE, DELETE, REFERENCES ON SCHEMA :: [{schema}] TO [{username}]");
    }

    public static async Task DropSchemaForUserAsync(this IDbConnection db, string catalog, string schema, string username)
    {
        await db.ExecuteAsync($"use {catalog}");
        
        if (!"dbo".Equals(schema, StringComparison.OrdinalIgnoreCase))
        {   
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
            
            var existingViews = db.GetCustomViewNames(schema);
            
            foreach (var existingView in existingViews)
            {
                db.DropView(schema, existingView);
            }
            
            await db.ExecuteAsync($"DROP SCHEMA IF EXISTS [{schema}]");
        }

        await db.ExecuteAsync($"DROP USER IF EXISTS [{username}]");

        try
        {
            await db.ExecuteAsync($"use master");
            await db.ExecuteAsync($"DROP LOGIN [{username}]");
        }
        catch (SqlException)
        {
            // Ignore if the login does not exist
        }
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
    
    private static IEnumerable<SqlServerColumnModel> GetTableColumns(this IDbConnection db, string schema,
        string tableName)
    {
        return db.Query<SqlServerColumnModel>(
            ColumnQuery, 
            new { schema = schema, table = tableName });
    }
    
    private static IEnumerable<SqlServerIndexModel> GetCustomTableIndices(this IDbConnection db, string schema, string tableName)
    {
        return db.Query<SqlServerIndexModel>(
            IndexQuery,
            new { schema = schema, table = tableName });
    }

    private static void DropIndex(this IDbConnection db, string tableName, string indexName)
    {
        db.Execute($"DROP INDEX IF EXISTS {indexName} ON {tableName}");
    }

    private static void CreateIndex(this IDbConnection db, string tableName, SqlServerIndexModel index)
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
        
        var indexName = CreateIndexName(index);

        db.Execute(
            $"CREATE{(index.Unique ? " UNIQUE" : "")} INDEX {indexName} ON {tableName} ({string.Join(", ", columnNames)})");
    }
    
    public static void CreateOrUpdateTable(this IDbConnection db, string schema, SqlServerTableModel model)
    {
        if (!db.TableExists(schema, model.TableName))
        {
            db.CreateTable(schema, model);
        }
        
        var existingIndices = db.GetCustomTableIndices(schema, model.TableName);

        foreach (var existingIndex in existingIndices)
        {
            db.DropIndex(model.TableName, CreateIndexName(existingIndex));
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

        db.CreateIndex(model.TableName, new SqlServerIndexModel()
        {
            Unique = false,
            ColumnNames = [TenantIdColumnName]
        });
        
        if (!model.NoIdentity)
        {
            db.CreateIndex(model.TableName, new SqlServerIndexModel()
            {
                Unique = true,
                ColumnNames = ["Uuid", TenantIdColumnName]
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
            db.DropFunction(schema, name);
        }

        db.Execute(sql);
    }

    public static void DropFunction(this IDbConnection db, string schema, string name)
    {
        db.Execute($"DROP FUNCTION IF EXISTS [{schema}].[{name}]");
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
        db.Execute($"DROP VIEW IF EXISTS [{schema}].[{name}]");
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
        db.Execute($"DROP TYPE IF EXISTS [{schema}].[{name}]");
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
        db.Execute($"DROP TABLE IF EXISTS [{schema}].[{name}]");
    }
}