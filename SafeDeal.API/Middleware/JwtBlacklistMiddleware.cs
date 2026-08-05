using SafeDeal.Infrastructure.Services.Cache;

namespace SafeDeal.API.Middleware;

public class JwtBlacklistMiddleware
{
    private readonly RequestDelegate _next;
    public JwtBlacklistMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IRedisCacheService cache)
    {
        var token = context.Request.Headers.Authorization
            .FirstOrDefault()?.Split(" ").Last();

        if (token is not null && await cache.ExistsAsync($"blacklist:{token}"))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { message = "Unauthenticated." });
            return;
        }

        await _next(context);
    }
}