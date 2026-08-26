using MediatR;
using SafeDeal.Application.Common.Models;
using SafeDeal.Application.Notifications.DTOs;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Notifications.Queries.GetNotifications;

public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, PagedResult<NotificationDto>>
{
    private readonly INotificationRepository _notifications;

    public GetNotificationsQueryHandler(INotificationRepository notifications)
    {
        _notifications = notifications;
    }

    public async Task<PagedResult<NotificationDto>> Handle(GetNotificationsQuery request, CancellationToken ct)
    {
        var (notifications, total) = await _notifications.GetByUserIdAsync(
            request.UserId,
            request.IsPaginated() ? request.SafePage() : null,
            request.SafePageSize(),
            ct);

        var dtos = notifications.Select(n => new NotificationDto(
            n.Id,
            n.Message,
            n.Type.ToString().ToLower(),
            n.TransactionId,
            n.IsRead,
            n.CreatedAt.ToString("o"))).ToList();

        return dtos.ToResult(request, total);
    }
}
