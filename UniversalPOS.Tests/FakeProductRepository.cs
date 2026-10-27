
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Products;
using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Tests;

public class FakeProductRepository : IProductRepository
{
    private readonly List<Product> _products = new();
    private int _nextId = 1;

    public void Add(Product product)
    {
        if (product is null)
            throw new ArgumentNullException(nameof(product));

        if (_products.Any(p => p.Barcode == product.Barcode))
            throw new InvalidOperationException("Product barcode already exists.");

        // Simulate database-generated product IDs.
        if (product.Id == 0)
        {
            typeof(Product)
                .GetProperty(nameof(Product.Id))!
                .SetValue(product, _nextId++);
        }
        else
        {
            if (_products.Any(p => p.Id == product.Id))
                throw new InvalidOperationException("Product ID already exists.");

            _nextId = Math.Max(_nextId, product.Id + 1);
        }

        _products.Add(product);
    }

    public Task<Product?> GetByIdAsync(int id)
    {
        Product? product = _products.FirstOrDefault(p => p.Id == id);
        return Task.FromResult(product);
    }

    public Task<Product?> GetByBarcodeAsync(string barcode)
    {
        Product? product = _products.FirstOrDefault(
            p => p.Barcode == barcode);

        return Task.FromResult(product);
    }

    public Task<IReadOnlyList<Product>> GetByIdsAsync(
        IReadOnlyCollection<int> ids)
    {
        IReadOnlyList<Product> products = _products
            .Where(p => ids.Contains(p.Id))
            .ToList();

        return Task.FromResult(products);
    }

    public Task<IReadOnlyList<Product>> GetAllAsync()
    {
        IReadOnlyList<Product> products = _products.ToList();
        return Task.FromResult(products);
    }
    public Task<IReadOnlyList<ProductListItemDto>> GetAllLocalizedAsync(
    string languageCode)
    {
        IReadOnlyList<ProductListItemDto> products =
            Array.Empty<ProductListItemDto>();

        return Task.FromResult(products);
    }
}
