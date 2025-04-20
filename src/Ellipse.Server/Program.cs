using Ellipse.Server.Services;
using Geo.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Osrm.HttpApiClient;

namespace Ellipse.Server;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureServices(builder);

        var app = builder.Build();

        app.UseRouting();
        app.UseCors("DynamicCors");
        app.UseRequestTimeouts();
        app.MapControllers();
        app.Run();
    }

    public static void ConfigureServices(WebApplicationBuilder builder)
    {
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
            .AddHttpClient<OsrmHttpApiClient>(
                "OsrmClient",
                client => client.BaseAddress = new Uri("https://router.project-osrm.org/")
            );

        var mapboxClient = builder.Services.AddMapBoxGeocoding();
        mapboxClient.AddKey(
            "pk.eyJ1Ijoia3VtamExIiwiYSI6ImNtOGl0eXdiMzBiYm0ya29lbXV2YWY5dWMifQ.GhJar4VuZnxM7llZdVylqQ"
        );
    }
}
