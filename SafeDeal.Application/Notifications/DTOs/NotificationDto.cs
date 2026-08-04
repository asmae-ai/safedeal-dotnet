namespace SafeDeal.Application.Notifications.DTOs;

public record NotificationDto(
    int Id,
    string Message,
    string? ReadAt,
    string CreatedAt);