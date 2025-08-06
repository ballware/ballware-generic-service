using Ballware.Generic.Api;
using Ballware.Generic.Api.Endpoints;
using Ballware.Shared.Authorization;
using Ballware.Shared.Authorization.Jint;
using Ballware.Generic.Caching;
using Ballware.Generic.Data.Ef;
using Ballware.Generic.Data.Ef.Configuration;
using Ballware.Generic.Data.Ef.Postgres;
using Ballware.Generic.Data.Ef.SqlServer;
using Ballware.Generic.Jobs;
using Ballware.Generic.Metadata;
using Ballware.Generic.Scripting.Jint;
using Ballware.Generic.Service.Adapter;
using Ballware.Generic.Service.Configuration;
using Ballware.Generic.Service.Mappings;
using Ballware.Generic.Tenant.Data;
using Ballware.Generic.Tenant.Data.Postgres;
using Ballware.Generic.Tenant.Data.SqlServer;
using Ballware.Generic.Tenant.Data.SqlServer.Configuration;
using Ballware.Meta.Client;
using Ballware.Ml.Client;
using Ballware.Storage.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;
using Microsoft.OpenApi.Models;
using Quartz;
using CorsOptions = Ballware.Generic.Service.Configuration.CorsOptions;
using SwaggerOptions = Ballware.Generic.Service.Configuration.SwaggerOptions;
using Serilog;

namespace Ballware.Generic.Service;


public class Startup(IWebHostEnvironment environment, ConfigurationManager configuration, IServiceCollection services)
{
    private readonly string ClaimTypeScope = "scope";
    
    private IWebHostEnvironment Environment { get; } = environment;
    private ConfigurationManager Configuration { get; } = configuration;
    private IServiceCollection Services { get; } = services;

    public void InitializeServices()
    {
        CorsOptions? corsOptions = Configuration.GetSection("Cors").Get<CorsOptions>();
        AuthorizationOptions? authorizationOptions =
            Configuration.GetSection("Authorization").Get<AuthorizationOptions>();
        StorageOptions? storageOptions = Configuration.GetSection("Storage").Get<StorageOptions>();
        TenantStorageOptions? tenantStorageOptions = Configuration.GetSection("TenantStorage").Get<TenantStorageOptions>();       
        SqlServerTenantStorageOptions? sqlServerTenantStorageOptions = Configuration.GetSection("SqlServerTenantStorage").Get<SqlServerTenantStorageOptions>();
        CacheOptions? cacheOptions = Configuration.GetSection("Cache").Get<CacheOptions>();
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
        
        Services.AddOptionsWithValidateOnStart<TenantStorageOptions>()
            .Bind(Configuration.GetSection("TenantStorage"))
            .ValidateDataAnnotations();        
        
        Services.AddOptionsWithValidateOnStart<Ballware.Generic.Caching.Configuration.CacheOptions>()
            .Bind(Configuration.GetSection("Cache"))
            .ValidateDataAnnotations();
        
        Services.AddOptionsWithValidateOnStart<CacheOptions>()
            .Bind(Configuration.GetSection("Cache"))
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

        if (authorizationOptions == null || storageOptions == null || tenantStorageOptions == null || string.IsNullOrEmpty(tenantMasterConnectionString))
        {
            throw new ConfigurationException("Required configuration for authorization and storage is missing");
        }

        if ("mssql".Equals(tenantStorageOptions.Provider) && sqlServerTenantStorageOptions == null)
        {
            throw new ConfigurationException("Required configuration for SqlServerTenantStorage is missing");
        }
        
        if (cacheOptions == null)
        {
            cacheOptions = new CacheOptions();
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
        
        if (!string.IsNullOrEmpty(cacheOptions.RedisConfiguration))
        {
            Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = cacheOptions.RedisConfiguration;
                options.InstanceName = cacheOptions.RedisInstanceName;
            });
        }
        else
        {
            Services.AddDistributedMemoryCache();
        }

        Services.AddBallwareGenericDistributedCaching();

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
                    .Where(c => ClaimTypeScope == c.Type)
                    .SelectMany(c => c.Value.Split(' '))
                    .Any(s => s.Equals(authorizationOptions.RequiredMetaScope, StringComparison.Ordinal)))
            )
            .AddPolicy("metaApi", policy => policy.RequireAssertion(context =>
                    context.User
                        .Claims
                        .Where(c => ClaimTypeScope == c.Type)
                        .SelectMany(c => c.Value.Split(' '))
                        .Any(s => s.Equals(authorizationOptions.RequiredMetaScope, StringComparison.Ordinal)))
            )
            .AddPolicy("serviceApi", policy => policy.RequireAssertion(context =>
                context.User
                    .Claims
                    .Where(c => ClaimTypeScope == c.Type)
                    .SelectMany(c => c.Value.Split(' '))
                    .Any(s => s.Equals(authorizationOptions.RequiredServiceScope, StringComparison.Ordinal)))
            )
            .AddPolicy("schemaApi", policy => policy.RequireAssertion(context =>
                context.User
                    .Claims
                    .Where(c => ClaimTypeScope == c.Type)
                    .SelectMany(c => c.Value.Split(' '))
                    .Any(s => s.Equals(authorizationOptions.RequiredSchemaScope, StringComparison.Ordinal)))
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
        
        Services.Configure<JsonOptions>(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = null;
        });
        
        Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = null;
        });
        
        Services.AddHttpContextAccessor();

        Services.AddMvcCore()
            .AddApiExplorer();

        Services.AddControllers();
        
        Services.Configure<QuartzOptions>(Configuration.GetSection("Quartz"));
        Services.AddBallwareGenericBackgroundJobs();

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
#if DEBUG            
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            })
#endif                  
            .AddClientCredentialsTokenHandler("meta");

        Services.AddHttpClient<BallwareStorageClient>(client =>
            {
                client.BaseAddress = new Uri(storageClientOptions.ServiceUrl);
            })
#if DEBUG            
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            })
#endif            
            .AddClientCredentialsTokenHandler("storage");
        
        Services.AddHttpClient<BallwareMlClient>(client =>
            {
                client.BaseAddress = new Uri(mlClientOptions.ServiceUrl);
            })
#if DEBUG            
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            })
#endif                        
            .AddClientCredentialsTokenHandler("ml");
        
        Services.AddAutoMapper(config =>
        {
            config.AddBallwareTenantStorageMappings();
            config.AddProfile<MetaServiceGenericMetadataProfile>();
        });

        Services.AddScoped<IMetadataAdapter, MetaServiceMetadataAdapter>();
        Services.AddScoped<IMlAdapter, MlServiceMlAdapter>();
        Services.AddScoped<IGenericFileStorageAdapter, StorageServiceFileStorageAdapter>();
        Services.AddScoped<IJobsFileStorageAdapter, StorageServiceFileStorageAdapter>();
        
        Services.AddBallwareSharedAuthorizationUtils(authorizationOptions.TenantClaim, authorizationOptions.UserIdClaim, authorizationOptions.RightClaim);
        Services.AddBallwareSharedJintRightsChecker();
        Services.AddBallwareJintGenericScripting();

        if ("mssql".Equals(tenantStorageOptions.Provider, StringComparison.InvariantCultureIgnoreCase))
        {
            Services.AddBallwareTenantStorageForSqlServer(storageOptions, tenantMasterConnectionString);    
        } 
        else if ("postgres".Equals(tenantStorageOptions.Provider, StringComparison.InvariantCultureIgnoreCase))
        {
            Services.AddBallwareTenantStorageForPostgres(storageOptions, tenantMasterConnectionString);
        }
        
        Services.AddBallwareTenantGenericStorage(builder =>
        {
            if ("mssql".Equals(tenantStorageOptions.Provider, StringComparison.InvariantCultureIgnoreCase))
            {
                builder.AddSqlServerTenantDataStorage(tenantMasterConnectionString, sqlServerTenantStorageOptions);
            } 
            else if ("postgres".Equals(tenantStorageOptions.Provider, StringComparison.InvariantCultureIgnoreCase))
            {
                builder.AddPostgresTenantDataStorage(tenantMasterConnectionString);
            }
        });

        Services.AddEndpointsApiExplorer();
        
        if (swaggerOptions != null)
        {
            Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("generic", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "ballware Generic API",
                    Version = "v1"
                });
                
                c.SwaggerDoc("service", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "ballware Service API",
                    Version = "v1"
                });
                
                c.SwaggerDoc("schema", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "ballware Schema API",
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
        }
    }

    public void InitializeApp(WebApplication app)
    {
        if (Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            IdentityModelEventSource.ShowPII = true;
        }
        
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();
                var exception = exceptionFeature?.Error;

                if (exception != null)
                {
                    Log.Error(exception, "Unhandled exception occurred");

                    var problemDetails = new ProblemDetails
                    {
                        Type = "https://httpstatuses.com/500",
                        Title = "An unexpected error occurred.",
                        Status = StatusCodes.Status500InternalServerError,
                        Detail = app.Environment.IsDevelopment() ? exception.ToString() : null,
                        Instance = context.Request.Path
                    };

                    context.Response.StatusCode = problemDetails.Status.Value;
                    context.Response.ContentType = "application/problem+json";
                    await context.Response.WriteAsJsonAsync(problemDetails);
                }
            });
        });

        app.UseCors();
        app.UseRouting();

        app.UseAuthorization();

        app.MapLookupUserDataApi("/tenant/lookup");
        app.MapLookupServiceDataApi("/tenant/lookup");
        app.MapMlModelDataApi("/tenent/mlmodel");
        app.MapProcessingStateDataApi("/tenant/processingstate");
        app.MapStatisticDataApi("/tenant/statistic");
        app.MapTenantServiceDataApi("/tenant/tenant");
        app.MapGenericDataApi("/generic");
        
        app.MapTenantServiceSchemaApi("/api/tenant");

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
                    c.SwaggerEndpoint("service/swagger.json", "ballware Service API");
                    c.SwaggerEndpoint("schema/swagger.json", "ballware Schema API");

                    c.OAuthClientId(swaggerOptions.ClientId);
                    c.OAuthClientSecret(swaggerOptions.ClientSecret);
                    c.OAuthScopes(swaggerOptions.RequiredScopes?.Split(" "));
                    c.OAuthUsePkce();
                });
            }
        }
    }
}