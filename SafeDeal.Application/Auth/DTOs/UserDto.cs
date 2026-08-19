namespace SafeDeal.Application.Auth.DTOs;

public record UserDto(
    int Id,
    string Name,
    string Email,
    string Role,
    string? Phone,
    string IdentityStatus,
    string ReputationScore,
    string CreatedAt,
    string? AvatarPath);
