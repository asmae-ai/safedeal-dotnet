using SafeDeal.API.Extensions;
using SafeDeal.Infrastructure;
using SafeDeal.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// DOIT ÊTRE EN PREMIER - avant tout middleware
app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();
    await next();
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "SafeDeal API";
        options.Theme = ScalarTheme.Purple;
    });
}

app.UseApiMiddlewares();
app.MapControllers();

app.Run();

public partial class Program { }  