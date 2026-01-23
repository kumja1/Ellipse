using DotNetEnv;
using Ellipse.Server.Policies;
using Ellipse.Server.Services;
using Ellipse.Server.Utils.Clients.Mapping;
using Ellipse.Server.Utils.Clients.Mapping.Geocoding;
using Ellipse.Common.Utils.Logging;
using Microsoft.Extensions.Http;
using Npgsql;
using Osrm.HttpApiClient;
using Serilog;

namespace Ellipse.Server;

public static class Program
{
    public static void Main(string[] args)
    {
        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            builder.Host.UseSerilog((_, config) => config.Enrich.With<CallerEnricher>().WriteTo.Console()
            );
            
            ConfigureServices(builder);

            WebApplication app = builder.Build();
            app.UseCors("DynamicCorsPolicy");
            app.UseRouting();
            app.UseOutputCache();
            app.MapControllers();
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occured");
            throw;
        }
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddRouting();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(
                "DynamicCorsPolicy",
                policy =>
                    policy.SetIsOriginAllowed(origin => true).AllowAnyHeader().AllowAnyMethod()
            );
        });

        builder.Services.AddOutputCache(options =>
            options.AddPolicy(
                "PostCachingPolicy",
                b =>
                    b
                        .AddPolicy<PostCachingPolicy>()
                        .VaryByValue(async (context, token) =>
                            {
                                if (context.Request.ContentLength == 0)
                                    return KeyValuePair.Create("bodyHash", "no-body");

                                context.Request.EnableBuffering();
                                StreamReader reader = new(
                                    context.Request.Body,
                                    leaveOpen: true,
                                    bufferSize: 1024,
                                    detectEncodingFromByteOrderMarks: true
                                );

                                var body = await reader.ReadToEndAsync(token);
                                context.Request.Body.Position = 0;
                                return KeyValuePair.Create(
                                    "bodyHash",
                                    HashCode.Combine(body).ToString()
                                );
                            }
                        )
            )
        );

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddControllers();

        builder.Services.ConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpClientActions.Add(client => client.Timeout = TimeSpan.FromMinutes(10))
        );

        _ = Env.Load(options: new LoadOptions(onlyExactPath: false));
        string? openRouteApiKey = Environment.GetEnvironmentVariable("OPENROUTE_API_KEY");
        string? mapillaryApiKey = Environment.GetEnvironmentVariable("MAPILLARY_API_KEY");
        string? postgrestUrl = builder.Configuration.GetConnectionString("PostgresCache") ??
                               Environment.GetEnvironmentVariable(
                                   "PostgresCache__ConnectionString"
                               );
        string? postgrestSchema = builder.Configuration.GetValue<string?>("PostgresCache:SchemaName", null) ??
                                  Environment.GetEnvironmentVariable("PostgresCache__SchemaName");
        string? postgrestTable = builder.Configuration.GetValue<string?>("PostgresCache:TableName", null) ??
                                 Environment.GetEnvironmentVariable("PostgresCache__TableName");

        ArgumentException.ThrowIfNullOrEmpty(postgrestUrl);
        ArgumentException.ThrowIfNullOrEmpty(postgrestSchema);
        ArgumentException.ThrowIfNullOrEmpty(postgrestTable);
        ArgumentException.ThrowIfNullOrEmpty(openRouteApiKey);
        ArgumentException.ThrowIfNullOrEmpty(mapillaryApiKey);

        builder.Services.AddDistributedPostgresCache(options =>
        {
            options.ConnectionString = postgrestUrl;
            options.SchemaName = postgrestSchema;
            options.TableName = postgrestTable;
            options.CreateIfNotExists = true;
            options.DefaultSlidingExpiration = TimeSpan.FromDays(365);
        });

        builder
            .Services.AddSingleton<PhotonGeocoderClient>()
            .AddSingleton<MarkerService>()
            .AddSingleton<GeocodingService>()
            .AddSingleton<CensusGeocoderClient>()
            .AddSingleton(sp => new OpenRouteClient(
                sp.GetRequiredService<HttpClient>(),
                openRouteApiKey
            ))
            .AddSingleton(sp => new MapillaryClient(
                sp.GetRequiredService<HttpClient>(),
                mapillaryApiKey
            ))
            .AddSingleton<SchoolsScraperService>()
            .AddHttpClient<OsrmHttpApiClient>(
                "OsrmClient",
                client => client.BaseAddress = new Uri("https://router.project-osrm.org/")
            );
    }
}