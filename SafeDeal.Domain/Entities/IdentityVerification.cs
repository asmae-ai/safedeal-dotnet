using SafeDeal.Domain.Common;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Domain.Entities;

public class IdentityVerification : BaseEntity, IAuditableEntity
{
    public int UserId { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public string DocumentFrontPath { get; private set; } = string.Empty;
    public string SelfiePath { get; private set; } = string.Empty;
    public IdentityStatus Status { get; private set; } = IdentityStatus.Pending;
    public string? RejectionReason { get; private set; }
    public string? SumsubApplicantId { get; private set; }

    public User User { get; private set; } = null!;

    private IdentityVerification() { }

    public static IdentityVerification Create(int userId, string documentType, string documentFrontPath, string selfiePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentFrontPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(selfiePath);

        return new IdentityVerification
        {
            UserId = userId,
            DocumentType = documentType.ToLower(),
            DocumentFrontPath = documentFrontPath,
            SelfiePath = selfiePath
        };
    }

    public void Approve()
    {
        Status = IdentityStatus.Approved;
        UpdateTimestamp();
    }

    public void Reject(string reason)
    {
        Status = IdentityStatus.Rejected;
        RejectionReason = reason;
        UpdateTimestamp();
    }

    public void SetSumsubApplicantId(string applicantId)
    {
        SumsubApplicantId = applicantId;
        UpdateTimestamp();
    }
}