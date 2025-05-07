using AngleSharp.Common;
using DotNetEnv;
using DotNetEnv.Extensions;
using Ellipse.Server.Services;
using Ellipse.Server.Utils.Objects;
using Geo.ArcGIS;
using Geo.ArcGIS.Services;
using Geo.Extensions.DependencyInjection;
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
            var builder = WebApplication.CreateBuilder(args);
            await ConfigureServices(builder);

            var app = builder.Build();

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
                policy =>  policy.SetIsOriginAllowed(origin => true).AllowAnyHeader().AllowAnyMethod()
            );
        });

        builder.Services.AddRequestTimeouts(options =>
            options.AddPolicy("ResponseTimeout", TimeSpan.FromMinutes(5))
        );

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddControllers();

        builder.Services.ConfigureAll<HttpClientFactoryOptions>(options => options.HttpClientActions.Add(client => client.Timeout = TimeSpan.FromMinutes(10)));

        Dictionary<string, string> env = Env.Load(Path.Join(Environment.CurrentDirectory, ".env"))
            .ToDotEnvDictionary(CreateDictionaryOption.Throw);

        string? anonKey = env.GetValueOrDefault("SUPABASE_ANON_KEY");
        string? supabaseUrl = env.GetValueOrDefault("SUPABASE_PROJECT_URL");

        ArgumentException.ThrowIfNullOrEmpty(anonKey);
        ArgumentException.ThrowIfNullOrEmpty(supabaseUrl);

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
            .AddSingleton<GeoService>()
            .AddSingleton<CensusGeocoderClient>()
            .AddSingleton<WebScraperService>()
            .AddHttpClient<OsrmHttpApiClient>(
                "OsrmClient",
                client => client.BaseAddress = new Uri("https://router.project-osrm.org/")
            );
    }
}
