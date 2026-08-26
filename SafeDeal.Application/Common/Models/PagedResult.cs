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

    /// <summary>
    /// Derniere page d'un total donne. Une liste vide compte une page, pas zero :
    /// « page 1 sur 0 » n'a pas de sens a l'ecran, et les clients qui bornent
    /// leur pagination sur last_page se retrouvaient sans page valide.
    /// </summary>
    public static int LastPage(int total, int pageSize)
        => total == 0 ? 1 : (int)Math.Ceiling(total / (double)NormalizePageSize(pageSize));
}

/// <summary>
/// Pagination facultative, pour les listes que le client lit encore en entier.
/// Sans parametre <c>page</c>, la reponse reste celle d'avant — la liste
/// complete — et le client qui l'ignore n'est pas casse. Des qu'une page est
/// demandee, la liste est decoupee et <c>meta</c> renseigne la navigation.
/// </summary>
public interface IOptionallyPagedQuery
{
    int? Page { get; }
    int PageSize { get; }
}

public static class OptionalPaging
{
    public static bool IsPaginated(this IOptionallyPagedQuery query) => query.Page.HasValue;

    public static int SafePage(this IOptionallyPagedQuery query) => Paging.NormalizePage(query.Page ?? 1);

    public static int SafePageSize(this IOptionallyPagedQuery query) => Paging.NormalizePageSize(query.PageSize);

    /// <summary>Applique la tranche demandee, ou laisse la requete entiere.</summary>
    public static IQueryable<T> Slice<T>(this IQueryable<T> source, IOptionallyPagedQuery query)
        => query.IsPaginated()
            ? source.Skip((query.SafePage() - 1) * query.SafePageSize()).Take(query.SafePageSize())
            : source;

    public static PagedResult<T> ToResult<T>(this List<T> items, IOptionallyPagedQuery query, int total)
        => new(
            items,
            query.SafePage(),
            query.IsPaginated() ? Paging.LastPage(total, query.SafePageSize()) : 1,
            total);
}
