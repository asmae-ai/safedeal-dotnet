namespace SafeDeal.Tests.Integration;

/// <summary>
/// Toutes les classes de test partagent une seule API et un seul couple de
/// conteneurs. Deux instances demarraient sinon deux seeders concurrents qui se
/// disputaient les memes fichiers de seed, et doublaient le temps de demarrage.
/// </summary>
[CollectionDefinition(Name)]
public class SafeDealCollection : ICollectionFixture<SafeDealFactory>
{
    public const string Name = "safedeal";
}
