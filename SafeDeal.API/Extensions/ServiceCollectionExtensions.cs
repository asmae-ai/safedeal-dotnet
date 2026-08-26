using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using SafeDeal.Application.Common.Behaviors;
using System.Security.Claims;
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
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<SafeDeal.API.OpenApi.SafeDealDocumentTransformer>();
            options.AddOperationTransformer<SafeDeal.API.OpenApi.SecurityOperationTransformer>();
        });

        // Compression des reponses JSON (Brotli, puis gzip en repli).
        services.AddSafeDealCompression(configuration);

        // Sondes d'etat (API, PostgreSQL, Redis).
        services.AddSafeDealHealthChecks(configuration);

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

        // Durées de vie du cache de lecture (section Cache).
        services.Configure<SafeDeal.Application.Common.Options.CacheOptions>(
            configuration.GetSection(SafeDeal.Application.Common.Options.CacheOptions.SectionName));

        // MediatR + Behaviors
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(
                typeof(SafeDeal.Application.Auth.Commands.Login.LoginCommand).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            // Apres la validation : une commande rejetee en amont n'est pas une
            // action metier, seules les tentatives reellement traitees sont tracees.
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
            // En dernier, au plus pres du handler : une reponse servie depuis le
            // cache reste ainsi validee, journalisee et auditee comme les autres.
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
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
            // ajustables par configuration (RateLimiting:<politique>).
            // Identifie par compte quand l'utilisateur est connu, par IP sinon :
            // un attaquant derriere une IP partagee ne doit pas pouvoir bloquer
            // les autres utilisateurs du meme reseau.
            void AddPolicy(string name, int defaultLimit, bool perUser = false)
            {
                var limit = configuration.GetValue<int?>($"RateLimiting:{name}") ?? defaultLimit;
                options.AddPolicy(name, context =>
                {
                    var partition = perUser && context.User.Identity?.IsAuthenticated == true
                        ? $"user:{context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value}"
                        : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"{name}:{partition}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = limit,
                            Window = TimeSpan.FromMinutes(1)
                        });
                });
            }

            // --- Authentification : cibles privilegiees du bourrage d'identifiants ---
            AddPolicy("login", 5);
            AddPolicy("register", 10);
            AddPolicy("otp", 3, perUser: true);
            AddPolicy("verify-otp", 10);
            AddPolicy("refresh", 30);
            AddPolicy("password-reset", 5);
            AddPolicy("email-verification", 10, perUser: true);

            // --- Ecritures metier : bornees large, pour ne freiner personne ---
            AddPolicy("mutations", 60, perUser: true);

            // --- Webhooks : proteges par signature, mais le calcul HMAC lui-meme
            //     ne doit pas devenir un vecteur de saturation. ---
            AddPolicy("webhooks", 300);
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