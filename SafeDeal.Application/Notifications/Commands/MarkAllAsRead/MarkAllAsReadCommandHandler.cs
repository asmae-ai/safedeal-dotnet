using MediatR;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Notifications.Commands.MarkAllAsRead;

public class MarkAllAsReadCommandHandler : IRequestHandler<MarkAllAsReadCommand>
{
    private readonly INotificationRepository _notifications;
    public MarkAllAsReadCommandHandler(INotificationRepository notifications) => _notifications = notifications;

    public async Task Handle(MarkAllAsReadCommand request, CancellationToken ct)
        => await _notifications.MarkAllAsReadAsync(request.UserId, ct);
}