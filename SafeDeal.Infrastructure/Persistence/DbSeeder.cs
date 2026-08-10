using Microsoft.EntityFrameworkCore;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        // 1. Admin
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

        // 2. Vendor de test
        var vendor = await context.Users.FirstOrDefaultAsync(u => u.Email == "vendor@safedeal.com");
        if (vendor is null)
        {
            vendor = User.Create(
                "Vendor Test",
                "vendor@safedeal.com",
                BCrypt.Net.BCrypt.HashPassword("password123"),
                UserRole.Vendor);
            vendor.VerifyEmail();
            await context.Users.AddAsync(vendor);
            await context.SaveChangesAsync();
        }

        // 3. Buyer de test
        if (!await context.Users.AnyAsync(u => u.Email == "buyer@safedeal.com"))
        {
            var buyer = User.Create(
                "Buyer Test",
                "buyer@safedeal.com",
                BCrypt.Net.BCrypt.HashPassword("password123"),
                UserRole.Buyer);
            buyer.VerifyEmail();
            await context.Users.AddAsync(buyer);
            await context.SaveChangesAsync();
        }

        // 4. Vérification KYC de test + fichiers image réels sur le disque
        if (!await context.IdentityVerifications.AnyAsync())
        {
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "identity");
            Directory.CreateDirectory(uploadsDir);

            var pngBytes = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

            var frontFileName = "seed_document_front.png";
            var selfieFileName = "seed_selfie.png";

            await File.WriteAllBytesAsync(Path.Combine(uploadsDir, frontFileName), pngBytes);
            await File.WriteAllBytesAsync(Path.Combine(uploadsDir, selfieFileName), pngBytes);

            var frontPath = Path.Combine("uploads", "identity", frontFileName);
            var selfiePath = Path.Combine("uploads", "identity", selfieFileName);

            var verification = IdentityVerification.Create(
                vendor.Id,
                "cin",
                frontPath,
                selfiePath);

            await context.IdentityVerifications.AddAsync(verification);
            await context.SaveChangesAsync();
        }
    }
}