namespace SafeDeal.Application.Auth.DTOs;

public record AuthResponseDto(string Token, UserDto User);