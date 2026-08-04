namespace SafeDeal.Application.Common.Models;

public record PagedResult<T>(
    IEnumerable<T> Data,
    int CurrentPage,
    int LastPage,
    int Total);