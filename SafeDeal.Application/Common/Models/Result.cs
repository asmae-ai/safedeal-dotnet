namespace SafeDeal.Application.Common.Models;

public record Result<T>(bool Success, T? Data, string? Message = null);

public record Result(bool Success, string? Message = null)
{
    public static Result Ok(string? message = null) => new(true, message);
    public static Result Fail(string message) => new(false, message);
    public static Result<T> Ok<T>(T data, string? message = null) => new(true, data, message);
    public static Result<T> Fail<T>(string message) => new(false, default, message);
}