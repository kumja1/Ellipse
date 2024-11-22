using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Syncfusion.Blazor;
using Ellipse.Services;
using Ellipse;
using Syncfusion.Licensing;

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
        
        builder.Services.AddSyncfusionBlazor();
        SyncfusionLicenseProvider.RegisterLicense("ORg4AjUWIQA/Gnt2UlhhQlVMfV5AQmBIYVp/TGpJfl96cVxMZVVBJAtUQF1hTX9SdERjUHtXc3RXQWVY;MzU4OTM4NkAzMjM3MmUzMDJlMzBkS3JYbVdjVzRZdjlOTFVORVdJdXVyWHNwSXBIQ0JQTzU2STBxSDFVSG9ZPQ==;MzU4OTM4N0AzMjM3MmUzMDJlMzBMM1IvWVBQa2JIaTBlNlU2OHkxcEc4eW9kdVBVYTJFTHpqSGowNjFZaXQwPQ==");
        // builder.Services.AddSingleton<IMarkerFactory>();
    }
}




