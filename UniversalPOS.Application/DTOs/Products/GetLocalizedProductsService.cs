using UniversalPOS.Application.Abstractions.Repositories;

namespace UniversalPOS.Application.Products;

public sealed class GetLocalizedProductsService
{
    private readonly IProductRepository _productRepository;

    public GetLocalizedProductsService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public Task<IReadOnlyList<ProductListItemDto>> ExecuteAsync(string languageCode)
    {
        return _productRepository.GetAllLocalizedAsync(languageCode);
    }
}