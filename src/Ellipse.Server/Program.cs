using DotNetEnv;
using DotNetEnv.Extensions;
using Ellipse.Server.Services;
using Ellipse.Server.Utils.Clients;
using Ellipse.Server.Utils.Clients.Mapping;
using Ellipse.Server.Utils.Clients.Mapping.Geocoding;
using Ellipse.Server.Utils.Logging;
using Microsoft.Extensions.Http;
using Osrm.HttpApiClient;
using Serilog;
using Supabase;

namespace Ellipse.Server;

public static class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            await ConfigureServices(builder);

            WebApplication app = builder.Build();

            app.UseRouting();
            app.UseCors("DynamicCors");
            app.UseRequestTimeouts();
            app.MapControllers();
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occured");
            throw;
        }
    }

    public static async ValueTask ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog(
            (_, config) => config.Enrich.With<CallerEnricher>().WriteTo.Console()
        );

        builder.Services.AddRouting();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(
                "DynamicCors",
                policy =>
                    policy.SetIsOriginAllowed(origin => true).AllowAnyHeader().AllowAnyMethod()
            );
        });

        builder.Services.AddRequestTimeouts(options =>
            options.AddPolicy("ResponseTimeout", TimeSpan.FromMinutes(5))
        );

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddControllers();

        builder.Services.ConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpClientActions.Add(client => client.Timeout = TimeSpan.FromMinutes(10))
        );

        Dictionary<string, string> env = Env.Load(".env")
            .ToDotEnvDictionary(CreateDictionaryOption.Throw);

        string? anonKey = env.GetValueOrDefault("SUPABASE_ANON_KEY");
        string? supabaseUrl = env.GetValueOrDefault("SUPABASE_PROJECT_URL");
        string? openRouteApiKey = env.GetValueOrDefault("OPENROUTE_API_KEY");

        ArgumentException.ThrowIfNullOrEmpty(anonKey);
        ArgumentException.ThrowIfNullOrEmpty(supabaseUrl);
        ArgumentException.ThrowIfNullOrEmpty(openRouteApiKey);

        Client client = new(
            supabaseUrl,
            anonKey,
            new SupabaseOptions { AutoConnectRealtime = true, AutoRefreshToken = true }
        );
        SupabaseStorageClient storageClient = new(client);

        try
        {
            await client.InitializeAsync();
            await storageClient.InitializeAsync();

            builder.Services.AddSingleton(client).AddSingleton(storageClient);
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occured while initializing Supabase client");
            return;
        }

        builder
            .Services.AddSingleton<PhotonGeocoderClient>()
            .AddSingleton<MarkerService>()
            .AddSingleton<GeocodingService>()
            .AddSingleton<CensusGeocoderClient>()
            .AddSingleton(sp => new OpenRouteClient(
                sp.GetRequiredService<HttpClient>(),
                openRouteApiKey
            ))
            .AddSingleton<WebScraperService>()
            .AddHttpClient<OsrmHttpApiClient>(
                "OsrmClient",
                client => client.BaseAddress = new Uri("https://router.project-osrm.org/")
            );
    }
}
