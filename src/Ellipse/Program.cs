using System.Security.Cryptography;
using System.Text;
using DotNetEnv;
using Ellipse.Common.Utils.Logging;
using Ellipse.Components;
using Ellipse.Policies;
using Ellipse.Services;
using Ellipse.Utils.Clients.Mapping;
using Ellipse.Utils.Clients.Mapping.Geocoding;
using Microsoft.Extensions.Http;
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
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Client.Components._Imports).Assembly);

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

                                string body = await reader.ReadToEndAsync(token);
                                context.Request.Body.Position = 0;
                                byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(body));
                                return KeyValuePair.Create(
                                    "bodyHash",
                                    Convert.ToHexString(hashBytes)
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