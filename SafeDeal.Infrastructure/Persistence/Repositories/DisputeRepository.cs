using Microsoft.EntityFrameworkCore;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Infrastructure.Persistence.Repositories;

public class DisputeRepository : IDisputeRepository
{
    private readonly AppDbContext _context;
    public DisputeRepository(AppDbContext context) => _context = context;

    public async Task<Dispute?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.Disputes
            .Include(d => d.OpenedBy)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<Dispute?> GetByTransactionIdAsync(int transactionId, CancellationToken ct = default)
        => await _context.Disputes
            .Include(d => d.OpenedBy)
            .FirstOrDefaultAsync(d => d.TransactionId == transactionId, ct);

    public async Task AddAsync(Dispute dispute, CancellationToken ct = default)
    {
        await _context.Disputes.AddAsync(dispute, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Dispute dispute, CancellationToken ct = default)
    {
        _context.Disputes.Update(dispute);
        await _context.SaveChangesAsync(ct);
    }
}