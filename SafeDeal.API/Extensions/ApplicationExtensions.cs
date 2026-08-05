using SafeDeal.API.Middleware;

namespace SafeDeal.API.Extensions;

public static class ApplicationExtensions
{
    public static WebApplication UseApiMiddlewares(this WebApplication app)
    {
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<JwtBlacklistMiddleware>();
        app.UseCors("Frontend");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}