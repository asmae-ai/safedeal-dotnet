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

// Avant tout ce qui ecrit une reponse — erreurs et fichiers statiques compris :
// un middleware place plus bas ne pourrait plus compresser ce qui est deja parti.
app.UseResponseCompression();

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

        // Le jeton se saisit une fois et vaut pour tous les essais : sans cela,
        // essayer un endpoint protege depuis la documentation demande de coller
        // un « Bearer » a la main a chaque appel.
        options.AddPreferredSecuritySchemes("Bearer");

        // Les exemples proposes correspondent a ce que le frontend utilise.
        options.WithDefaultHttpClient(ScalarTarget.JavaScript, ScalarClient.Fetch);

        // Les groupes s'ouvrent un a un : huit domaines deplies d'emblee
        // noieraient la barre laterale.
        options.WithDefaultOpenAllTags(false);
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
app.MapSafeDealHealthChecks();
app.MapControllers();
app.Run();

public partial class Program { }
