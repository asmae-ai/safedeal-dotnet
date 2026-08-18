using MediatR;
using SafeDeal.Application.Notifications.DTOs;

namespace SafeDeal.Application.Notifications.Queries.GetNotifications;

public record GetNotificationsQuery(int UserId) : IRequest<List<NotificationDto>>;
