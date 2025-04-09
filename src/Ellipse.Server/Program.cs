using CensusGeocoder;
using Ellipse.Server.Services;
using Nominatim.API.Geocoders;
using Nominatim.API.Interfaces;
using Nominatim.API.Web;
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
            options.AddPolicy("DynamicCors", policy =>
            {
                policy.SetIsOriginAllowed(origin => true)
                     .AllowAnyHeader()
                     .AllowAnyMethod();
            });
        });

        builder.Services.AddRequestTimeouts(options => options.AddPolicy("ResponseTimeout", TimeSpan.FromMinutes(5)));
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddControllers();
        builder.Services
        .AddSingleton<GeoService>()
        .AddSingleton<MarkerService>()
        .AddSingleton<GeocodingService>()
        .AddSingleton(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var client = httpClientFactory.CreateClient("OsrmClient");

            client.BaseAddress = new Uri("https://router.project-osrm.org/");
            return new OsrmHttpApiClient(client);
        })
        .AddHttpClient();

    }
}
