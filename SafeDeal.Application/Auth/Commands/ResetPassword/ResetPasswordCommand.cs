using MediatR;

namespace SafeDeal.Application.Auth.Commands.ResetPassword;

public record ResetPasswordCommand(
    string Email,
    string Token,
    string Password,
    string PasswordConfirmation) : IRequest;