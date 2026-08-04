using MediatR;

namespace SafeDeal.Application.Notifications.Commands.MarkAllAsRead;

public record MarkAllAsReadCommand(int UserId) : IRequest;