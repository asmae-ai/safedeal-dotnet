namespace SafeDeal.Application.Admin.DTOs;

public record AdminUserDto(
    int Id,
    string Name,
    string Email,
    string Role,
    string IdentityStatus,
    string ReputationScore,
    bool IsEmailVerified,
    bool IsActive,
    string CreatedAt);

public record AdminVerificationDto(
    int Id,
    int UserId,
    string UserName,
    string UserEmail,
    string DocumentType,
    string Status,
    string SubmittedAt,
    string DocumentFrontPath,
    string SelfiePath);