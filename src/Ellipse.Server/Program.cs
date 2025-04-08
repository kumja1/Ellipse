using CensusGeocoder;
using Ellipse.Server.Services;
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
        .AddSingleton<GeocodingService>()
        .AddSingleton<MarkerService>()
        .AddHttpClient<OsrmHttpApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://router.project-osrm.org/");
        });

    }
}
