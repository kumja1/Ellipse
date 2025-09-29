using System.Buffers;
using Ellipse.Server.Policies;
using Ellipse.Server.Services;
using Ellipse.Server.Utils.Clients;
using Ellipse.Server.Utils.Clients.Mapping;
using Ellipse.Server.Utils.Clients.Mapping.Geocoding;
using Ellipse.Server.Utils.Logging;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Http;
using Osrm.HttpApiClient;
using Serilog;
using Supabase;

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
            app.UseRouting();
            app.UseCors("DynamicCorsPolicy");
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
        {
            options.AddBasePolicy(builder => builder.Expire(TimeSpan.FromMinutes(60)));
            options.AddPolicy(
                "PostCachingPolicy",
                builder =>
                    builder
                        .AddPolicy<PostCachingPolicy>()
                        .VaryByValue(
                            async (context, token) =>
                            {
                                using StreamReader reader = new(context.Request.Body);
                                if (reader.BaseStream.Length == 0)
                                    return KeyValuePair.Create("body", string.Empty);

                                return KeyValuePair.Create(
                                    "body",
                                    await reader.ReadToEndAsync(token)
                                );
                            }
                        )
            );
        });

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
