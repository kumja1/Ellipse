using Ellipse.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Http;
using MudBlazor.Services;

namespace Ellipse;

partial class Program
{
    public static async Task Main(string[] args)
    {
        WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);

        Setup(builder);
        await builder.Build().RunAsync();
    }

    public static void Setup(WebAssemblyHostBuilder builder)
    {
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");
        ConfigureServices(builder);
    }

    public static void ConfigureServices(WebAssemblyHostBuilder builder)
    {
        builder.Services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpClientActions.Add(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(10);
            });
        });

        builder
            .Services.AddMudServices()
            .AddScoped<SiteFinderService>()
            .AddHttpClient<SchoolFetcherService>(client =>
            {
                client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
            });
    }
}