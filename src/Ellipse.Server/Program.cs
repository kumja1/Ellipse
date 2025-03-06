namespace Ellipse.Server;

public static class Program
{

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureServices(builder);

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        ConfigureEndpoints(app);

        app.Run();

    }

    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
    }

    public static void ConfigureEndpoints(WebApplication app)
    {
        app.MapPost("/get-schools", async (context) =>
        {
            IFormCollection form = await context.Request.ReadFormAsync();
            if (!form.ContainsKey("divisionCode"))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Error: Missing required field 'divisionCode'.");
                return;
            }

            string divisionCode = form["divisionCode"];
            var result = await WebScraper.StartNewAsync(int.Parse(divisionCode));

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(result);
        });
    }

}

