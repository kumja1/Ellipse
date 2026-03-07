using Ellipse.Client.Services;
using Ellipse.Common.Utils.Logging;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Http;
using MudBlazor.Services;
using Serilog;

namespace Ellipse.Client;

static class Program
{
    public static async Task Main(string[] args)
    {
        WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);

        ConfigureServices(builder);
        await builder.Build().RunAsync();
    }

    private static void ConfigureServices(WebAssemblyHostBuilder builder)
    {
        Log.Logger = new LoggerConfiguration().Enrich.With(new CallerEnricher())
            .WriteTo.BrowserConsole()
            .CreateLogger();

        builder.Services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpClientActions.Add(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(10);
                client.BaseAddress = new Uri(
                    "http://localhost:6001"
                );
            });
        });

        builder
            .Services.AddMudServices().AddMudPopoverService()
            .AddHttpClient()
            .AddSingleton<SchoolDivisionService>();
    }
}