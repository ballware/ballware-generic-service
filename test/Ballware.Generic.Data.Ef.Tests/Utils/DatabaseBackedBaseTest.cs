using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;

namespace Ballware.Generic.Data.Ef.Tests.Utils;

public abstract class DatabaseBackedBaseTest
{
    private MsSqlContainer? _mssqlContainer;
    
    protected WebApplicationBuilder PreparedBuilder { get; set; } = null!;
    protected string MasterConnectionString { get; set; } = null!;
    
    [OneTimeSetUp]
    public async Task MssqlSetUp()
    {
        var configurationBuilder = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"), optional: false)
            .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"appsettings.local.json"), true, true)
            .AddEnvironmentVariables();
        
        var config = configurationBuilder.Build();
        
        var password = config.GetValue<string>("TestSettings:DatabasePassword");
        
        _mssqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server")
            .WithPortBinding(1433, assignRandomHostPort: true)
            .WithEnvironment("ACCEPT_EULA", "1")
            .WithEnvironment("SA_PASSWORD", password)
            .WithWaitStrategy(Wait
                .ForUnixContainer()
                .UntilMessageIsLogged("SQL Server is now ready for client connections")
                .AddCustomWaitStrategy(new DelayWaitStrategy(TimeSpan.FromSeconds(5))))
            .Build();

        await _mssqlContainer.StartAsync();
        
        var port = _mssqlContainer.GetMappedPublicPort(1433);
        
        var connectionStringBuilder = new SqlConnectionStringBuilder();
        connectionStringBuilder.DataSource = $"localhost,{port}";
        connectionStringBuilder.InitialCatalog = "master";
        connectionStringBuilder.UserID = "sa";
        connectionStringBuilder.Password = password;
        connectionStringBuilder.TrustServerCertificate = true;
        
        await using var conn = new SqlConnection(connectionStringBuilder.ConnectionString);
        await conn.OpenAsync();

        var sql = @"CREATE DATABASE tenant;";

        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();

        connectionStringBuilder.InitialCatalog = "tenant";
        
        MasterConnectionString = connectionStringBuilder.ToString();
    }

    [OneTimeTearDown]
    public async Task MssqlTearDown()
    {
        if (_mssqlContainer != null)
        {
            await _mssqlContainer.DisposeAsync();
        }
    }

    [SetUp]
    public void SetupApplicationBuilder()
    {
        PreparedBuilder = WebApplication.CreateBuilder();

        PreparedBuilder.Configuration.Sources.Clear();
        PreparedBuilder.Configuration.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"), optional: false);
        PreparedBuilder.Configuration.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"appsetting.{PreparedBuilder.Environment.EnvironmentName}.json"), true, true);
        PreparedBuilder.Configuration.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"appsettings.local.json"), true, true);
        PreparedBuilder.Configuration.AddEnvironmentVariables();
    }
}