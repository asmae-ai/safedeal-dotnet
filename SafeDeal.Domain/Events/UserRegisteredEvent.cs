using SafeDeal.Domain.Common;

namespace SafeDeal.Domain.Events;

public class UserRegisteredEvent : BaseEvent
{
    public int UserId { get; }
    public string Email { get; }
    public UserRegisteredEvent(int userId, string email)
    {
        UserId = userId;
        Email = email;
    }
}