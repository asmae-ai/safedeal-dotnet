using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SafeDeal.Application.Common.Audit;
using SafeDeal.Application.Common.Interfaces;
using SafeDeal.Domain.Interfaces.Repositories;
using SafeDeal.Domain.Interfaces.Services;
using SafeDeal.Infrastructure.Persistence;
using SafeDeal.Infrastructure.Persistence.Repositories;
using SafeDeal.Infrastructure.Services.Audit;
using SafeDeal.Infrastructure.Services.Auth;
using SafeDeal.Infrastructure.Services.Cache;
using SafeDeal.Infrastructure.Services.Email;
using SafeDeal.Infrastructure.Services.Identity;
using SafeDeal.Infrastructure.Services.Payment;
using StackExchange.Redis;
using SafeDeal.Infrastructure.Services.Storage;

namespace SafeDeal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Redis
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")!));
        services.AddSingleton<IRedisCacheService, RedisCacheService>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IDisputeRepository, DisputeRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IIdentityVerificationRepository, IdentityVerificationRepository>();

        // Services
        services.AddScoped<ITokenService, JwtService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IPaymentService, StripeService>();
        services.AddHttpClient<IIdentityVerificationService, SumsubService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        // Audit : l'adresse IP et l'agent proviennent de la requete courante.
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditLogger, AuditLogger>();
        return services;
    }
}