namespace SafeDeal.Application.Common.Models;

public record PagedResult<T>(
    IEnumerable<T> Data,
    int CurrentPage,
    int LastPage,
    int Total);

/// <summary>
/// Bornes de pagination communes. Les valeurs hors bornes sont ramenees dans le
/// domaine autorise plutot que rejetees : un client qui demandait la page 0
/// recevait auparavant une erreur serveur, il doit maintenant recevoir la
/// premiere page, sans nouveau code d'erreur dans le contrat.
/// </summary>
public static class Paging
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    public static int NormalizePage(int page) => page < 1 ? 1 : page;

    public static int NormalizePageSize(int pageSize)
        => pageSize < 1 ? DefaultPageSize
            : pageSize > MaxPageSize ? MaxPageSize
            : pageSize;
}
