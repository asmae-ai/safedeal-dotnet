using SafeDeal.Domain.Common;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Domain.Entities;

public class Dispute : BaseEntity, IAuditableEntity
{
    public int TransactionId { get; private set; }
    public int OpenedByUserId { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DisputeStatus Status { get; private set; } = DisputeStatus.Open;
    public string? ResolutionNote { get; private set; }
    public ICollection<string> EvidenceFiles { get; private set; } = [];

    public Transaction Transaction { get; private set; } = null!;
    public User OpenedBy { get; private set; } = null!;

    private Dispute() { }

    public static Dispute Create(int transactionId, int openedByUserId, string category, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Dispute
        {
            TransactionId = transactionId,
            OpenedByUserId = openedByUserId,
            Category = category.Trim(),
            Description = description.Trim()
        };
    }

    public void AddEvidence(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        EvidenceFiles.Add(filePath);
        UpdateTimestamp();
    }

    public void Resolve(string note)
    {
        Status = DisputeStatus.Resolved;
        ResolutionNote = note;
        UpdateTimestamp();
    }

    public void Close()
    {
        Status = DisputeStatus.Closed;
        UpdateTimestamp();
    }
}