using MediatR;
using SafeDeal.Application.Notifications.DTOs;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Notifications.Queries.GetNotifications;

public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, IEnumerable<NotificationDto>>
{
    private readonly INotificationRepository _notifications;
    public GetNotificationsQueryHandler(INotificationRepository notifications) => _notifications = notifications;

    public async Task<IEnumerable<NotificationDto>> Handle(GetNotificationsQuery request, CancellationToken ct)
    {
        var notifications = await _notifications.GetByUserIdAsync(request.UserId, ct);
        return notifications.Select(n => new NotificationDto(
            n.Id, n.Message,
            n.ReadAt?.ToString("o"),
            n.CreatedAt.ToString("o")));
    }
}