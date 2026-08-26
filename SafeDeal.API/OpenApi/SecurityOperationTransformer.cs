using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

namespace SafeDeal.API.OpenApi;

/// <summary>
/// Reporte sur chaque opération ce que ses attributs disent déjà : le jeton
/// attendu, le rôle exigé, la limite de débit, et les réponses d'erreur qui en
/// découlent.
///
/// La source est la politique réellement appliquée par le pipeline, pas une
/// annotation parallèle : une documentation recopiée à la main finit toujours
/// par mentir sur qui a le droit d'appeler quoi.
/// </summary>
public sealed class SecurityOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        operation.Responses ??= new OpenApiResponses();

        DescribeAuthorization(operation, context, metadata);
        DescribeRateLimit(operation, metadata);
        DescribeValidation(operation, context);

        return Task.CompletedTask;
    }

    private static void DescribeAuthorization(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        IList<object> metadata)
    {
        // AllowAnonymous l'emporte sur un Authorize porté par le contrôleur.
        if (metadata.OfType<IAllowAnonymous>().Any()) return;

        var authorize = metadata.OfType<AuthorizeAttribute>().ToList();
        if (authorize.Count == 0) return;

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SafeDealDocumentTransformer.BearerScheme, context.Document)] = []
            }
        ];

        operation.Responses!.TryAdd("401", new OpenApiResponse
        {
            Description = "Jeton absent, expiré, ou révoqué par une déconnexion."
        });

        var roles = authorize
            .Select(a => a.Roles)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .SelectMany(r => r!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct()
            .ToList();

        operation.Responses.TryAdd("403", new OpenApiResponse
        {
            Description = roles.Count > 0
                ? $"Rôle insuffisant : réservé à {string.Join(" ou ", roles)}."
                : "Droit insuffisant : la ressource appartient à un autre compte."
        });

        if (roles.Count > 0)
        {
            operation.Description = Append(
                operation.Description,
                $"Réservé au rôle **{string.Join("** ou **", roles)}**.");
        }
    }

    /// <summary>
    /// Une politique de débit change ce que le client doit prévoir : le 429
    /// fait partie du contrat, au même titre qu'un 422.
    /// </summary>
    private static void DescribeRateLimit(OpenApiOperation operation, IList<object> metadata)
    {
        var policy = metadata.OfType<EnableRateLimitingAttribute>().FirstOrDefault()?.PolicyName;
        if (policy is null) return;

        operation.Responses!.TryAdd("429", new OpenApiResponse
        {
            Description = $"Limite de débit « {policy} » atteinte. Réessayer après la fenêtre en cours (une minute)."
        });

        operation.Description = Append(
            operation.Description,
            $"Soumis à la limite de débit « {policy} », ajustable par configuration (`RateLimiting:{policy}`).");
    }

    /// <summary>
    /// Toute écriture passe par FluentValidation puis par les règles du domaine,
    /// qui sortent l'une comme l'autre en 422.
    /// </summary>
    private static void DescribeValidation(OpenApiOperation operation, OpenApiOperationTransformerContext context)
    {
        var method = context.Description.HttpMethod;
        if (method is not ("POST" or "PUT" or "PATCH")) return;

        operation.Responses!.TryAdd("422", new OpenApiResponse
        {
            Description =
                "Validation refusée ou règle métier violée. Le corps porte `message`, " +
                "et `errors` détaille les champs fautifs quand la validation est en cause."
        });
    }

    private static string Append(string? existing, string addition)
        => string.IsNullOrWhiteSpace(existing) ? addition : $"{existing}\n\n{addition}";
}
