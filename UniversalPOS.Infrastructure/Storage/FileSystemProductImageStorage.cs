using UniversalPOS.Application.Abstractions.Storage;

namespace UniversalPOS.Infrastructure.Storage;

/// <summary>Development adapter only. Production deployments should use private cloud object storage.</summary>
public sealed class FileSystemProductImageStorage : IProductImageStorage
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private readonly string _root;

    public FileSystemProductImageStorage(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("Image storage root is required.", nameof(root));
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(int productId, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        if (productId <= 0) throw new ArgumentOutOfRangeException(nameof(productId));
        ArgumentNullException.ThrowIfNull(content);
        var extension = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new ArgumentException("Only JPEG, PNG, and WebP images are supported.", nameof(contentType))
        };

        var relativeKey = $"products/{productId}/{Guid.NewGuid():N}{extension}";
        var target = ResolvePath(relativeKey);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxImageBytes)
            {
                output.Close();
                File.Delete(target);
                throw new InvalidOperationException("Product image exceeds the 5 MB limit.");
            }
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        if (total == 0)
        {
            output.Close();
            File.Delete(target);
            throw new InvalidOperationException("The uploaded image is empty.");
        }
        return relativeKey;
    }

    public Task<Stream?> OpenReadAsync(string imageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(imageKey);
        Stream? stream = File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true) : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string imageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(imageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || Path.IsPathRooted(key) || key.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Invalid relative image key.", nameof(key));
        var normalized = key.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_root, normalized));
        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Image key resolves outside the configured storage root.", nameof(key));
        return fullPath;
    }
}
