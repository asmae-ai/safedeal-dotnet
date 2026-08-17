using MediatR;

namespace SafeDeal.Application.Auth.Commands.UpdateProfile;

public record UpdateProfileCommand(
    int UserId,
    string? Name,
    string? Phone) : IRequest;
