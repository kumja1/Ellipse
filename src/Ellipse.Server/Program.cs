using DotNetEnv;
using DotNetEnv.Extensions;
using Ellipse.Server.Services;
using Ellipse.Server.Utils.Objects;
using Geo.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Osrm.HttpApiClient;
using Serilog;

namespace Ellipse.Server;

public static class Program
{
    public static async Task Main(string[] args)
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

    public static async ValueTask ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog(
            (context, config) => config.Enrich.With<CallerEnricher>().WriteTo.Console()
        );

        builder.Services.AddRouting();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(
                "DynamicCors",
                policy =>
                {
                    policy.SetIsOriginAllowed(origin => true).AllowAnyHeader().AllowAnyMethod();
                }
            );
        });

        builder.Services.AddRequestTimeouts(options =>
            options.AddPolicy("ResponseTimeout", TimeSpan.FromMinutes(5))
        );

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddControllers();

        builder.Services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpClientActions.Add(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(10);
            });
        });

        builder
            .Services.AddSingleton<MarkerService>()
            .AddSingleton<GeoService>()
            .AddSingleton<CensusGeocoderClient>()
            .AddSingleton<SupabaseStorageClient>()
            .AddHttpClient<OsrmHttpApiClient>(
                "OsrmClient",
                client => client.BaseAddress = new Uri("https://router.project-osrm.org/")
            );

        Dictionary<string, string> env = Env.Load(Path.Join(Environment.CurrentDirectory, ".env"))
            .ToDotEnvDictionary(CreateDictionaryOption.Throw);
        string? anonKey = env.GetValueOrDefault("SUPABASE_ANON_KEY");
        string? supabaseUrl = env.GetValueOrDefault("SUPABASE_PROJECT_URL");

        ArgumentException.ThrowIfNullOrEmpty(anonKey);
        ArgumentException.ThrowIfNullOrEmpty(supabaseUrl);
        var mapboxClient = builder.Services.AddMapBoxGeocoding();
        mapboxClient.AddKey(anonKey);

        Supabase.Client client = new(supabaseUrl, anonKey);
        await client.InitializeAsync();

        builder.Services.AddSingleton(client);
    }
}
