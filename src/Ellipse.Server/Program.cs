namespace Ellipse.Server;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureServices(builder);

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }


        app.UseRouting();

        app.UseCors("AllowAll");
        app.UseHttpsRedirection();
        app.MapControllers();
        app.Run();
    }

    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddCors(options =>
                {
                    options.AddPolicy("AllowAll", policy =>
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()  
                              .AllowAnyHeader()
                              .Build();
                    });
                }); builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddControllers();
        builder.Services.AddSwaggerGen();
    }
}
