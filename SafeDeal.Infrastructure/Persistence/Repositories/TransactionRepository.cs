using Microsoft.EntityFrameworkCore;
using SafeDeal.Domain.Entities;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Infrastructure.Persistence.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly AppDbContext _context;
    public TransactionRepository(AppDbContext context) => _context = context;

    public async Task<Transaction?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.Transactions
            .Include(t => t.Vendor)
            .Include(t => t.Buyer)
            .Include(t => t.Logs)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<Transaction?> GetByTokenAsync(string token, CancellationToken ct = default)
        => await _context.Transactions
            .Include(t => t.Vendor)
            .Include(t => t.Buyer)
            .FirstOrDefaultAsync(t => t.SecureToken == token, ct);

    public async Task<(IEnumerable<Transaction> Items, int Total)> GetByUserIdAsync(
        int userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Transactions
            .Include(t => t.Vendor)
            .Include(t => t.Buyer)
            .Where(t => t.VendorId == userId || t.BuyerId == userId)
            .OrderByDescending(t => t.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task AddAsync(Transaction transaction, CancellationToken ct = default)
    {
        await _context.Transactions.AddAsync(transaction, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Transaction transaction, CancellationToken ct = default)
    {
        _context.Transactions.Update(transaction);
        await _context.SaveChangesAsync(ct);
    }
}