using MediatR;

namespace SafeDeal.Application.Notifications.Commands.MarkAsRead;

public record MarkAsReadCommand(int NotificationId, int UserId) : IRequest;