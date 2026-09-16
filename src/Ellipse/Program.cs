using DotNetEnv;
using DotNetEnv.Configuration;
using Ellipse.Client.Services;
using Ellipse.Common.Utils.Logging;
using Ellipse.Components;
using Ellipse.Services;
using Ellipse.Utils.Clients.Mapping;
using Ellipse.Utils.Clients.Mapping.Geocoding;
using Microsoft.Extensions.Http;
using MudBlazor.Services;
using Osrm.HttpApiClient;
using Serilog;

namespace Ellipse;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.Host.UseSerilog((_, config) => config.Enrich.With<CallerEnricher>().WriteTo.Console()
        );

        ConfigureServices(builder);
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        WebApplication app = builder.Build();
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }
        

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();
        app.UseAntiforgery();
        app.UseStaticFiles();

        app.MapControllers();
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

        app.Run();
    }


    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddRouting();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(
                "DynamicCorsPolicy",
                policy =>
                    policy.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod()
            );
        });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddControllers();

        builder.Services.ConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpClientActions.Add(client => client.Timeout = TimeSpan.FromMinutes(10))
            
        );

        builder.Configuration.AddDotNetEnv(options: LoadOptions.TraversePath());
        string? openRouteApiKey = builder.Configuration.GetValue<string?>("OPENROUTE_API_KEY");
        string? mapillaryApiKey = builder.Configuration.GetValue<string?>("MAPILLARY_API_KEY");
        string? postgrestUrl = builder.Configuration.GetValue<string?>("PostgresCache:ConnectionString");
        string? postgrestSchema = builder.Configuration.GetValue<string?>("PostgresCache:SchemaName");
        string? postgrestTable = builder.Configuration.GetValue<string?>("PostgresCache:TableName");

        ArgumentException.ThrowIfNullOrEmpty(openRouteApiKey);
        ArgumentException.ThrowIfNullOrEmpty(mapillaryApiKey);
        ArgumentException.ThrowIfNullOrEmpty(postgrestUrl);
        ArgumentException.ThrowIfNullOrEmpty(postgrestSchema);
        ArgumentException.ThrowIfNullOrEmpty(postgrestTable);

        builder.Services.AddDistributedPostgresCache(options =>
        {
            options.ConnectionString = postgrestUrl;
            options.SchemaName = postgrestSchema;
            options.TableName = postgrestTable;
            options.CreateIfNotExists = true;
            options.DefaultSlidingExpiration = TimeSpan.FromDays(365);
        });
        
        
        builder
            .Services.AddMudServices().AddSingleton<PhotonGeocoderClient>()
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

        // Client Services
        builder.Services.AddHttpClient<SchoolDivisionService>(client => client.BaseAddress = new Uri("http://localhost:5291/"));


    }
}