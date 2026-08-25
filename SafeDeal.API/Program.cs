using SafeDeal.API.Extensions;
using SafeDeal.Infrastructure;
using SafeDeal.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

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

// Seuls les avatars sont servis en acces libre. Les pieces d'identite et les
// preuves de litige restent derriere des endpoints authentifies : exposer tout
// le dossier uploads rendait une piece d'identite lisible par quiconque en
// connaissait le chemin, que les DTO admin renvoient.
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
var avatarsPath = Path.Combine(uploadsPath, "avatars");
Directory.CreateDirectory(avatarsPath);
Directory.CreateDirectory(Path.Combine(uploadsPath, "identity"));
Directory.CreateDirectory(Path.Combine(uploadsPath, "disputes"));

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(avatarsPath),
    RequestPath = "/uploads/avatars"
});

app.UseApiMiddlewares();
app.MapControllers();
app.Run();

public partial class Program { }
