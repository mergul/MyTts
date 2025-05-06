using System.Net.Http.Headers;
using ElevenLabs;
using MyTts.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyTts.Config;
using MyTts.Routes;
using MyTts.Services;
using StackExchange.Redis;
using MyTts.Data.Context;
using MyTts.Data.Interfaces;
using MyTts.Data.Entities;
using MyTts.Data.Repositories;
using MyTts.Controllers;
using MyTts.Storage;
using MyTts.Data;

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
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IRepository<Mp3Meta>, Mp3MetaRepository>();
        services.AddScoped<IRepository<News>, NewsRepository>();
        services.AddTransient<Mp3Controller>();
        services.AddScoped<IMp3Service, Mp3Service>();
        services.AddScoped<IMp3FileRepository, Mp3FileRepository>();
        services.AddScoped<NewsFeedsService>();
        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.AddDebug();
        });

        return services;
    }

    public static IServiceCollection AddStorageServices(this IServiceCollection services, IConfiguration configuration)
    {

        services.AddOptions<StorageConfiguration>()
        .Bind(configuration.GetSection("Storage"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

        // Register TtsManager as a singleton with disposal
        services.AddSingleton<TtsManager>();
        services.AddHostedService<IHostedService>(sp =>
            new HostedServiceWrapper(sp.GetRequiredService<TtsManager>()));
        return services;
    }



    public static IServiceCollection AddElevenLabsServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ElevenLabsConfig>()
            .BindConfiguration("ElevenLabs")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ElevenLabsConfig>, ElevenLabsConfig>();

        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IOptions<ElevenLabsConfig>>().Value;
            var apiKey = config.ApiKey ??
                        Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY") ??
                        throw new InvalidOperationException("ElevenLabs API key not found");

            return new ElevenLabsClient(
                new ElevenLabsAuthentication(apiKey),
                new ElevenLabsClientSettings("api.elevenlabs.io", "v1"),
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("ElevenLabsClient")
            );
        });

        return services;
    }

    public static IServiceCollection AddRedisServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();

        services.AddOptions<RedisConfig>()
            .BindConfiguration("Redis")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<RedisConfig>>().Value;
            return ConnectionMultiplexer.Connect(config.ConnectionString);
        });

        services.AddSingleton<IRedisCacheService, RedisCacheService>();

        return services;
    }

    public static IServiceCollection AddHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient("FirebaseStorage", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.Add("User-Agent", "HaberTTS/1.0");
        });

        services.AddHttpClient("ElevenLabsClient", (sp, client) =>
        {
            var settings = new ElevenLabsClientSettings("api.elevenlabs.io", "v1");
            var config = sp.GetRequiredService<IOptions<ElevenLabsConfig>>().Value;
            var apiKey = config.ApiKey ??
                        Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY") ??
                        throw new InvalidOperationException("ElevenLabs API key not found");

            client.BaseAddress = new Uri(settings.BaseRequestUrlFormat.Replace("{0}", ""));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("xi-api-key", apiKey);
            client.DefaultRequestHeaders.Add("User-Agent", "HaberTTS");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddHttpClient("FeedClient", client =>
        {
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "HaberTTS-FeedClient");
        });

        return services;
    }

    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddOpenApi();
        services.AddControllers();

        services.AddCors(options =>
        {
            options.AddPolicy("AllowAllOrigins", builder =>
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader());
        });

        return services;
    }
}
