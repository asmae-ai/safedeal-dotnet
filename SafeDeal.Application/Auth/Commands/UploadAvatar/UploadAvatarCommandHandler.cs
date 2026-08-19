using MediatR;
using Microsoft.AspNetCore.Http;
using SafeDeal.Application.Common.Exceptions;
using SafeDeal.Domain.Interfaces.Repositories;

namespace SafeDeal.Application.Auth.Commands.UploadAvatar;

public class UploadAvatarCommandHandler : IRequestHandler<UploadAvatarCommand, string>
{
    private readonly IUserRepository _users;

    public UploadAvatarCommandHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<string> Handle(UploadAvatarCommand request, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException("User", request.UserId);

        var ext = Path.GetExtension(request.File.FileName).ToLower();
        if (ext is not ".jpg" and not ".jpeg" and not ".png")
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["file"] = ["Only jpg and png files are allowed."]
            });

        var folder = Path.Combine(request.UploadPath, "avatars");
        Directory.CreateDirectory(folder);

        var fileName = $"{user.Id}_{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        using var stream = new FileStream(fullPath, FileMode.Create);
        await request.File.CopyToAsync(stream, ct);

        var relativePath = "uploads/avatars/" + fileName;
        user.UpdateAvatar(relativePath);
        await _users.UpdateAsync(user, ct);

        return relativePath;
    }
}
