using SafeDeal.Domain.Common;

namespace SafeDeal.Domain.Entities;

public class Notification : BaseEntity, IAuditableEntity
{
    public int UserId { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public DateTime? ReadAt { get; private set; }
    public bool IsRead => ReadAt.HasValue;

    public User User { get; private set; } = null!;

    private Notification() { }

    public static Notification Create(int userId, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new Notification { UserId = userId, Message = message.Trim() };
    }

    public void MarkAsRead()
    {
        if (!IsRead)
        {
            ReadAt = DateTime.UtcNow;
            UpdateTimestamp();
        }
    }
}