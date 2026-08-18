using MediatR;

namespace SafeDeal.Application.Notifications.Commands.MarkAllRead;

public record MarkAllReadCommand(int UserId) : IRequest;
