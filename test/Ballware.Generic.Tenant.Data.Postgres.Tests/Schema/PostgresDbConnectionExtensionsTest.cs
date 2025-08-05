using Ballware.Generic.Tenant.Data.Postgres.Internal;
using Ballware.Generic.Tenant.Data.Postgres.Tests.Utils;
using Dapper;
using Npgsql;

namespace Ballware.Generic.Tenant.Data.Postgres.Tests.Schema;

[TestFixture]
public class PostgresDbConnectionExtensionsTest : DatabaseBackedBaseTest
{
    [SetUp]
    public void Setup()
    {
        SqlMapper.AddTypeHandler(new PostgresColumnTypeHandler());
    }
    
    [Test]
    public async Task Create_tenant_schema_succeeds()
    {
        var connectionString = MasterConnectionString;
        
        await using var db = new NpgsqlConnection(connectionString);
        
        await db.OpenAsync();
        
        try
        {
            await db.DropSchemaForUserAsync("tenant", "fakeschema1", "fake1");
            Assert.DoesNotThrowAsync(async () => await db.CreateSchemaForUserAsync("tenant", "fakeschema1", "fake1", "fakepasswd#112"));
            Assert.DoesNotThrowAsync(async () => await db.DropSchemaForUserAsync("tenant", "fakeschema1", "fake1"));
        }
        finally
        {
            await db.CloseAsync();    
        }
    }

    [Test]
    public async Task Create_entity_identity_table_succeeds()
    {
        var connectionString = MasterConnectionString;
        
        await using var masterDb = new NpgsqlConnection(connectionString);
        
        await masterDb.OpenAsync();
        await masterDb.DropSchemaForUserAsync("tenant", "fakeschema2", "fake2");
        await masterDb.CreateSchemaForUserAsync("tenant", "fakeschema2", "fake2", "fakepasswd#112");
        await masterDb.CloseAsync();
        
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            
        connectionStringBuilder.Database = "tenant";
        connectionStringBuilder.Username = "fake2";
        connectionStringBuilder.Password = "fakepasswd#112";
            
        await using var tenantDb = new NpgsqlConnection(connectionStringBuilder.ToString());
        await tenantDb.OpenAsync();
        
        var tableModel = new PostgresTableModel()
        {
            TableName = "faketable",
            NoIdentity = false,
            CustomColumns = [
                new PostgresColumnModel() { ColumnName = "nullable_bool_column", ColumnType = PostgresColumnType.Bool, Nullable = true },
                new PostgresColumnModel() { ColumnName = "bool_column", ColumnType = PostgresColumnType.Bool, Nullable = false },
                new PostgresColumnModel() { ColumnName = "nullable_int_column", ColumnType = PostgresColumnType.Int, Nullable = true },
                new PostgresColumnModel() { ColumnName = "int_column", ColumnType = PostgresColumnType.Int, Nullable = false },
                new PostgresColumnModel() { ColumnName = "nullable_long_column", ColumnType = PostgresColumnType.Long, Nullable = true },
                new PostgresColumnModel() { ColumnName = "long_column", ColumnType = PostgresColumnType.Long, Nullable = false },
                new PostgresColumnModel() { ColumnName = "nullable_float_column", ColumnType = PostgresColumnType.Float, Nullable = true },
                new PostgresColumnModel() { ColumnName = "float_column", ColumnType = PostgresColumnType.Float, Nullable = false },
                new PostgresColumnModel() { ColumnName = "nullable_uuid_column", ColumnType = PostgresColumnType.Uuid, Nullable = true },
                new PostgresColumnModel() { ColumnName = "uuid_column", ColumnType = PostgresColumnType.Uuid, Nullable = false },
                new PostgresColumnModel() { ColumnName = "nullable_datetime_column", ColumnType = PostgresColumnType.Datetime, Nullable = true },
                new PostgresColumnModel() { ColumnName = "datetime_column", ColumnType = PostgresColumnType.Datetime, Nullable = false },
                new PostgresColumnModel() { ColumnName = "nullable_string_column", ColumnType = PostgresColumnType.String, Nullable = true },
                new PostgresColumnModel() { ColumnName = "string_column", ColumnType = PostgresColumnType.String, Nullable = false },
                new PostgresColumnModel() { ColumnName = "nullable_string_column_with_length", ColumnType = PostgresColumnType.String, Nullable = true, MaxLength = 100 },
                new PostgresColumnModel() { ColumnName = "string_column_with_length", ColumnType = PostgresColumnType.String, Nullable = false, MaxLength = 100 }
            ],
            CustomIndexes = [],
        };
        
        try
        {
            await tenantDb.ExecuteAsync("drop table if exists fakeschema2.faketable");
            
            Assert.DoesNotThrow(() => tenantDb.CreateOrUpdateTable("fakeschema2", tableModel));
        }
        finally
        {
            await tenantDb.CloseAsync();
            await masterDb.OpenAsync();
            await masterDb.DropSchemaForUserAsync("tenant", "fakeschema2", "fake2");
            await masterDb.CloseAsync();    
        }
    }
    
    [Test]
    public async Task Create_entity_noidentity_table_succeeds()
    {
        var connectionString = MasterConnectionString;
        
        await using var masterDb = new NpgsqlConnection(connectionString);
        
        await masterDb.OpenAsync();
        await masterDb.DropSchemaForUserAsync("tenant", "fakeschema3", "fake3");
        await masterDb.CreateSchemaForUserAsync("tenant", "fakeschema3", "fake3", "fakepasswd#112");
        await masterDb.CloseAsync();
        
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            
        connectionStringBuilder.Database = "tenant";
        connectionStringBuilder.Username = "fake3";
        connectionStringBuilder.Password = "fakepasswd#112";
            
        await using var tenantDb = new NpgsqlConnection(connectionStringBuilder.ToString());
        await tenantDb.OpenAsync();
        
        var tableModel = new PostgresTableModel()
        {
            TableName = "faketable",
            NoIdentity = true,
            CustomColumns = [
                new PostgresColumnModel() { ColumnName = "NullableBoolColumn", ColumnType = PostgresColumnType.Bool, Nullable = true },
                new PostgresColumnModel() { ColumnName = "BoolColumn", ColumnType = PostgresColumnType.Bool, Nullable = false },
                new PostgresColumnModel() { ColumnName = "NullableIntColumn", ColumnType = PostgresColumnType.Int, Nullable = true },
                new PostgresColumnModel() { ColumnName = "IntColumn", ColumnType = PostgresColumnType.Int, Nullable = false },
                new PostgresColumnModel() { ColumnName = "NullableLongColumn", ColumnType = PostgresColumnType.Long, Nullable = true },
                new PostgresColumnModel() { ColumnName = "LongColumn", ColumnType = PostgresColumnType.Long, Nullable = false },
                new PostgresColumnModel() { ColumnName = "NullableFloatColumn", ColumnType = PostgresColumnType.Float, Nullable = true },
                new PostgresColumnModel() { ColumnName = "FloatColumn", ColumnType = PostgresColumnType.Float, Nullable = false },
                new PostgresColumnModel() { ColumnName = "NullableUuidColumn", ColumnType = PostgresColumnType.Uuid, Nullable = true },
                new PostgresColumnModel() { ColumnName = "UuidColumn", ColumnType = PostgresColumnType.Uuid, Nullable = false },
                new PostgresColumnModel() { ColumnName = "NullableDatetimeColumn", ColumnType = PostgresColumnType.Datetime, Nullable = true },
                new PostgresColumnModel() { ColumnName = "DatetimeColumn", ColumnType = PostgresColumnType.Datetime, Nullable = false },
                new PostgresColumnModel() { ColumnName = "NullableStringColumn", ColumnType = PostgresColumnType.String, Nullable = true },
                new PostgresColumnModel() { ColumnName = "StringColumn", ColumnType = PostgresColumnType.String, Nullable = false },
                new PostgresColumnModel() { ColumnName = "NullableStringColumnWithLength", ColumnType = PostgresColumnType.String, Nullable = true, MaxLength = 100 },
                new PostgresColumnModel() { ColumnName = "StringColumnWithLength", ColumnType = PostgresColumnType.String, Nullable = false, MaxLength = 100 }
            ],
            CustomIndexes = [],
        };
        
        try
        {
            await tenantDb.ExecuteAsync("drop table if exists fakeschema3.faketable");
            
            Assert.DoesNotThrow(() => tenantDb.CreateOrUpdateTable("fakeschema3", tableModel));
        }
        finally
        {
            await tenantDb.CloseAsync();
            await masterDb.OpenAsync();
            await masterDb.DropSchemaForUserAsync("tenant", "fakeschema3", "fake3");
            await masterDb.CloseAsync();    
        }
    }
    
    [Test]
    public async Task Alter_entity_table_succeeds()
    {
        var connectionString = MasterConnectionString;
        
        await using var masterDb = new NpgsqlConnection(connectionString);
        
        await masterDb.OpenAsync();
        await masterDb.DropSchemaForUserAsync("tenant", "fakeschema4", "fake4");
        await masterDb.CreateSchemaForUserAsync("tenant", "fakeschema4", "fake4", "fakepasswd#112");
        await masterDb.CloseAsync();
        
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            
        connectionStringBuilder.Database = "tenant";
        connectionStringBuilder.Username = "fake4";
        connectionStringBuilder.Password = "fakepasswd#112";
            
        await using var tenantDb = new NpgsqlConnection(connectionStringBuilder.ToString());
        await tenantDb.OpenAsync();
        
        var customColumns = new List<PostgresColumnModel>([
            new PostgresColumnModel() { ColumnName = "NullableBoolColumn", ColumnType = PostgresColumnType.Bool, Nullable = true },
            new PostgresColumnModel() { ColumnName = "BoolColumn", ColumnType = PostgresColumnType.Bool, Nullable = false },
            new PostgresColumnModel() { ColumnName = "NullableIntColumn", ColumnType = PostgresColumnType.Int, Nullable = true },
            new PostgresColumnModel() { ColumnName = "IntColumn", ColumnType = PostgresColumnType.Int, Nullable = false },
            new PostgresColumnModel() { ColumnName = "NullableLongColumn", ColumnType = PostgresColumnType.Long, Nullable = true },
            new PostgresColumnModel() { ColumnName = "LongColumn", ColumnType = PostgresColumnType.Long, Nullable = false },
            new PostgresColumnModel() { ColumnName = "NullableFloatColumn", ColumnType = PostgresColumnType.Float, Nullable = true },
            new PostgresColumnModel() { ColumnName = "FloatColumn", ColumnType = PostgresColumnType.Float, Nullable = false },
            new PostgresColumnModel() { ColumnName = "NullableUuidColumn", ColumnType = PostgresColumnType.Uuid, Nullable = true },
            new PostgresColumnModel() { ColumnName = "UuidColumn", ColumnType = PostgresColumnType.Uuid, Nullable = false },
            new PostgresColumnModel() { ColumnName = "NullableDatetimeColumn", ColumnType = PostgresColumnType.Datetime, Nullable = true },
            new PostgresColumnModel() { ColumnName = "DatetimeColumn", ColumnType = PostgresColumnType.Datetime, Nullable = false },
            new PostgresColumnModel() { ColumnName = "NullableStringColumn", ColumnType = PostgresColumnType.String, Nullable = true },
            new PostgresColumnModel() { ColumnName = "StringColumn", ColumnType = PostgresColumnType.String, Nullable = false },
            new PostgresColumnModel() { ColumnName = "NullableStringColumnWithLength", ColumnType = PostgresColumnType.String, Nullable = true, MaxLength = 100 },
            new PostgresColumnModel() { ColumnName = "StringColumnWithLength", ColumnType = PostgresColumnType.String, Nullable = false, MaxLength = 100 }
        ]); 
            
        var tableModel = new PostgresTableModel()
        {
            TableName = "faketable",
            NoIdentity = false,
            CustomColumns = customColumns,
            CustomIndexes = [],
        };
        
        try
        {
            await tenantDb.ExecuteAsync("drop table if exists fakeschema4.faketable");
            
            Assert.DoesNotThrow(() => tenantDb.CreateOrUpdateTable("fakeschema4", tableModel));
            
            customColumns.First(x => x.ColumnName == "NullableBoolColumn").Nullable = false;
            customColumns.Remove(customColumns.First(x => x.ColumnName == "BoolColumn"));
            customColumns.First(x => x.ColumnName == "IntColumn").ColumnType = PostgresColumnType.Long;
            customColumns.First(x => x.ColumnName == "StringColumnWithLength").MaxLength = 200;

            tableModel.CustomColumns = customColumns;
            
            Assert.DoesNotThrow(() => tenantDb.CreateOrUpdateTable("fakeschema4", tableModel));
        }
        finally
        {
            await tenantDb.CloseAsync();
            await masterDb.OpenAsync();
            await masterDb.DropSchemaForUserAsync("tenant", "fakeschema4", "fake4");
            await masterDb.CloseAsync();    
        }
    }
    
    [Test]
    public async Task Create_entity_table_custom_index_succeeds()
    {
        var connectionString = MasterConnectionString;
        
        await using var masterDb = new NpgsqlConnection(connectionString);
        
        await masterDb.OpenAsync();
        await masterDb.DropSchemaForUserAsync("tenant", "fakeschema5", "fake5");
        await masterDb.CreateSchemaForUserAsync("tenant", "fakeschema5", "fake5", "fakepasswd#112");
        await masterDb.CloseAsync();
        
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            
        connectionStringBuilder.Database = "tenant";
        connectionStringBuilder.Username = "fake5";
        connectionStringBuilder.Password = "fakepasswd#112";
            
        await using var tenantDb = new NpgsqlConnection(connectionStringBuilder.ToString());
        await tenantDb.OpenAsync();
        
        var tableModel = new PostgresTableModel()
            {
                TableName = "faketable",
                NoIdentity = false,
                CustomColumns = [
                    new PostgresColumnModel() { ColumnName = "nullable_bool_column", ColumnType = PostgresColumnType.Bool, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "bool_column", ColumnType = PostgresColumnType.Bool, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_int_column", ColumnType = PostgresColumnType.Int, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "int_column", ColumnType = PostgresColumnType.Int, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_long_column", ColumnType = PostgresColumnType.Long, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "long_column", ColumnType = PostgresColumnType.Long, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_float_column", ColumnType = PostgresColumnType.Float, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "float_column", ColumnType = PostgresColumnType.Float, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_uuid_column", ColumnType = PostgresColumnType.Uuid, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "uuid_column", ColumnType = PostgresColumnType.Uuid, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_datetime_column", ColumnType = PostgresColumnType.Datetime, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "datetime_column", ColumnType = PostgresColumnType.Datetime, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_string_column", ColumnType = PostgresColumnType.String, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "string_column", ColumnType = PostgresColumnType.String, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_string_column_with_length", ColumnType = PostgresColumnType.String, Nullable = true, MaxLength = 100 },
                    new PostgresColumnModel() { ColumnName = "string_column_with_length", ColumnType = PostgresColumnType.String, Nullable = false, MaxLength = 100 }
                ],
                CustomIndexes = [
                    new PostgresIndexModel() { IndexName = "uidx_faketable_explicit_name", ColumnNames = ["tenant_id", "bool_column", "int_column"], Unique = true },
                    new PostgresIndexModel() { ColumnNames = ["tenant_id", "uuid_column"], Unique = true },
                    new PostgresIndexModel() { IndexName = "idx_faketable_explicit_name", ColumnNames = ["tenant_id", "uuid_column", "string_column_with_length"], Unique = false },
                    new PostgresIndexModel() { ColumnNames = ["tenant_id", "string_column_with_length"], Unique = false },
                ],
            };
        
        try
        {
            await tenantDb.ExecuteAsync("drop table if exists fakeschema5.faketable");
            
            Assert.DoesNotThrow(() => tenantDb.CreateOrUpdateTable("fakeschema5", tableModel));
        }
        finally
        {
            await tenantDb.CloseAsync();
            await masterDb.OpenAsync();
            await masterDb.DropSchemaForUserAsync("tenant", "fakeschema5", "fake5");
            await masterDb.CloseAsync();    
        }
    }
    
    [Test]
    public async Task Create_custom_view_succeeds()
    {
        var connectionString = MasterConnectionString;
        
        await using var masterDb = new NpgsqlConnection(connectionString);
        
        await masterDb.OpenAsync();
        await masterDb.DropSchemaForUserAsync("tenant", "fakeschema6", "fake6");
        await masterDb.CreateSchemaForUserAsync("tenant", "fakeschema6", "fake6", "fakepasswd#112");
        await masterDb.CloseAsync();
        
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            
        connectionStringBuilder.Database = "tenant";
        connectionStringBuilder.Username = "fake6";
        connectionStringBuilder.Password = "fakepasswd#112";
            
        await using var tenantDb = new NpgsqlConnection(connectionStringBuilder.ToString());
        await tenantDb.OpenAsync();
        
        var tableModel = new PostgresTableModel()
            {
                TableName = "faketable",
                NoIdentity = false,
                CustomColumns = [
                    new PostgresColumnModel() { ColumnName = "nullable_bool_column", ColumnType = PostgresColumnType.Bool, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "bool_column", ColumnType = PostgresColumnType.Bool, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_int_column", ColumnType = PostgresColumnType.Int, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "int_column", ColumnType = PostgresColumnType.Int, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_long_column", ColumnType = PostgresColumnType.Long, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "long_column", ColumnType = PostgresColumnType.Long, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_float_column", ColumnType = PostgresColumnType.Float, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "float_column", ColumnType = PostgresColumnType.Float, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_uuid_column", ColumnType = PostgresColumnType.Uuid, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "uuid_column", ColumnType = PostgresColumnType.Uuid, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_datetime_column", ColumnType = PostgresColumnType.Datetime, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "datetime_column", ColumnType = PostgresColumnType.Datetime, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_string_column", ColumnType = PostgresColumnType.String, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "string_column", ColumnType = PostgresColumnType.String, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_string_column_with_length", ColumnType = PostgresColumnType.String, Nullable = true, MaxLength = 100 },
                    new PostgresColumnModel() { ColumnName = "string_column_with_length", ColumnType = PostgresColumnType.String, Nullable = false, MaxLength = 100 }
                ],
                CustomIndexes = [],
            };
        
        try
        {
            await tenantDb.ExecuteAsync("drop table if exists fakeschema6.faketable");
            
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() => tenantDb.CreateOrUpdateTable("fakeschema6", tableModel));
                Assert.DoesNotThrow(() => tenantDb.CreateView("fakeschema6", "view_customfake", "create view view_customfake as select bool_column, int_column, string_column_with_length from faketable"));
                Assert.DoesNotThrow(() => tenantDb.DropView("fakeschema6", "view_customfake"));    
            });
        }
        finally
        {
            await tenantDb.CloseAsync();
            await masterDb.OpenAsync();
            await masterDb.DropSchemaForUserAsync("tenant", "fakeschema6", "fake6");
            await masterDb.CloseAsync();    
        }
    }
    
    [Test]
    public async Task Create_custom_type_succeeds()
    {
        var connectionString = MasterConnectionString;
        
        await using var masterDb = new NpgsqlConnection(connectionString);
        
        await masterDb.OpenAsync();
        await masterDb.DropSchemaForUserAsync("tenant", "fakeschema7", "fake7");
        await masterDb.CreateSchemaForUserAsync("tenant", "fakeschema7", "fake7", "fakepasswd#112");
        await masterDb.CloseAsync();
        
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            
        connectionStringBuilder.Database = "tenant";
        connectionStringBuilder.Username = "fake7";
        connectionStringBuilder.Password = "fakepasswd#112";
            
        await using var tenantDb = new NpgsqlConnection(connectionStringBuilder.ToString());
        await tenantDb.OpenAsync();
        
        try
        {
            await tenantDb.ExecuteAsync("drop table if exists fakeschema7.faketable");

            var tableModel = new PostgresTableModel()
            {
                TableName = "faketable",
                NoIdentity = false,
                CustomColumns = [
                    new PostgresColumnModel() { ColumnName = "nullable_bool_column", ColumnType = PostgresColumnType.Bool, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "bool_column", ColumnType = PostgresColumnType.Bool, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_int_column", ColumnType = PostgresColumnType.Int, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "int_column", ColumnType = PostgresColumnType.Int, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_long_column", ColumnType = PostgresColumnType.Long, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "long_column", ColumnType = PostgresColumnType.Long, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_float_column", ColumnType = PostgresColumnType.Float, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "float_column", ColumnType = PostgresColumnType.Float, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_uuid_column", ColumnType = PostgresColumnType.Uuid, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "uuid_column", ColumnType = PostgresColumnType.Uuid, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_datetime_column", ColumnType = PostgresColumnType.Datetime, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "datetime_column", ColumnType = PostgresColumnType.Datetime, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_string_column", ColumnType = PostgresColumnType.String, Nullable = true },
                    new PostgresColumnModel() { ColumnName = "string_column", ColumnType = PostgresColumnType.String, Nullable = false },
                    new PostgresColumnModel() { ColumnName = "nullable_string_column_with_length", ColumnType = PostgresColumnType.String, Nullable = true, MaxLength = 100 },
                    new PostgresColumnModel() { ColumnName = "string_column_with_length", ColumnType = PostgresColumnType.String, Nullable = false, MaxLength = 100 },
                    new PostgresColumnModel() { ColumnName = "custom_column", ColumnType = PostgresColumnType.Custom("fakeschema7.customtype"), Nullable = false }
                ],
                CustomIndexes = [],
            };
            
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() => tenantDb.CreateType("fakeschema7", "customtype", "create domain fakeschema7.customtype as varchar(20) not null"));
                Assert.DoesNotThrow(() => tenantDb.CreateOrUpdateTable("fakeschema7", tableModel));
                Assert.DoesNotThrow(() => tenantDb.DropTable("fakeschema7", "faketable"));
                Assert.DoesNotThrow(() => tenantDb.DropType("fakeschema7", "customtype"));
            });
        }
        finally
        {
            await tenantDb.CloseAsync();
            await masterDb.OpenAsync();
            await masterDb.DropSchemaForUserAsync("tenant", "fakeschema7", "fake7");
            await masterDb.CloseAsync();    
        }
    }
    
    [Test]
    public async Task Create_custom_function_succeeds()
    {
        var connectionString = MasterConnectionString;
        
        await using var masterDb = new NpgsqlConnection(connectionString);
        
        await masterDb.OpenAsync();
        await masterDb.DropSchemaForUserAsync("tenant", "fakeschema8", "fake8");
        await masterDb.CreateSchemaForUserAsync("tenant", "fakeschema8", "fake8", "fakepasswd#112");
        await masterDb.CloseAsync();
        
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            
        connectionStringBuilder.Database = "tenant";
        connectionStringBuilder.Username = "fake8";
        connectionStringBuilder.Password = "fakepasswd#112";
            
        await using var tenantDb = new NpgsqlConnection(connectionStringBuilder.ToString());
        await tenantDb.OpenAsync();
        
        try
        {
            tenantDb.CreateFunction("fakeschema8", "udf_split", "create function udf_split(concatenated text) returns table (Value text) as $$ BEGIN RETURN QUERY SELECT unnest(string_to_array(concatenated, '|')); END; $$ LANGUAGE plpgsql;");

            var splitResult = await tenantDb.QueryAsync<string>("select value from udf_split('A|B|C|D')");
            
            Assert.That(splitResult.Count(), Is.EqualTo(4));
            
            tenantDb.DropFunction("fakeschema8", "udf_split");
        }
        finally
        {
            await tenantDb.CloseAsync();
            await masterDb.OpenAsync();
            await masterDb.DropSchemaForUserAsync("tenant", "fakeschema8", "fake8");
            await masterDb.CloseAsync();    
        }
    }
    
    [Test]
    public async Task Create_custom_table_succeeds()
    {
        var connectionString = MasterConnectionString;
        
        await using var masterDb = new NpgsqlConnection(connectionString);
        
        await masterDb.OpenAsync();
        await masterDb.DropSchemaForUserAsync("tenant", "fakeschema9", "fake9");
        await masterDb.CreateSchemaForUserAsync("tenant", "fakeschema9", "fake9", "fakepasswd#112");
        await masterDb.CloseAsync();
        
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            
        connectionStringBuilder.Database = "tenant";
        connectionStringBuilder.Username = "fake9";
        connectionStringBuilder.Password = "fakepasswd#112";
            
        await using var tenantDb = new NpgsqlConnection(connectionStringBuilder.ToString());
        await tenantDb.OpenAsync();
        
        try
        {
            await tenantDb.ExecuteAsync("drop table if exists fakeschema9.customtable");
         
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() => tenantDb.CreateTable("fakeschema9", "customtable", "create table customtable (id bigserial primary key)"));
                Assert.DoesNotThrow(() => tenantDb.DropTable("fakeschema9", "customtable"));
            });
        }
        finally
        {
            await tenantDb.CloseAsync();
            await masterDb.OpenAsync();
            await masterDb.DropSchemaForUserAsync("tenant", "fakeschema9", "fake6");
            await masterDb.CloseAsync();    
        }
    }
}