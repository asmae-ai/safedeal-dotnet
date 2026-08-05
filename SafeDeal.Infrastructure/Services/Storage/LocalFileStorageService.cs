using SafeDeal.Domain.Interfaces.Services;
using Microsoft.Extensions.Configuration;

namespace SafeDeal.Infrastructure.Services.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;

    public LocalFileStorageService(IConfiguration configuration)
        => _basePath = configuration["Storage:BasePath"] ?? "uploads";

    public async Task<string> SaveAsync(Stream fileStream, string fileName, string folder, CancellationToken ct = default)
    {
        var directory = Path.Combine(_basePath, folder);
        Directory.CreateDirectory(directory);

        var uniqueName = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
        var filePath = Path.Combine(directory, uniqueName);

        await using var stream = File.Create(filePath);
        await fileStream.CopyToAsync(stream, ct);

        return filePath;
    }

    public Task DeleteAsync(string filePath, CancellationToken ct = default)
    {
        if (File.Exists(filePath)) File.Delete(filePath);
        return Task.CompletedTask;
    }

    public bool IsValidExtension(string fileName, string[] allowedExtensions)
        => allowedExtensions.Contains(Path.GetExtension(fileName).ToLower());

    public bool IsValidSize(long sizeInBytes, long maxSizeInBytes)
        => sizeInBytes <= maxSizeInBytes;
}