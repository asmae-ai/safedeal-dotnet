using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using SafeDeal.Application.Common.Behaviors;
using System.Text;
using System.Threading.RateLimiting;

namespace SafeDeal.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddOpenApi();

        // JWT Authentication
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!))
                };
            });

        services.AddAuthorization();

        // Paramètres commerciaux (taux de commission, devise de référence).
        services.Configure<SafeDeal.Application.Common.Options.PlatformOptions>(
            configuration.GetSection(SafeDeal.Application.Common.Options.PlatformOptions.SectionName));

        // MediatR + Behaviors
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(
                typeof(SafeDeal.Application.Auth.Commands.Login.LoginCommand).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        });

        // FluentValidation
        services.AddValidatorsFromAssembly(
            typeof(SafeDeal.Application.Auth.Commands.Login.LoginCommand).Assembly);

        // Rate Limiting
        services.AddRateLimiter(options =>
        {
            // Par défaut ASP.NET renvoie 503, que le frontend interprète comme une panne.
            // Une limite atteinte est un 429, ce que l'écran de connexion sait présenter.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Les seuils restent des valeurs de production, mais deviennent
            // ajustables par configuration plutot que codes en dur.
            void AddIpPolicy(string name, int defaultLimit)
            {
                var limit = configuration.GetValue<int?>($"RateLimiting:{name}") ?? defaultLimit;
                options.AddPolicy(name, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = limit,
                            Window = TimeSpan.FromMinutes(1)
                        }));
            }

            AddIpPolicy("login", 5);
            AddIpPolicy("register", 10);
            AddIpPolicy("otp", 3);
        });

        // CORS
        services.AddCors(options =>
        {
            options.AddPolicy("Frontend", policy =>
            {
                policy.WithOrigins(
                        configuration["Cors:AllowedOrigins"]?.Split(',') ?? ["http://localhost:5173"])
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }
}