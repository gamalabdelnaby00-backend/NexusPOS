using System.Collections.Generic;
using System.Threading.Tasks;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Application.Sales;

public class GetProductsService
{
    private readonly IProductRepository _productRepository;

    public GetProductsService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<IReadOnlyList<Product>> ExecuteAsync()
    {
        return await _productRepository.GetAllAsync();
    }
}
