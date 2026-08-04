namespace SafeDeal.Domain.Interfaces.Services;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream fileStream, string fileName, string folder, CancellationToken ct = default);
    Task DeleteAsync(string filePath, CancellationToken ct = default);
    bool IsValidExtension(string fileName, string[] allowedExtensions);
    bool IsValidSize(long sizeInBytes, long maxSizeInBytes);
}