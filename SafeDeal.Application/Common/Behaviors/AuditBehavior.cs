using MediatR;
using SafeDeal.Application.Common.Audit;

namespace SafeDeal.Application.Common.Behaviors;

/// <summary>
/// Trace les commandes sensibles au passage, succès comme échec, sans que les
/// handlers aient à s'en préoccuper. Une commande qui échoue est aussi une
/// information d'audit : c'est même souvent la plus intéressante.
/// </summary>
public class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditLogger _audit;

    public AuditBehavior(IAuditLogger audit) => _audit = audit;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var descriptor = AuditRegistry.For(typeof(TRequest));
        if (descriptor is null) return await next();

        TResponse response;
        try
        {
            response = await next();
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(BuildEntry(
                request, descriptor, null, succeeded: false, failureReason: ex.Message), ct);
            throw;
        }

        await _audit.LogAsync(BuildEntry(request, descriptor, response, succeeded: true), ct);
        return response;
    }

    private static AuditEntry BuildEntry(
        TRequest request,
        AuditDescriptor descriptor,
        object? response,
        bool succeeded,
        string? failureReason = null)
    {
        // L'identifiant d'entité est plus fiable dans la réponse (une création
        // ne connaît son id qu'après coup) ; on retombe sur la requête sinon.
        var entityId = ReadInt(response, AuditRegistry.EntityIdProperties)
                       ?? ReadInt(request, AuditRegistry.EntityIdProperties);

        return new AuditEntry(
            descriptor.Action,
            UserId: ReadInt(request, AuditRegistry.UserIdProperties),
            Subject: descriptor.SubjectProperty is null
                ? null
                : ReadString(request, descriptor.SubjectProperty),
            EntityType: descriptor.EntityType,
            EntityId: entityId,
            Succeeded: succeeded,
            FailureReason: failureReason);
    }

    private static int? ReadInt(object? source, string[] candidates)
    {
        if (source is null) return null;

        foreach (var name in candidates)
        {
            var value = source.GetType().GetProperty(name)?.GetValue(source);
            if (value is int i and > 0) return i;
        }
        return null;
    }

    private static string? ReadString(object? source, string propertyName)
        => source?.GetType().GetProperty(propertyName)?.GetValue(source) as string;
}
