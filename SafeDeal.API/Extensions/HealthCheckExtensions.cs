using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SafeDeal.API.HealthChecks;

namespace SafeDeal.API.Extensions;

/// <summary>
/// Sonde d'etat pour l'orchestrateur et la supervision.
/// <c>/health</c> couvre l'API et ses dependances, <c>/health/live</c> ne
/// repond que pour le processus : un Redis en panne ne doit pas provoquer le
/// redemarrage en boucle d'un conteneur qui, lui, fonctionne.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>Marque les sondes qui interrogent une dependance externe.</summary>
    public const string DependencyTag = "dependency";

    private static readonly JsonSerializerOptions ResponseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IServiceCollection AddSafeDealHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Une dependance qui ne repond pas doit trancher vite : sans plafond, la
        // sonde reste ouverte aussi longtemps que le delai TCP et l'orchestrateur
        // conclut a un timeout plutot qu'a un etat.
        var timeout = TimeSpan.FromSeconds(
            configuration.GetValue<double?>("HealthChecks:TimeoutSeconds") ?? 5);

        services.AddHealthChecks()
            // Sonde de vie du processus : sans elle, /health/live repondrait sur
            // un rapport vide et l'orchestrateur n'aurait rien a lire.
            .AddCheck(
                "api",
                () => HealthCheckResult.Healthy("API is running."))
            .AddCheck<PostgresHealthCheck>(
                "postgres",
                failureStatus: HealthStatus.Unhealthy,
                tags: [DependencyTag],
                timeout: timeout)
            .AddCheck<RedisHealthCheck>(
                "redis",
                // Degrade : le cache et la liste noire tombent, le parcours metier tient.
                failureStatus: HealthStatus.Degraded,
                tags: [DependencyTag],
                timeout: timeout);

        return services;
    }

    public static WebApplication MapSafeDealHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteResponseAsync,
            ResultStatusCodes =
            {
                // Degraded reste un 200 : le service repond, l'etat detaille dans
                // le corps dit ce qui manque. Seul Unhealthy sort de la rotation.
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        }).AllowAnonymous();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            // Aucune dependance : repond tant que le processus tient debout.
            Predicate = registration => !registration.Tags.Contains(DependencyTag),
            ResponseWriter = WriteResponseAsync
        }).AllowAnonymous();

        return app;
    }

    /// <summary>
    /// Reponse volontairement pauvre : nom, etat, duree. Ni chaine de connexion,
    /// ni message d'exception, ni version d'assemblage — /health est public.
    /// </summary>
    private static async Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries
                .Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
                    description = entry.Value.Description
                })
                .OrderBy(check => check.name)
                .ToArray()
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, ResponseOptions));
    }
}
