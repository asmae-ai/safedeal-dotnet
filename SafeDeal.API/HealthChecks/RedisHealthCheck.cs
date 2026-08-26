using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace SafeDeal.API.HealthChecks;

/// <summary>
/// Redis porte le cache, les codes OTP et la liste noire de jetons. Son absence
/// degrade le service sans l'interrompre : l'API sait encore authentifier et
/// traiter une transaction, elle perd la revocation immediate et le cache.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // abortConnect=false laisse le multiplexeur « connecte » sans serveur
            // en face : seul un PING reel tranche.
            var latency = await _redis.GetDatabase().PingAsync();

            return HealthCheckResult.Healthy($"Redis reachable ({latency.TotalMilliseconds:F0} ms).");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                description: "Redis unreachable.",
                exception: ex);
        }
    }
}
