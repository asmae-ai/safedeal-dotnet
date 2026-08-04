using MediatR;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Notifications.Commands.MarkAsRead;

public class MarkAsReadCommandHandler : IRequestHandler<MarkAsReadCommand>
{
    private readonly INotificationRepository _notifications;
    public MarkAsReadCommandHandler(INotificationRepository notifications) => _notifications = notifications;

    public async Task Handle(MarkAsReadCommand request, CancellationToken ct)
    {
        var notification = await _notifications.GetByIdAsync(request.NotificationId, ct)
            ?? throw new NotFoundException("Notification", request.NotificationId);

        if (notification.UserId != request.UserId)
            throw new ForbiddenException();

        notification.MarkAsRead();
        await _notifications.UpdateAsync(notification, ct);
    }
}