using System.Diagnostics.Tracing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Ballware.Generic.Tenant.Data.SqlServer.Internal;
using Ballware.Generic.Tenant.Data.SqlServer.Tests.Utils;
using Dapper;

namespace Ballware.Generic.Tenant.Data.SqlServer.Tests.Schema;

[TestFixture]
public class SqlServerDbConnectionExtensionsTest : DatabaseBackedBaseTest
{
    [SetUp]
    public void Setup()
    {
        SqlMapper.AddTypeHandler(new SqlServerColumnTypeHandler());
    }
    
    [Test]
    public async Task Create_tenant_schema_succeeds()
    {
        var connectionString = MasterConnectionString;
        
        using var listener = new SqlClientListener();
        await using var db = new SqlConnection(connectionString);
        
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
        
        using var listener = new SqlClientListener();
        await using var masterDb = new SqlConnection(connectionString);
        
        await masterDb.OpenAsync();
        await masterDb.DropSchemaForUserAsync("tenant", "fakeschema2", "fake2");
        await masterDb.CreateSchemaForUserAsync("tenant", "fakeschema2", "fake2", "fakepasswd#112");
        await masterDb.CloseAsync();
        
        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            
        connectionStringBuilder.InitialCatalog = "tenant";
        connectionStringBuilder.UserID = "fake2";
        connectionStringBuilder.Password = "fakepasswd#112";
            
        await using var tenantDb = new SqlConnection(connectionStringBuilder.ToString());
        await tenantDb.OpenAsync();
        
        var tableModel = new SqlServerTableModel()
        {
            TableName = "faketable",
            NoIdentity = false,
            CustomColumns = [
                new SqlServerColumnModel() { ColumnName = "NullableBoolColumn", ColumnType = SqlServerColumnType.Bool, Nullable = true },
                new SqlServerColumnModel() { ColumnName = "BoolColumn", ColumnType = SqlServerColumnType.Bool, Nullable = false },
                new SqlServerColumnModel() { ColumnName = "NullableIntColumn", ColumnType = SqlServerColumnType.Int, Nullable = true },
                new SqlServerColumnModel() { ColumnName = "IntColumn", ColumnType = SqlServerColumnType.Int, Nullable = false },
                new SqlServerColumnModel() { ColumnName = "NullableLongColumn", ColumnType = SqlServerColumnType.Long, Nullable = true },
                new SqlServerColumnModel() { ColumnName = "LongColumn", ColumnType = SqlServerColumnType.Long, Nullable = false },
                new SqlServerColumnModel() { ColumnName = "NullableFloatColumn", ColumnType = SqlServerColumnType.Float, Nullable = true },
                new SqlServerColumnModel() { ColumnName = "FloatColumn", ColumnType = SqlServerColumnType.Float, Nullable = false },
                new SqlServerColumnModel() { ColumnName = "NullableUuidColumn", ColumnType = SqlServerColumnType.Uuid, Nullable = true },
                new SqlServerColumnModel() { ColumnName = "UuidColumn", ColumnType = SqlServerColumnType.Uuid, Nullable = false },
                new SqlServerColumnModel() { ColumnName = "NullableDatetimeColumn", ColumnType = SqlServerColumnType.Datetime, Nullable = true },
                new SqlServerColumnModel() { ColumnName = "DatetimeColumn", ColumnType = SqlServerColumnType.Datetime, Nullable = false },
                new SqlServerColumnModel() { ColumnName = "NullableStringColumn", ColumnType = SqlServerColumnType.String, Nullable = true },
                new SqlServerColumnModel() { ColumnName = "StringColumn", ColumnType = SqlServerColumnType.String, Nullable = false },
                new SqlServerColumnModel() { ColumnName = "NullableStringColumnWithLength", ColumnType = SqlServerColumnType.String, Nullable = true, MaxLength = 100 },
                new SqlServerColumnModel() { ColumnName = "StringColumnWithLength", ColumnType = SqlServerColumnType.String, Nullable = false, MaxLength = 100 }
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
        
        using var listener = new SqlClientListener();
        await using var masterDb = new SqlConnection(connectionString);
        
        await masterDb.OpenAsync();
        await masterDb.DropSchemaForUserAsync("tenant", "fakeschema3", "fake3");
        await masterDb.CreateSchemaForUserAsync("tenant", "fakeschema3", "fake3", "fakepasswd#112");
        await masterDb.CloseAsync();
        
        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            
        connectionStringBuilder.InitialCatalog = "tenant";
        connectionStringBuilder.UserID = "fake3";
        connectionStringBuilder.Password = "fakepasswd#112";
            
        await using var tenantDb = new SqlConnection(connectionStringBuilder.ToString());
        await tenantDb.OpenAsync();
        
        var tableModel = new SqlServerTableModel()
        {
            TableName = "faketable",
            NoIdentity = true,
            CustomColumns = [
                new SqlServerColumnModel() { ColumnName = "NullableBoolColumn", ColumnType = SqlServerColumnType.Bool, Nullable = true },
                new SqlServerColumnModel() { ColumnName = "BoolColumn", ColumnType = SqlServerColumnType.Bool, Nullable = false },
                new SqlServerColumnModel() { ColumnName = "NullableIntColumn", ColumnType = SqlServerColumnType.Int, Nullable = true },
                new SqlServerColumnModel() { ColumnName = "IntColumn", ColumnType = SqlServerColumnType.Int, Nullable = false },
                new SqlServerColumnModel() { ColumnName = "NullableLongColumn", ColumnType = SqlServerColumnType.Long, Nullable = true },
                new SqlServerColumnModel() { ColumnName = "LongColumn", ColumnType = SqlServerColumnType.Long, Nullable = false },
                new SqlServerColumnModel() { ColumnName = "NullableFloatColumn", ColumnType = SqlServerColumnType.Float, Nullable = true },
                new SqlServerColumnModel() { ColumnName = "FloatColumn", ColumnType = SqlServerColumnType.Float, Nullable = false },
                new SqlServerColumnModel() { ColumnName = "NullableUuidColumn", ColumnType = SqlServerColumnType.Uuid, Nullable = true },
                new SqlServerColumnModel() { ColumnName = "UuidColumn", ColumnType = SqlServerColumnType.Uuid, Nullable = false },
                new SqlServerColumnModel() { ColumnName = "NullableDatetimeColumn", ColumnType = SqlServerColumnType.Datetime, Nullable = true },
                new SqlServerColumnModel() { ColumnName = "DatetimeColumn", ColumnType = SqlServerColumnType.Datetime, Nullable = false },
                new SqlServerColumnModel() { ColumnName = "NullableStringColumn", ColumnType = SqlServerColumnType.String, Nullable = true },
                new SqlServerColumnModel() { ColumnName = "StringColumn", ColumnType = SqlServerColumnType.String, Nullable = false },
                new SqlServerColumnModel() { ColumnName = "NullableStringColumnWithLength", ColumnType = SqlServerColumnType.String, Nullable = true, MaxLength = 100 },
                new SqlServerColumnModel() { ColumnName = "StringColumnWithLength", ColumnType = SqlServerColumnType.String, Nullable = false, MaxLength = 100 }
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
        
        using var listener = new SqlClientListener();
        await using var masterDb = new SqlConnection(connectionString);
        
        await masterDb.OpenAsync();
        await masterDb.DropSchemaForUserAsync("tenant", "fakeschema4", "fake4");
        await masterDb.CreateSchemaForUserAsync("tenant", "fakeschema4", "fake4", "fakepasswd#112");
        await masterDb.CloseAsync();
        
        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            
        connectionStringBuilder.InitialCatalog = "tenant";
        connectionStringBuilder.UserID = "fake4";
        connectionStringBuilder.Password = "fakepasswd#112";
            
        await using var tenantDb = new SqlConnection(connectionStringBuilder.ToString());
        await tenantDb.OpenAsync();
        
        var customColumns = new List<SqlServerColumnModel>([
            new SqlServerColumnModel() { ColumnName = "NullableBoolColumn", ColumnType = SqlServerColumnType.Bool, Nullable = true },
            new SqlServerColumnModel() { ColumnName = "BoolColumn", ColumnType = SqlServerColumnType.Bool, Nullable = false },
            new SqlServerColumnModel() { ColumnName = "NullableIntColumn", ColumnType = SqlServerColumnType.Int, Nullable = true },
            new SqlServerColumnModel() { ColumnName = "IntColumn", ColumnType = SqlServerColumnType.Int, Nullable = false },
            new SqlServerColumnModel() { ColumnName = "NullableLongColumn", ColumnType = SqlServerColumnType.Long, Nullable = true },
            new SqlServerColumnModel() { ColumnName = "LongColumn", ColumnType = SqlServerColumnType.Long, Nullable = false },
            new SqlServerColumnModel() { ColumnName = "NullableFloatColumn", ColumnType = SqlServerColumnType.Float, Nullable = true },
            new SqlServerColumnModel() { ColumnName = "FloatColumn", ColumnType = SqlServerColumnType.Float, Nullable = false },
            new SqlServerColumnModel() { ColumnName = "NullableUuidColumn", ColumnType = SqlServerColumnType.Uuid, Nullable = true },
            new SqlServerColumnModel() { ColumnName = "UuidColumn", ColumnType = SqlServerColumnType.Uuid, Nullable = false },
            new SqlServerColumnModel() { ColumnName = "NullableDatetimeColumn", ColumnType = SqlServerColumnType.Datetime, Nullable = true },
            new SqlServerColumnModel() { ColumnName = "DatetimeColumn", ColumnType = SqlServerColumnType.Datetime, Nullable = false },
            new SqlServerColumnModel() { ColumnName = "NullableStringColumn", ColumnType = SqlServerColumnType.String, Nullable = true },
            new SqlServerColumnModel() { ColumnName = "StringColumn", ColumnType = SqlServerColumnType.String, Nullable = false },
            new SqlServerColumnModel() { ColumnName = "NullableStringColumnWithLength", ColumnType = SqlServerColumnType.String, Nullable = true, MaxLength = 100 },
            new SqlServerColumnModel() { ColumnName = "StringColumnWithLength", ColumnType = SqlServerColumnType.String, Nullable = false, MaxLength = 100 }
        ]); 
            
        var tableModel = new SqlServerTableModel()
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
            customColumns.First(x => x.ColumnName == "IntColumn").ColumnType = SqlServerColumnType.Long;
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
        
        using var listener = new SqlClientListener();
        await using var masterDb = new SqlConnection(connectionString);
        
        await masterDb.OpenAsync();
        await masterDb.DropSchemaForUserAsync("tenant", "fakeschema5", "fake5");
        await masterDb.CreateSchemaForUserAsync("tenant", "fakeschema5", "fake5", "fakepasswd#112");
        await masterDb.CloseAsync();
        
        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            
        connectionStringBuilder.InitialCatalog = "tenant";
        connectionStringBuilder.UserID = "fake5";
        connectionStringBuilder.Password = "fakepasswd#112";
            
        await using var tenantDb = new SqlConnection(connectionStringBuilder.ToString());
        await tenantDb.OpenAsync();
        
        var tableModel = new SqlServerTableModel()
            {
                TableName = "faketable",
                NoIdentity = false,
                CustomColumns = [
                    new SqlServerColumnModel() { ColumnName = "NullableBoolColumn", ColumnType = SqlServerColumnType.Bool, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "BoolColumn", ColumnType = SqlServerColumnType.Bool, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableIntColumn", ColumnType = SqlServerColumnType.Int, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "IntColumn", ColumnType = SqlServerColumnType.Int, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableLongColumn", ColumnType = SqlServerColumnType.Long, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "LongColumn", ColumnType = SqlServerColumnType.Long, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableFloatColumn", ColumnType = SqlServerColumnType.Float, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "FloatColumn", ColumnType = SqlServerColumnType.Float, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableUuidColumn", ColumnType = SqlServerColumnType.Uuid, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "UuidColumn", ColumnType = SqlServerColumnType.Uuid, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableDatetimeColumn", ColumnType = SqlServerColumnType.Datetime, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "DatetimeColumn", ColumnType = SqlServerColumnType.Datetime, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableStringColumn", ColumnType = SqlServerColumnType.String, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "StringColumn", ColumnType = SqlServerColumnType.String, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableStringColumnWithLength", ColumnType = SqlServerColumnType.String, Nullable = true, MaxLength = 100 },
                    new SqlServerColumnModel() { ColumnName = "StringColumnWithLength", ColumnType = SqlServerColumnType.String, Nullable = false, MaxLength = 100 }
                ],
                CustomIndexes = [
                    new SqlServerIndexModel() { IndexName = "uidx_explicit_name", ColumnNames = ["TenantId", "BoolColumn", "IntColumn"], Unique = true },
                    new SqlServerIndexModel() { ColumnNames = ["TenantId", "UuidColumn"], Unique = true },
                    new SqlServerIndexModel() { IndexName = "idx_explicit_name", ColumnNames = ["TenantId", "UuidColumn", "StringColumnWithLength"], Unique = false },
                    new SqlServerIndexModel() { ColumnNames = ["TenantId", "StringColumnWithLength"], Unique = false },
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
        
        using var listener = new SqlClientListener();
        await using var masterDb = new SqlConnection(connectionString);
        
        await masterDb.OpenAsync();
        await masterDb.DropSchemaForUserAsync("tenant", "fakeschema6", "fake6");
        await masterDb.CreateSchemaForUserAsync("tenant", "fakeschema6", "fake6", "fakepasswd#112");
        await masterDb.CloseAsync();
        
        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            
        connectionStringBuilder.InitialCatalog = "tenant";
        connectionStringBuilder.UserID = "fake6";
        connectionStringBuilder.Password = "fakepasswd#112";
            
        await using var tenantDb = new SqlConnection(connectionStringBuilder.ToString());
        await tenantDb.OpenAsync();
        
        var tableModel = new SqlServerTableModel()
            {
                TableName = "faketable",
                NoIdentity = false,
                CustomColumns = [
                    new SqlServerColumnModel() { ColumnName = "NullableBoolColumn", ColumnType = SqlServerColumnType.Bool, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "BoolColumn", ColumnType = SqlServerColumnType.Bool, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableIntColumn", ColumnType = SqlServerColumnType.Int, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "IntColumn", ColumnType = SqlServerColumnType.Int, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableLongColumn", ColumnType = SqlServerColumnType.Long, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "LongColumn", ColumnType = SqlServerColumnType.Long, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableFloatColumn", ColumnType = SqlServerColumnType.Float, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "FloatColumn", ColumnType = SqlServerColumnType.Float, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableUuidColumn", ColumnType = SqlServerColumnType.Uuid, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "UuidColumn", ColumnType = SqlServerColumnType.Uuid, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableDatetimeColumn", ColumnType = SqlServerColumnType.Datetime, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "DatetimeColumn", ColumnType = SqlServerColumnType.Datetime, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableStringColumn", ColumnType = SqlServerColumnType.String, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "StringColumn", ColumnType = SqlServerColumnType.String, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableStringColumnWithLength", ColumnType = SqlServerColumnType.String, Nullable = true, MaxLength = 100 },
                    new SqlServerColumnModel() { ColumnName = "StringColumnWithLength", ColumnType = SqlServerColumnType.String, Nullable = false, MaxLength = 100 }
                ],
                CustomIndexes = [],
            };
        
        try
        {
            await tenantDb.ExecuteAsync("drop table if exists fakeschema6.faketable");
            
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() => tenantDb.CreateOrUpdateTable("fakeschema6", tableModel));
                Assert.DoesNotThrow(() => tenantDb.CreateView("fakeschema6", "view_customfake", "create view [view_customfake] as select BoolColumn, IntColumn, StringColumnWithLength from faketable"));
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
        
        using var listener = new SqlClientListener();
        await using var masterDb = new SqlConnection(connectionString);
        
        await masterDb.OpenAsync();
        await masterDb.DropSchemaForUserAsync("tenant", "fakeschema7", "fake7");
        await masterDb.CreateSchemaForUserAsync("tenant", "fakeschema7", "fake7", "fakepasswd#112");
        await masterDb.CloseAsync();
        
        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            
        connectionStringBuilder.InitialCatalog = "tenant";
        connectionStringBuilder.UserID = "fake7";
        connectionStringBuilder.Password = "fakepasswd#112";
            
        await using var tenantDb = new SqlConnection(connectionStringBuilder.ToString());
        await tenantDb.OpenAsync();
        
        try
        {
            await tenantDb.ExecuteAsync("drop table if exists fakeschema7.faketable");

            var tableModel = new SqlServerTableModel()
            {
                TableName = "faketable",
                NoIdentity = false,
                CustomColumns = [
                    new SqlServerColumnModel() { ColumnName = "NullableBoolColumn", ColumnType = SqlServerColumnType.Bool, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "BoolColumn", ColumnType = SqlServerColumnType.Bool, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableIntColumn", ColumnType = SqlServerColumnType.Int, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "IntColumn", ColumnType = SqlServerColumnType.Int, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableLongColumn", ColumnType = SqlServerColumnType.Long, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "LongColumn", ColumnType = SqlServerColumnType.Long, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableFloatColumn", ColumnType = SqlServerColumnType.Float, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "FloatColumn", ColumnType = SqlServerColumnType.Float, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableUuidColumn", ColumnType = SqlServerColumnType.Uuid, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "UuidColumn", ColumnType = SqlServerColumnType.Uuid, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableDatetimeColumn", ColumnType = SqlServerColumnType.Datetime, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "DatetimeColumn", ColumnType = SqlServerColumnType.Datetime, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableStringColumn", ColumnType = SqlServerColumnType.String, Nullable = true },
                    new SqlServerColumnModel() { ColumnName = "StringColumn", ColumnType = SqlServerColumnType.String, Nullable = false },
                    new SqlServerColumnModel() { ColumnName = "NullableStringColumnWithLength", ColumnType = SqlServerColumnType.String, Nullable = true, MaxLength = 100 },
                    new SqlServerColumnModel() { ColumnName = "StringColumnWithLength", ColumnType = SqlServerColumnType.String, Nullable = false, MaxLength = 100 },
                    new SqlServerColumnModel() { ColumnName = "CustomColumn", ColumnType = SqlServerColumnType.Custom("[fakeschema7].[customtype]"), Nullable = false }
                ],
                CustomIndexes = [],
            };
            
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() => tenantDb.CreateType("fakeschema7", "customtype", "create type fakeschema7.customtype from nvarchar(20) not null"));
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
        
        using var listener = new SqlClientListener();
        await using var masterDb = new SqlConnection(connectionString);
        
        await masterDb.OpenAsync();
        await masterDb.DropSchemaForUserAsync("tenant", "fakeschema8", "fake8");
        await masterDb.CreateSchemaForUserAsync("tenant", "fakeschema8", "fake8", "fakepasswd#112");
        await masterDb.CloseAsync();
        
        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            
        connectionStringBuilder.InitialCatalog = "tenant";
        connectionStringBuilder.UserID = "fake8";
        connectionStringBuilder.Password = "fakepasswd#112";
            
        await using var tenantDb = new SqlConnection(connectionStringBuilder.ToString());
        await tenantDb.OpenAsync();
        
        try
        {
            tenantDb.CreateFunction("fakeschema8", "udf_split", "create function udf_split(@concatenated nvarchar(max)) returns @table table (Value nvarchar(max)) as begin insert into @table select Value from string_split(@concatenated, '|') return end");

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
        
        using var listener = new SqlClientListener();
        await using var masterDb = new SqlConnection(connectionString);
        
        await masterDb.OpenAsync();
        await masterDb.DropSchemaForUserAsync("tenant", "fakeschema9", "fake9");
        await masterDb.CreateSchemaForUserAsync("tenant", "fakeschema9", "fake9", "fakepasswd#112");
        await masterDb.CloseAsync();
        
        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            
        connectionStringBuilder.InitialCatalog = "tenant";
        connectionStringBuilder.UserID = "fake9";
        connectionStringBuilder.Password = "fakepasswd#112";
            
        await using var tenantDb = new SqlConnection(connectionStringBuilder.ToString());
        await tenantDb.OpenAsync();
        
        try
        {
            await tenantDb.ExecuteAsync("drop table if exists fakeschema9.customtable");
         
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() => tenantDb.CreateTable("fakeschema9", "customtable", "create table [customtable] (Id bigint identity primary key)"));
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