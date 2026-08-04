using SafeDeal.Domain.Common;

namespace SafeDeal.Domain.Events;

public class EmailVerifiedEvent : BaseEvent
{
    public int UserId { get; }
    public EmailVerifiedEvent(int userId) => UserId = userId;
}