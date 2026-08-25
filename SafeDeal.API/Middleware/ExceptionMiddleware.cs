using System.Text.Json;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Exceptions;

namespace SafeDeal.API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    /// <summary>
    /// Les clés du dictionnaire d'erreurs suivent la même convention que le reste
    /// du contrat. Sans DictionaryKeyPolicy, FluentValidation renvoyait "Email"
    /// quand le client lit "email" : les erreurs de champ n'atteignaient jamais
    /// l'interface.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        int statusCode;
        object response;

        switch (exception)
        {
            case ValidationException ex:
                statusCode = 422;
                response = new { message = "Validation failed.", errors = ex.Errors };
                break;

            case NotFoundException ex:
                statusCode = 404;
                response = new { message = ex.Message };
                break;

            case UnauthorizedException ex:
                statusCode = 401;
                response = new { message = ex.Message };
                break;

            case ForbiddenException ex:
                statusCode = 403;
                response = new { message = ex.Message };
                break;

            // Le drapeau permet au client de rediriger vers l'ecran de verification
            // plutot que d'afficher une erreur generique.
            case EmailNotVerifiedException ex:
                statusCode = 403;
                response = new { message = ex.Message, email_verified = false };
                break;

            case UnauthorizedDomainException ex:
                statusCode = 403;
                response = new { message = ex.Message };
                break;

            // Couvre BusinessRuleException, InvalidTransitionException et toute
            // future règle métier : une violation du domaine est une 422, jamais une 500.
            case DomainException ex:
                statusCode = 422;
                response = new { message = ex.Message };
                break;

            default:
                statusCode = 500;
                response = new { message = "An unexpected error occurred." };
                _logger.LogError(exception, "Unhandled exception");
                break;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, SerializerOptions));
    }
}