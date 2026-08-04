using SafeDeal.Domain.Common;

namespace SafeDeal.Domain.Events;

public class IdentityVerificationSubmittedEvent : BaseEvent
{
    public int UserId { get; }
    public int VerificationId { get; }
    public IdentityVerificationSubmittedEvent(int userId, int verificationId)
    {
        UserId = userId;
        VerificationId = verificationId;
    }
}