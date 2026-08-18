using MediatR;

namespace SafeDeal.Application.Notifications.Commands.MarkOneRead;

public record MarkOneReadCommand(int UserId, int NotificationId) : IRequest;
