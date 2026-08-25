namespace SafeDeal.Application.Notifications.DTOs;

public record NotificationDto(
    int Id,
    string Message,
    string Type,
    int? TransactionId,
    bool IsRead,
    string CreatedAt);
