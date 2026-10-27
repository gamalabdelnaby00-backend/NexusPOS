using UniversalPOS.Application.Abstractions.Repositories;

namespace UniversalPOS.Application.Sales;

public class AddProductToSaleService
{
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;

    public AddProductToSaleService(
        ISaleRepository saleRepository,
        IProductRepository productRepository)
    {
        _saleRepository = saleRepository;
        _productRepository = productRepository;
    }

    public async Task ExecuteAsync(
        int saleId,
        string barcode,
        decimal quantity)
    {
        if (saleId <= 0)
            throw new ArgumentOutOfRangeException(nameof(saleId));

        if (string.IsNullOrWhiteSpace(barcode))
            throw new ArgumentException(
                "Barcode is required.",
                nameof(barcode));

        if (quantity <= 0m)
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Quantity must be greater than zero.");

        if (decimal.Round(quantity, 3) != quantity)
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Quantity cannot have more than 3 decimal places.");

        var sale = await _saleRepository.GetByIdAsync(saleId);

        if (sale is null)
            throw new InvalidOperationException("Sale not found.");

        var product =
            await _productRepository.GetByBarcodeAsync(barcode.Trim());

        if (product is null)
            throw new InvalidOperationException("Product not found.");

        sale.AddProduct(product, quantity);

        await _saleRepository.UpdateAsync(sale);
    }
}
