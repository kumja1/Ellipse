using Ellipse.Server.Services;
using MapboxGeocoder = Mapbox.AspNetCore.Services.MapBoxService;
using Mapbox.AspNetCore.DependencyInjection;

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
                policy.SetIsOriginAllowed(origin =>
                     origin.Contains("https://kumja2-ellipse-"))
                     .AllowAnyHeader()
                     .AllowAnyMethod();
            });
        });

        builder.Services.AddRequestTimeouts(options => options.AddPolicy("ResponseTimeout", TimeSpan.FromMinutes(5)));
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddControllers();

        builder.Services
        .AddSingleton<GeoService>()
        .AddSingleton<MapboxClient>()
        .AddSingleton<MarkerService>()
        .AddMapBoxServices(options => options.UseApiKey("pk.eyJ1Ijoia3VtamExIiwiYSI6ImNtMmRoenRsaDEzY3cyam9uZDA1cThzeDIifQ.twiBonW5YmTeLXjMEBhccA"))
        .AddHttpClient<MapboxGeocoder>();
    }
}
