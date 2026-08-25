using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SafeDeal.Application.Common.Audit;
using SafeDeal.Domain.Entities;
using SafeDeal.Infrastructure.Persistence;

namespace SafeDeal.Infrastructure.Services.Audit;

public class AuditLogger : IAuditLogger
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpContextAccessor _httpContext;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(
        IServiceScopeFactory scopeFactory,
        IHttpContextAccessor httpContext,
        ILogger<AuditLogger> logger)
    {
        _scopeFactory = scopeFactory;
        _httpContext = httpContext;
        _logger = logger;
    }

    public async Task LogAsync(AuditEntry entry, CancellationToken ct = default)
    {
        try
        {
            var context = _httpContext.HttpContext;

            var log = AuditLog.Record(
                entry.Action,
                entry.UserId,
                entry.Subject,
                entry.EntityType,
                entry.EntityId,
                entry.Succeeded,
                entry.FailureReason,
                ResolveIpAddress(context),
                context?.Request.Headers.UserAgent.ToString(),
                AuditRedaction.Serialize(entry.Metadata));

            // Portee dediee : l'audit doit survivre au rollback de la transaction
            // metier qui l'a declenche, et ne pas partager son ChangeTracker.
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditLogs.Add(log);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Un audit indisponible ne doit pas annuler un paiement. On trace
            // l'incident dans les logs applicatifs et on laisse passer.
            _logger.LogError(ex, "Failed to write audit entry {Action}.", entry.Action);
        }
    }

    /// <summary>
    /// Derriere un reverse proxy, l'adresse de connexion est celle du proxy :
    /// X-Forwarded-For porte alors l'adresse d'origine.
    /// </summary>
    private static string? ResolveIpAddress(HttpContext? context)
    {
        if (context is null) return null;

        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
