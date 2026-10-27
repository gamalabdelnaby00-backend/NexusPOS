using System;
using System.Collections.Generic;
using System.Text;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.DTOs.Sales;
using System.Linq;

namespace UniversalPOS.Application.Sales;

public class GetSaleDetailsService
{
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    public GetSaleDetailsService(ISaleRepository saleRepository,IProductRepository productRepository)
    {
        _saleRepository = saleRepository;
        _productRepository = productRepository;
    }
    public async Task<SaleDetailsDto> ExecuteAsync(int saleId)
    {
        var sale = await _saleRepository.GetByIdAsync(saleId);
        if (sale is null)
            throw new InvalidOperationException("Sale not found.");

        var productIds = sale.Items.Select(item => item.ProductId).Distinct().ToList();

        var products = await _productRepository.GetByIdsAsync(productIds);

        var productsById = products.ToDictionary(product => product.Id);

        var items = sale.Items.Select(item =>
        {
            productsById.TryGetValue(item.ProductId, out var product);

            return new SaleItemDto
            {
                ProductId = item.ProductId,
                ProductName = product?.Name ?? "Unknown Product",
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                TotalPrice = item.TotalPrice
            };
        }).ToList();

        return new SaleDetailsDto
        {
            Id = sale.Id,
            SaleDate = sale.SaleDate,
            Status = sale.Status,
            PaymentStatus = sale.PaymentStatus,
            TotalAmount = sale.TotalAmount,
            PaidAmount = sale.PaidAmount,
            Items = items
        };
    }

}

