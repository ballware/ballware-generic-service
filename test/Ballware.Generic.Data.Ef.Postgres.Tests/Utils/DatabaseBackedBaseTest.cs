using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Ballware.Generic.Data.Ef.Postgres.Tests.Utils;

public abstract class DatabaseBackedBaseTest
{
    private PostgreSqlContainer? _postgresContainer;

    protected virtual string AdditionalSettingsFile { get; } = "appsettings.additional.json";
    
    protected WebApplicationBuilder PreparedBuilder { get; set; } = null!;
    protected string MasterConnectionString { get; set; } = null!;
    
    [OneTimeSetUp]
    public async Task MssqlSetUp()
    {
        var configurationBuilder = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"), optional: false)
            .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AdditionalSettingsFile), optional: true)
            .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"appsettings.local.json"), true, true)
            .AddEnvironmentVariables();
        
        var config = configurationBuilder.Build();
        
        var password = config.GetValue<string>("TestSettings:DatabasePassword");
        
        _postgresContainer = new PostgreSqlBuilder()
            .WithDatabase("tenant")
            .WithUsername("postgres")
            .WithPassword(password)
            .Build();

        await _postgresContainer.StartAsync();
        
        MasterConnectionString = _postgresContainer.GetConnectionString();
    }

    [OneTimeTearDown]
    public async Task MssqlTearDown()
    {
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    [SetUp]
    public void SetupApplicationBuilder()
    {
        PreparedBuilder = WebApplication.CreateBuilder();

        PreparedBuilder.Configuration.Sources.Clear();
        PreparedBuilder.Configuration.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"), optional: false);
        PreparedBuilder.Configuration.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"appsetting.{PreparedBuilder.Environment.EnvironmentName}.json"), true, true);
        PreparedBuilder.Configuration.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AdditionalSettingsFile), optional: true);
        PreparedBuilder.Configuration.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"appsettings.local.json"), true, true);
        PreparedBuilder.Configuration.AddEnvironmentVariables();
    }
}