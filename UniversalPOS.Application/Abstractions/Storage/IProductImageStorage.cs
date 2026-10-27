namespace UniversalPOS.Application.Abstractions.Storage;

/// <summary>Stores product images outside SQL Server. The returned key is safe to persist in Product.ImageKey.</summary>
public interface IProductImageStorage
{
    Task<string> SaveAsync(int productId, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string imageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string imageKey, CancellationToken cancellationToken = default);
}
