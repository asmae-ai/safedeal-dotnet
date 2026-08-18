using MediatR;
using Microsoft.AspNetCore.Http;

namespace SafeDeal.Application.Auth.Commands.UploadAvatar;

public record UploadAvatarCommand(int UserId, IFormFile File, string UploadPath) : IRequest<string>;
