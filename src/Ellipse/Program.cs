using Ellipse;
using Ellipse.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

partial class Program
{

    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        Initialize(builder);
        await builder.Build().RunAsync();
    }


    public static void Initialize(WebAssemblyHostBuilder builder)
    {
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");
        ConfigureServices(builder);
    }


    public static void ConfigureServices(WebAssemblyHostBuilder builder)
    {
        builder.Services
        .AddMudServices()
        .AddScoped<SiteFinderService>()
        .AddHttpClient<SchoolLocatorService>(client =>
        {
            client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
        });

        // builder.Services.AddSingleton<IMarkerFactory>();
    }
}

