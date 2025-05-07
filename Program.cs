using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyTts.Config;
using MyTts.Controllers;
using MyTts.Data;
using MyTts.Data.Context;
using MyTts.Data.Entities;
using MyTts.Data.Interfaces;
using MyTts.Data.Repositories;
using MyTts.Repositories;
using MyTts.Routes;
using MyTts.Services;
using MyTts.Storage;
using Polly;
using StackExchange.Redis;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline
ConfigureMiddleware(app);
ConfigureEndpoints(app);

app.Run();

static void ConfigureMiddleware(WebApplication app)
{
    // Development specific middleware
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();
    app.UseRouting();
    app.UseCors("AllowAllOrigins");
    app.UseAuthorization();
}

static void ConfigureEndpoints(WebApplication app)
{
    ApiRoutes.RegisterMp3Routes(app);
    app.MapControllers();
}

static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    services
        .AddCoreServices(configuration)
        .AddStorageServices(configuration)
        .AddElevenLabsServices(configuration)
        .AddRedisServices(configuration)
        .AddHttpClients()
        .AddApiServices();
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Database connection string is not configured");
        }

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(3);
                sqlOptions.CommandTimeout(30);
            });
        });

        // Repository pattern registrations
        services.AddScoped<IRepository<Mp3Meta, IMp3>, Mp3MetaRepository>();
        services.AddScoped<IRepository<News, INews>, NewsRepository>();
        
        // Service registrations
        services.AddScoped<IMp3Service, Mp3Service>();
        services.AddScoped<IMp3FileRepository, Mp3FileRepository>();
        services.AddScoped<NewsFeedsService>();
        
        // Controller registrations
        services.AddTransient<Mp3Controller>();
        
        // Add logging with better configuration
        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.AddDebug();
            
            // Configure log levels for your application
            logging.AddFilter("Microsoft", LogLevel.Warning);
            logging.AddFilter("System", LogLevel.Warning);
            logging.AddFilter("MyTts", LogLevel.Information);
        });

        return services;
    }

    public static IServiceCollection AddStorageServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure storage with proper validation
        services.AddOptions<StorageConfiguration>()
            .Bind(configuration.GetSection("Storage"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        // Register disk configurations 
        services.AddSingleton(sp => {
            var storageConfig = sp.GetRequiredService<IOptions<StorageConfiguration>>().Value;
            var disks = new Dictionary<string, DiskConfiguration>();
            
            foreach (var disk in storageConfig.Disks)
            {
                disks[disk.Key] = new DiskConfiguration
                {
                    Driver = disk.Value.Driver,
                    Root = disk.Value.Root,
                    Config = disk.Value.Config ?? new Dictionary<string, string>()
                };
            }
            
            return disks;
        });

        // Register TtsManager as a singleton with proper disposal
        services.AddSingleton<TtsManager>();
        services.AddHostedService<IHostedService>(sp =>
            new HostedServiceWrapper(sp.GetRequiredService<TtsManager>()));
            
        return services;
    }

    public static IServiceCollection AddElevenLabsServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register ElevenLabs configuration
        services.AddOptions<ElevenLabsConfig>()
            .Bind(configuration.GetSection("ElevenLabs"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ElevenLabsConfig>, ElevenLabsConfig>();

        // Register ElevenLabsClient with better error handling
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IOptions<ElevenLabsConfig>>().Value;
            
            // First try configuration, then environment variable
            var apiKey = config.ApiKey ??
                        Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY") ??
                        throw new InvalidOperationException("ElevenLabs API key not found");

            return new ElevenLabs.ElevenLabsClient(
                new ElevenLabs.ElevenLabsAuthentication(apiKey),
                new ElevenLabs.ElevenLabsClientSettings("api.elevenlabs.io", "v1"),
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("ElevenLabsClient")
            );
        });

        return services;
    }

    public static IServiceCollection AddRedisServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register in-memory cache for local caching
        services.AddMemoryCache(options => {
            options.SizeLimit = 1024; // Set a reasonable size limit
        });

        // Register Redis configuration
        services.AddOptions<RedisConfig>()
            .Bind(configuration.GetSection("Redis"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register ConnectionMultiplexer with connection resilience
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<RedisConfig>>().Value;
            var options = ConfigurationOptions.Parse(config.ConnectionString);
            
            // Add resilience
            options.AbortOnConnectFail = false;
            options.ConnectRetry = 3;
            options.ConnectTimeout = 5000;
            
            return ConnectionMultiplexer.Connect(options);
        });

        // Register Redis cache service
        services.AddSingleton<IRedisCacheService, RedisCacheService>();

        return services;
    }

    public static IServiceCollection AddHttpClients(this IServiceCollection services)
    {
        // Configure Firebase storage client with resilience
        services.AddHttpClient("FirebaseStorage", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.Add("User-Agent", "MyTts/1.0");
        }).AddTransientHttpErrorPolicy(builder => 
            builder.WaitAndRetryAsync(3, retryAttempt => 
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        // Configure ElevenLabs client with proper settings
        services.AddHttpClient("ElevenLabsClient", (sp, client) =>
        {
            var settings = new ElevenLabs.ElevenLabsClientSettings("api.elevenlabs.io", "v1");
            var config = sp.GetRequiredService<IOptions<ElevenLabsConfig>>().Value;
            
            var apiKey = config.ApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY");
            }
            
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("ElevenLabs API key not found in configuration or environment variables");
            }

            client.BaseAddress = new Uri(settings.BaseRequestUrlFormat.Replace("{0}", ""));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("xi-api-key", apiKey);
            client.DefaultRequestHeaders.Add("User-Agent", "MyTts");
            client.Timeout = TimeSpan.FromSeconds(60);
        }).AddTransientHttpErrorPolicy(builder => 
            builder.WaitAndRetryAsync(3, retryAttempt => 
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        // Configure feed client with resilience
        services.AddHttpClient("FeedClient", client =>
        {
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "MyTts-FeedClient");
            client.Timeout = TimeSpan.FromSeconds(30);
        }).AddTransientHttpErrorPolicy(builder => 
            builder.WaitAndRetryAsync(3, retryAttempt => 
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        return services;
    }

    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddOpenApi();
        services.AddControllers(options => {
            // Add global filters if needed
            options.Filters.Add(new Microsoft.AspNetCore.Mvc.ProducesAttribute("application/json"));
        });

        services.AddCors(options =>
        {
            options.AddPolicy("AllowAllOrigins", builder =>
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .WithHeaders("Authorization", "Content-Type", "Accept"));
        });

        return services;
    }
}