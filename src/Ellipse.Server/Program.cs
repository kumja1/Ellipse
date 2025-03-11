using Ellipse.Server.Services;
using MapboxGeocoder = Mapbox.AspNetCore.Services.MapBoxService;


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
                policy.WithOrigins("https://kumja2-ellipse-twl8-code-redirect-2.apps.rm2.thpm.p1.openshiftapps.com")
                .AllowAnyHeader()
                .AllowAnyMethod();
            });
        });
        
        builder.Services.AddRequestTimeouts(options => options.AddPolicy("ResponseTimeout", TimeSpan.FromMinutes(5)));
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddControllers();
        builder.Services.AddSwaggerGen();

        builder.Services.AddSingleton<GeoService>();
        builder.Services.AddSingleton<MapboxClient>();
        builder.Services.AddSingleton<MapboxGeocoder>();
    }
}
