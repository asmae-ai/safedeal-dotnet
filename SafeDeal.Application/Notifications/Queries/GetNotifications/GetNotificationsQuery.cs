using MediatR;
using SafeDeal.Application.Common.Models;
using SafeDeal.Application.Notifications.DTOs;

namespace SafeDeal.Application.Notifications.Queries.GetNotifications;

/// <param name="Page">Absente, la liste complète est rendue comme avant.</param>
public record GetNotificationsQuery(int UserId, int? Page = null, int PageSize = 20)
    : IRequest<PagedResult<NotificationDto>>, IOptionallyPagedQuery;
