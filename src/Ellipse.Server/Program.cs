using Ellipse.Server.Policies;
using Ellipse.Server.Services;
using Ellipse.Server.Utils.Clients.Mapping;
using Ellipse.Server.Utils.Clients.Mapping.Geocoding;
using Ellipse.Server.Utils.Logging;
using Microsoft.Extensions.Http;
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

    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog(
            (_, config) => config.Enrich.With<CallerEnricher>().WriteTo.Console()
        );

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
                builder =>
                    builder
                        .AddPolicy<PostCachingPolicy>()
                        .VaryByValue(
                            async (context, token) =>
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

        string? openRouteApiKey = Environment.GetEnvironmentVariable("OPENROUTE_API_KEY");
        string? postgrestUrl = Environment.GetEnvironmentVariable(
            "PostgresCache__ConnectionString"
        );
        string? postgrestSchema = Environment.GetEnvironmentVariable("PostgresCache__SchemaName");
        string? postgrestTable = Environment.GetEnvironmentVariable("PostgresCache__TableName");

        ArgumentException.ThrowIfNullOrEmpty(postgrestUrl);
        ArgumentException.ThrowIfNullOrEmpty(postgrestSchema);
        ArgumentException.ThrowIfNullOrEmpty(postgrestTable);
        ArgumentException.ThrowIfNullOrEmpty(openRouteApiKey);

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
            .AddSingleton<SchoolsScraperService>()
            .AddHttpClient<OsrmHttpApiClient>(
                "OsrmClient",
                client => client.BaseAddress = new Uri("https://router.project-osrm.org/")
            );
    }
}
