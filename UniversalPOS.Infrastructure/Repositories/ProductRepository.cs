using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Infrastructure.Data;
using UniversalPOS.Application.Products;

namespace UniversalPOS.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _dbContext;

    public ProductRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Product?> GetByBarcodeAsync(string barcode)
    {
        return await _dbContext.Products.FirstOrDefaultAsync(p => p.Barcode == barcode);
    }

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(
        IReadOnlyCollection<int> ids)
    {
        return await _dbContext.Products.Where(p => ids.Contains(p.Id)).ToListAsync();
    }
    public async Task<IReadOnlyList<Product>> GetAllAsync()
    {
        return await _dbContext.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
    }

    public async Task<IReadOnlyList<ProductListItemDto>> GetAllLocalizedAsync(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("Language code is required.",nameof(languageCode));

        languageCode = languageCode.Trim();

        var products = await _dbContext.Products.AsNoTracking().Where(p => p.IsActive)
            .Select(p => new ProductListItemDto
            {
                Id = p.Id,

                Name = _dbContext.ProductTranslations.Where(t => t.ProductId == p.Id && t.LanguageCode == languageCode).Select(t => t.Name).FirstOrDefault() ?? p.Name,

                Price = p.Price,
                Barcode = p.Barcode,
                ImageKey = p.ImageKey,
                CategoryId = p.CategoryId,

                CategoryName = _dbContext.CategoryTranslations.Where(t => t.CategoryId == p.CategoryId && t.LanguageCode == languageCode).Select(t => t.Name)
                    .FirstOrDefault() ?? _dbContext.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault() ?? string.Empty,
                UnitOfMeasure = p.UnitOfMeasure
            }).OrderBy(p => p.Name).ToListAsync();
        return products;
    }
}
