using System;
using System.Threading.Tasks;
using UniversalPOS.Application.Abstractions;
using UniversalPOS.Application.Sales;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;
using Xunit;

namespace UniversalPOS.Tests;

public class CompleteSaleServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ValidSale_CompletesSaleAndDeductsStock()
    {
        var saleRepository = new FakeSaleRepository();
        var stockRepository = new FakeStockRepository();
        stockRepository.AddProduct(1);

        await stockRepository.AddBalanceAsync(new StockBalance(1, 10m));

        var sale = CreatePaidSale(quantity: 2m);
        await saleRepository.AddAsync(sale);

        var service = CreateService(saleRepository, stockRepository);

        await service.ExecuteAsync(sale.Id);

        Assert.Equal(SaleReturnStatus.Completed, sale.Status);
        Assert.Equal(8m, (await stockRepository.GetBalanceAsync(1))!.Quantity);
        Assert.Single(stockRepository.Movements);
        Assert.Equal(-2m, stockRepository.Movements[0].QuantityChange);
        Assert.Equal(1, stockRepository.UpdateBalanceCallCount);
        Assert.Equal(1, saleRepository.UpdateCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidSaleId_ThrowsException()
    {
        var saleRepository = new FakeSaleRepository();
        var stockRepository = new FakeStockRepository();
        var service = CreateService(saleRepository, stockRepository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.ExecuteAsync(0));
    }

    [Fact]
    public async Task ExecuteAsync_SaleDoesNotExist_ThrowsException()
    {
        var saleRepository = new FakeSaleRepository();
        var stockRepository = new FakeStockRepository();
        var service = CreateService(saleRepository, stockRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteAsync(999));
    }

    [Fact]
    public async Task ExecuteAsync_EmptySale_ThrowsException()
    {
        var saleRepository = new FakeSaleRepository();
        var stockRepository = new FakeStockRepository();
        var sale = new Sale();

        sale.AssignContext(1, 1, 1, Guid.NewGuid());
        await saleRepository.AddAsync(sale);

        var service = CreateService(saleRepository, stockRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteAsync(sale.Id));

        Assert.Equal(SaleReturnStatus.Draft, sale.Status);
        Assert.Empty(stockRepository.Movements);
    }

    [Fact]
    public async Task ExecuteAsync_SaleWithoutUser_ThrowsException()
    {
        var saleRepository = new FakeSaleRepository();
        var stockRepository = new FakeStockRepository();

        var sale = CreatePaidSale(quantity: 1m, assignContext: false);
        await saleRepository.AddAsync(sale);

        var service = CreateService(saleRepository, stockRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteAsync(sale.Id));

        Assert.Equal(SaleReturnStatus.Draft, sale.Status);
        Assert.Empty(stockRepository.Movements);
    }

    [Fact]
    public async Task ExecuteAsync_StockBalanceMissing_ThrowsException()
    {
        var saleRepository = new FakeSaleRepository();
        var stockRepository = new FakeStockRepository();
        stockRepository.AddProduct(1);

        var sale = CreatePaidSale(quantity: 2m);
        await saleRepository.AddAsync(sale);

        var service = CreateService(saleRepository, stockRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteAsync(sale.Id));

        Assert.Equal(SaleReturnStatus.Draft, sale.Status);
        Assert.Empty(stockRepository.Movements);
    }

    [Fact]
    public async Task ExecuteAsync_InsufficientStock_ThrowsException()
    {
        var saleRepository = new FakeSaleRepository();
        var stockRepository = new FakeStockRepository();
        stockRepository.AddProduct(1);

        await stockRepository.AddBalanceAsync(new StockBalance(1, 1m));

        var sale = CreatePaidSale(quantity: 2m);
        await saleRepository.AddAsync(sale);

        var service = CreateService(saleRepository, stockRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteAsync(sale.Id));

        Assert.Equal(SaleReturnStatus.Draft, sale.Status);
        Assert.Equal(1m, (await stockRepository.GetBalanceAsync(1))!.Quantity);
    }

    private static Sale CreatePaidSale(decimal quantity,bool assignContext = true)
    {
        var sale = new Sale();

        if (assignContext)
            sale.AssignContext(1, 1, 1, Guid.NewGuid());

        sale.AddItem(new SaleItem(1, 100m, quantity));
        sale.AddPayment(new Payment(sale.TotalAmount, PaymentMethod.Cash));

        return sale;
    }

    private static CompleteSaleService CreateService(FakeSaleRepository saleRepository,FakeStockRepository stockRepository)
    {
        return new CompleteSaleService(saleRepository,stockRepository,new FakeUnitOfWork());
    }
}
