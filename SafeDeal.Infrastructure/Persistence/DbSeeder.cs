using Microsoft.EntityFrameworkCore;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (!await context.Users.AnyAsync(u => u.Role == UserRole.Admin))
        {
            var admin = User.Create(
                "Admin SafeDeal",
                "admin@safedeal.com",
                BCrypt.Net.BCrypt.HashPassword("Admin@123456"),
                UserRole.Admin);

            admin.VerifyEmail();
            admin.UpdateIdentityStatus(IdentityStatus.Approved);

            await context.Users.AddAsync(admin);
            await context.SaveChangesAsync();
        }
    }
}