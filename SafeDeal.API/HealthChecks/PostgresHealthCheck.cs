using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SafeDeal.Infrastructure.Persistence;

namespace SafeDeal.API.HealthChecks;

/// <summary>
/// Verifie que la base repond et qu'elle porte bien le schema attendu.
/// Une base joignable mais restee sur une ancienne migration est un incident
/// silencieux : l'API demarre, puis echoue sur la premiere requete metier.
/// </summary>
public sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;

    public PostgresHealthCheck(AppDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Un aller-retour reel : CanConnect peut reussir sur une connexion
            // recuperee du pool sans que le serveur ait repondu.
            await _db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);

            var pending = await _db.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pending.Any())
            {
                // Degrade et non Unhealthy : la base sert encore les requetes
                // dont le schema n'a pas bouge.
                return HealthCheckResult.Degraded("Database reachable, migrations pending.");
            }

            return HealthCheckResult.Healthy("Database reachable.");
        }
        catch (Exception ex)
        {
            // Le detail part au journal, jamais dans la reponse : un message
            // Npgsql contient l'hote, le port et le nom d'utilisateur.
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                description: "Database unreachable.",
                exception: ex);
        }
    }
}
