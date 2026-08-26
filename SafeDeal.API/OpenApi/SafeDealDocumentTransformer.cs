using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SafeDeal.API.OpenApi;

/// <summary>
/// Complète le document OpenAPI généré à partir des contrôleurs : identité de
/// l'API, schéma d'authentification, et regroupement des opérations par
/// domaine métier. Rien ici ne touche au comportement des endpoints — la
/// documentation décrit l'API existante, elle ne la modifie pas.
/// </summary>
public sealed class SafeDealDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <summary>Nom du schéma de sécurité, référencé par chaque opération protégée.</summary>
    public const string BearerScheme = "Bearer";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "SafeDeal API",
            Version = "v1",
            Description = Description
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        // Déclaré une seule fois ici ; les opérations s'y réfèrent par son nom.
        document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description =
                "Jeton d'accès obtenu par POST /api/v1/login, à présenter en en-tête " +
                "`Authorization: Bearer <token>`. Sa durée de vie est courte : " +
                "POST /api/v1/auth/refresh délivre le suivant sans redemander le mot de passe."
        };

        document.Tags = Tags;

        return Task.CompletedTask;
    }

    private const string Description = """
        API du service de séquestre SafeDeal.

        ## Parcours

        Un vendeur vérifié crée une transaction et transmet son lien. L'acheteur
        s'y rattache, paie chez Stripe, et les fonds restent bloqués jusqu'à ce
        qu'il confirme la réception. Un litige gèle la transaction et seul un
        administrateur le tranche, en libérant les fonds ou en remboursant.

        ## Authentification

        Toutes les routes exigent un jeton `Bearer`, sauf l'inscription, la
        connexion, la réinitialisation de mot de passe, les webhooks et la
        consultation d'une transaction par son jeton sécurisé.

        ## Forme des réponses

        Les lectures rendent `{ "data": ... }`. Les listes paginées ajoutent
        `{ "meta": { "current_page", "last_page", "total" } }`. Les écritures
        rendent `{ "message": ... }`, accompagné de `data` quand la ressource
        modifiée est utile au client.

        ## Erreurs

        | Code | Signification |
        |------|---------------|
        | 400  | Requête malformée (paramètre non numérique, corps illisible) |
        | 401  | Jeton absent, expiré ou révoqué |
        | 403  | Droit insuffisant : rôle, ou ressource appartenant à autrui |
        | 404  | Ressource inexistante — ou masquée à un appelant non autorisé |
        | 422  | Validation ou règle métier refusée (`errors` détaille les champs) |
        | 429  | Limite de débit atteinte sur un endpoint sensible |

        Le corps d'erreur porte toujours `message`, et `errors` en 422 :
        `{ "message": "Validation failed.", "errors": { "email": ["..."] } }`.
        """;

    private static HashSet<OpenApiTag> Tags =>
    [
        new()
        {
            Name = "Auth",
            Description = "Inscription, connexion, session, 2FA, profil et mot de passe."
        },
        new()
        {
            Name = "Transactions",
            Description = "Cycle de vie du séquestre : création, rattachement, paiement, expédition, livraison, clôture, annulation."
        },
        new()
        {
            Name = "Disputes",
            Description = "Ouverture d'un litige, échanges et pièces jointes entre les deux parties."
        },
        new()
        {
            Name = "Identity",
            Description = "Vérification d'identité du vendeur, requise avant toute création de transaction."
        },
        new()
        {
            Name = "Notifications",
            Description = "Fil de notifications de l'utilisateur connecté."
        },
        new()
        {
            Name = "Dashboard",
            Description = "Agrégats de lecture par rôle. Servis depuis le cache, jamais utilisés pour décider."
        },
        new()
        {
            Name = "Admin",
            Description = "Écrans d'administration : files d'attente, décisions d'identité, arbitrage des litiges, journal d'audit."
        },
        new()
        {
            Name = "Webhooks",
            Description = "Notifications entrantes de Stripe et Sumsub, authentifiées par signature et non par jeton."
        }
    ];
}
