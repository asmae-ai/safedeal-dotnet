namespace SafeDeal.Application.Notifications.DTOs;

public record NotificationDto(int Id, string Message, bool IsRead, DateTime CreatedAt);
