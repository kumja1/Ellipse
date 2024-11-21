using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using dotnet80_example;
using dymaptic.GeoBlazor.Core;
using dymaptic.GeoBlazor.Core.Model;
using Microsoft.Extensions.DependencyInjection;

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
        builder.Services.AddHttpClient<MapService>(client =>
        {
            client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
        });

        builder.Services.AddGeoBlazor(builder.Configuration);
        
        // builder.Services.AddSingleton<IMarkerFactory>();
    }
}




