using Microsoft.EntityFrameworkCore;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Infrastructure.Persistence.Repositories;

public class IdentityVerificationRepository : IIdentityVerificationRepository
{
    private readonly AppDbContext _context;
    public IdentityVerificationRepository(AppDbContext context) => _context = context;

    public async Task<IdentityVerification?> GetByUserIdAsync(int userId, CancellationToken ct = default)
        => await _context.IdentityVerifications
            .FirstOrDefaultAsync(v => v.UserId == userId, ct);

    public async Task AddAsync(IdentityVerification verification, CancellationToken ct = default)
    {
        await _context.IdentityVerifications.AddAsync(verification, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(IdentityVerification verification, CancellationToken ct = default)
    {
        _context.IdentityVerifications.Update(verification);
        await _context.SaveChangesAsync(ct);
    }
}