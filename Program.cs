using Ellipse;
using Ellipse.Services;
using Mapbox.AspNetCore.DependencyInjection;
using Mapbox.AspNetCore.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;


partial class Program
{

    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        Init(builder);
        await builder.Build().RunAsync();
    }


    public static void Init(WebAssemblyHostBuilder builder)
    {
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");
        ConfigureServices(builder);
    }


    public static void ConfigureServices(WebAssemblyHostBuilder builder)
    {
        builder.Services
        .AddMudServices()
        .AddMapBoxServices(options => options.UseApiKey("pk.eyJ1Ijoia3VtamExIiwiYSI6ImNtMmRoenRsaDEzY3cyam9uZDA1cThzeDIifQ.twiBonW5YmTeLXjMEBhccA"))
        .AddScoped<MapBoxService>()
        .AddHttpClient<MapService>(client =>
        {
            client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
        });
    

        // builder.Services.AddSingleton<IMarkerFactory>();
    }
}

