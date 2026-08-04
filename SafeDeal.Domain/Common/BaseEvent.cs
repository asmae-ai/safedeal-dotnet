using MediatR;

namespace SafeDeal.Domain.Common;

public abstract class BaseEvent : INotification
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}