using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Notifications.Commands.MarkOneRead;

public class MarkOneReadCommandHandler : IRequestHandler<MarkOneReadCommand>
{
    private readonly INotificationRepository _notifications;

    public MarkOneReadCommandHandler(INotificationRepository notifications)
    {
        _notifications = notifications;
    }

    public async Task Handle(MarkOneReadCommand request, CancellationToken ct)
    {
        var notification = await _notifications.GetByIdAsync(request.NotificationId, ct)
            ?? throw new NotFoundException("Notification", request.NotificationId);

        if (notification.UserId != request.UserId)
            throw new NotFoundException("Notification", request.NotificationId);

        notification.MarkAsRead();
        await _notifications.UpdateAsync(notification, ct);
    }
}
