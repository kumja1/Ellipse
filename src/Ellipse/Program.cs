using Ellipse.Common.Utils.Logging;
using Ellipse.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Http;
using MudBlazor.Services;
using Serilog;

namespace Ellipse;

partial class Program
{
    public static async Task Main(string[] args)
    {
        WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);

        Setup(builder);
        await builder.Build().RunAsync();
    }

    private static void Setup(WebAssemblyHostBuilder builder)
    {
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");
        ConfigureServices(builder);
    }

    private static void ConfigureServices(WebAssemblyHostBuilder builder)
    {
        Log.Logger  = new LoggerConfiguration().Enrich.With(new CallerEnricher())
            .WriteTo.BrowserConsole()
            .CreateLogger();

        builder.Services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpClientActions.Add(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(10);
                client.BaseAddress = new Uri(
                    "https://doubtful-beatrix-lum-studios-0cd001db.koyeb.app/"
                );
            });
        });

        builder
            .Services.AddMudServices()
            .AddHttpClient()
            .AddSingleton<SchoolDivisionService>();
    }
}
