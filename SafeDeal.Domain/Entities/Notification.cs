using SafeDeal.Domain.Common;
using SafeDeal.Domain.Enums;

namespace SafeDeal.Domain.Entities;

public class Notification : BaseEntity, IAuditableEntity
{
    public int UserId { get; private set; }
    public string Message { get; private set; } = string.Empty;
    /// <summary>Nature de la notification : détermine l'icône et la couleur côté interface.</summary>
    public NotificationType Type { get; private set; } = NotificationType.System;
    /// <summary>Transaction concernée, quand la notification en vise une : permet d'y naviguer.</summary>
    public int? TransactionId { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public bool IsRead => ReadAt.HasValue;

    public User User { get; private set; } = null!;

    private Notification() { }

    public static Notification Create(
        int userId,
        string message,
        NotificationType type = NotificationType.System,
        int? transactionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new Notification
        {
            UserId = userId,
            Message = message.Trim(),
            Type = type,
            TransactionId = transactionId
        };
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