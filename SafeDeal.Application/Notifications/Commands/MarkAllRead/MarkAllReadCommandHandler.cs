using MediatR;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Notifications.Commands.MarkAllRead;

public class MarkAllReadCommandHandler : IRequestHandler<MarkAllReadCommand>
{
    private readonly INotificationRepository _notifications;

    public MarkAllReadCommandHandler(INotificationRepository notifications)
    {
        _notifications = notifications;
    }

    public async Task Handle(MarkAllReadCommand request, CancellationToken ct)
    {
        await _notifications.MarkAllAsReadAsync(request.UserId, ct);
    }
}


