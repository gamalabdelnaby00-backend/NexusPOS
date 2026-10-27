
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Application.Products;

public sealed class ProductListItemDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public string Barcode { get; init; } = string.Empty;

    public string? ImageKey { get; init; }

    public int CategoryId { get; init; }

    public string CategoryName { get; init; } = string.Empty;

    public UnitOfMeasure UnitOfMeasure { get; init; }
}
