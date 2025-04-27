using Ballware.Generic.Api;
using Ballware.Generic.Api.Endpoints;
using Ballware.Generic.Authorization;
using Ballware.Generic.Authorization.Jint;
using Ballware.Generic.Data.Ef;
using Ballware.Generic.Data.Ef.Configuration;
using Ballware.Generic.Metadata;
using Ballware.Generic.Scripting.Jint;
using Ballware.Generic.Service.Adapter;
using Ballware.Generic.Service.Configuration;
using Ballware.Generic.Service.Jobs;
using Ballware.Generic.Service.Mappings;
using Ballware.Generic.Tenant.Data;
using Ballware.Generic.Tenant.Data.SqlServer;
using Ballware.Meta.Client;
using Ballware.Meta.Service.Adapter;
using Ballware.Ml.Client;
using Ballware.Storage.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Serialization;
using Quartz;
using Quartz.AspNetCore;
using CorsOptions = Ballware.Generic.Service.Configuration.CorsOptions;
using SwaggerOptions = Ballware.Generic.Service.Configuration.SwaggerOptions;

namespace Ballware.Generic.Service;


public class Startup(IWebHostEnvironment environment, ConfigurationManager configuration, IServiceCollection services)
{
    private IWebHostEnvironment Environment { get; } = environment;
    private ConfigurationManager Configuration { get; } = configuration;
    private IServiceCollection Services { get; } = services;

    public void InitializeServices()
    {
        CorsOptions? corsOptions = Configuration.GetSection("Cors").Get<CorsOptions>();
        AuthorizationOptions? authorizationOptions =
            Configuration.GetSection("Authorization").Get<AuthorizationOptions>();
        StorageOptions? storageOptions = Configuration.GetSection("Storage").Get<StorageOptions>();
        SwaggerOptions? swaggerOptions = Configuration.GetSection("Swagger").Get<SwaggerOptions>();
        ServiceClientOptions? metaClientOptions = Configuration.GetSection("MetaClient").Get<ServiceClientOptions>();
        ServiceClientOptions? storageClientOptions = Configuration.GetSection("StorageClient").Get<ServiceClientOptions>();
        ServiceClientOptions? mlClientOptions = Configuration.GetSection("MlClient").Get<ServiceClientOptions>();
        
        var tenantMasterConnectionString = Configuration.GetConnectionString("TenantMasterConnection");

        Services.AddOptionsWithValidateOnStart<AuthorizationOptions>()
            .Bind(Configuration.GetSection("Authorization"))
            .ValidateDataAnnotations();
        
        Services.AddOptionsWithValidateOnStart<StorageOptions>()
            .Bind(Configuration.GetSection("Storage"))
            .ValidateDataAnnotations();        

        Services.AddOptionsWithValidateOnStart<SwaggerOptions>()
            .Bind(Configuration.GetSection("Swagger"))
            .ValidateDataAnnotations();
        
        Services.AddOptionsWithValidateOnStart<ServiceClientOptions>()
            .Bind(Configuration.GetSection("MetaClient"))
            .ValidateDataAnnotations();
        
        Services.AddOptionsWithValidateOnStart<ServiceClientOptions>()
            .Bind(Configuration.GetSection("StorageClient"))
            .ValidateDataAnnotations();
        
        Services.AddOptionsWithValidateOnStart<ServiceClientOptions>()
            .Bind(Configuration.GetSection("MlClient"))
            .ValidateDataAnnotations();

        if (authorizationOptions == null || storageOptions == null || string.IsNullOrEmpty(tenantMasterConnectionString))
        {
            throw new ConfigurationException("Required configuration for authorization and storage is missing");
        }

        if (metaClientOptions == null)
        {
            throw new ConfigurationException("Required configuration for metaClient is missing");
        }

        if (storageClientOptions == null)
        {
            throw new ConfigurationException("Required configuration for storageClient is missing");
        }
        
        if (mlClientOptions == null)
        {
            throw new ConfigurationException("Required configuration for mlClient is missing");
        }

        Services.AddMemoryCache();
        Services.AddDistributedMemoryCache();

        Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.Authority = authorizationOptions.Authority;
            options.Audience = authorizationOptions.Audience;
            options.RequireHttpsMetadata = authorizationOptions.RequireHttpsMetadata;
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters()
            {
                ValidIssuer = authorizationOptions.Authority
            };
        });

        Services.AddAuthorizationBuilder()
            .AddPolicy("genericApi", policy => policy.RequireAssertion(context =>
                context.User
                    .Claims
                    .Where(c => "scope" == c.Type)
                    .SelectMany(c => c.Value.Split(' '))
                    .Any(s => s.Equals(authorizationOptions.RequiredMetaScope, StringComparison.Ordinal)))
            );

        if (corsOptions != null)
        {
            Services.AddCors(options =>
            {
                options.AddDefaultPolicy(c =>
                {
                    c.WithOrigins(corsOptions.AllowedOrigins)
                        .WithMethods(corsOptions.AllowedMethods)
                        .WithHeaders(corsOptions.AllowedHeaders);
                });
            });
        }

        Services.AddHttpContextAccessor();

        Services.AddMvcCore()
            .AddJsonOptions(opts => opts.JsonSerializerOptions.PropertyNamingPolicy = null)
            .AddNewtonsoftJson(opts => opts.SerializerSettings.ContractResolver = new DefaultContractResolver())
            .AddApiExplorer();

        Services.AddControllers()
            .AddJsonOptions(opts => opts.JsonSerializerOptions.PropertyNamingPolicy = null)
            .AddNewtonsoftJson(opts => opts.SerializerSettings.ContractResolver = new DefaultContractResolver());

        Services.Configure<QuartzOptions>(Configuration.GetSection("Quartz"));
        Services.AddQuartz(q =>
        {
            q.AddJob<GenericImportJob>(GenericImportJob.Key, configurator => configurator.StoreDurably());
        });

        Services.AddQuartzServer(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        Services.AddClientCredentialsTokenManagement()
            .AddClient("meta", client =>
            {
                client.TokenEndpoint = metaClientOptions.TokenEndpoint;

                client.ClientId = metaClientOptions.ClientId;
                client.ClientSecret = metaClientOptions.ClientSecret;

                client.Scope = metaClientOptions.Scopes;
            })
            .AddClient("storage", client =>
            {
                client.TokenEndpoint = storageClientOptions.TokenEndpoint;

                client.ClientId = storageClientOptions.ClientId;
                client.ClientSecret = storageClientOptions.ClientSecret;

                client.Scope = storageClientOptions.Scopes;
            })
            .AddClient("ml", client =>
            {
                client.TokenEndpoint = mlClientOptions.TokenEndpoint;

                client.ClientId = mlClientOptions.ClientId;
                client.ClientSecret = mlClientOptions.ClientSecret;

                client.Scope = mlClientOptions.Scopes;
            });
        
        Services.AddHttpClient<BallwareMetaClient>(client =>
            {
                client.BaseAddress = new Uri(metaClientOptions.ServiceUrl);
            })
            /*
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            })
            */
            .AddClientCredentialsTokenHandler("meta");

        Services.AddHttpClient<BallwareStorageClient>(client =>
            {
                client.BaseAddress = new Uri(storageClientOptions.ServiceUrl);
            })
            .AddClientCredentialsTokenHandler("storage");
        
        Services.AddHttpClient<BallwareMlClient>(client =>
            {
                client.BaseAddress = new Uri(mlClientOptions.ServiceUrl);
            })
            .AddClientCredentialsTokenHandler("ml");
        
        Services.AddAutoMapper(config =>
        {
            config.AddBallwareTenantStorageMappings();
            config.AddProfile<MetaServiceGenericMetadataProfile>();
        });

        Services.AddScoped<IMetadataAdapter, MetaServiceMetadataAdapter>();
        Services.AddScoped<IMlAdapter, MlServiceMlAdapter>();
        Services.AddScoped<IGenericFileStorageAdapter, StorageServiceGenericFileStorageAdapter>();
        
        Services.AddBallwareGenericAuthorizationUtils(authorizationOptions.TenantClaim, authorizationOptions.UserIdClaim, authorizationOptions.RightClaim);
        Services.AddBallwareGenericJintRightsChecker();
        Services.AddBallwareJintGenericScripting();
        
        Services.AddBallwareTenantStorage(storageOptions, tenantMasterConnectionString);
        
        Services.AddBallwareTenantGenericStorage(builder =>
        {
            builder.AddSqlServerTenantDataStorage(tenantMasterConnectionString);
        });
        
        if (swaggerOptions != null)
        {
            Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("generic", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "ballware Generic API",
                    Version = "v1"
                });

                c.EnableAnnotations();

                c.AddSecurityDefinition("oidc", new OpenApiSecurityScheme
                {
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.OpenIdConnect,
                    OpenIdConnectUrl = new Uri(authorizationOptions.Authority + "/.well-known/openid-configuration")
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oidc" }
                        },
                        swaggerOptions.RequiredScopes.Split(" ")
                    }
                });
            });

            Services.AddSwaggerGenNewtonsoftSupport();
        }
    }

    public void InitializeApp(WebApplication app)
    {
        if (Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            IdentityModelEventSource.ShowPII = true;
        }

        app.UseCors();
        app.UseRouting();

        app.UseAuthorization();

        app.MapControllers();
        app.MapLookupUserDataApi("/api/lookup");
        app.MapLookupServiceDataApi("/api/lookup");
        app.MapMlModelDataApi("/api/mlmodel");
        app.MapProcessingStateDataApi("/api/processingstate");
        app.MapStatisticDataApi("/api/statistic");
        app.MapTenantServiceDataApi("/api/tenant");
        app.MapGenericDataApi("/api/generic");

        var authorizationOptions = app.Services.GetService<IOptions<AuthorizationOptions>>()?.Value;
        var swaggerOptions = app.Services.GetService<IOptions<SwaggerOptions>>()?.Value;

        if (swaggerOptions != null && authorizationOptions != null)
        {
            app.MapSwagger();

            app.UseSwagger();

            if (swaggerOptions.EnableClient)
            {
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("generic/swagger.json", "ballware Generic API");

                    c.OAuthClientId(swaggerOptions.ClientId);
                    c.OAuthClientSecret(swaggerOptions.ClientSecret);
                    c.OAuthScopes(swaggerOptions.RequiredScopes?.Split(" "));
                    c.OAuthUsePkce();
                });
            }
        }
    }
}