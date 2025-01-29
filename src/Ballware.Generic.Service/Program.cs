using Ballware.Generic.Service;
using Ballware.Generic.Service.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

var environment = builder.Environment;

builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile("appsettings.json", true, true);
builder.Configuration.AddJsonFile($"appsettings.{environment.EnvironmentName}.json", true, true);
builder.Configuration.AddJsonFile($"appsettings.local.json", true, true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<KestrelServerOptions>(builder.Configuration.GetSection("Kestrel"));

var startup = new Startup(builder.Environment, builder.Configuration, builder.Services);

try
{
    startup.InitializeServices();
}
catch (ConfigurationException ex)
{
    await Console.Error.WriteLineAsync(ex.Message);
    System.Environment.Exit(-1);
}

var app = builder.Build();

startup.InitializeApp(app);

await app.RunAsync();