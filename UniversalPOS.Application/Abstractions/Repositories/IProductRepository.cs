using System;
using System.Collections.Generic;
using System.Text;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Application.Products;

namespace UniversalPOS.Application.Abstractions.Repositories
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(int id);
        Task<Product?> GetByBarcodeAsync(string barcode);
        Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids);
        Task<IReadOnlyList<Product>> GetAllAsync();
        Task<IReadOnlyList<ProductListItemDto>> GetAllLocalizedAsync(string languageCode);
    }
}
