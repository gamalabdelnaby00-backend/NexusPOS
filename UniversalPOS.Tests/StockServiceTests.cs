
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Stock;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;
using Xunit;

namespace UniversalPOS.Tests;

public class StockServiceTests
{
    // 1. زيادة المخزون
    [Fact]
    public async Task IncreaseAsync_WithValidInput_ShouldIncreaseBalance()
    {
        var repository = CreateRepository();
        var service = new StockService(repository);

        await service.IncreaseAsync(
            1, 5m, StockMovementType.PurchaseReceipt,
            "Purchase", "100", "Test purchase", 1);

        var balance = await repository.GetBalanceAsync(1);

        Assert.NotNull(balance);
        Assert.Equal(5m, balance.Quantity);
    }

    // 2. صرف كمية صحيحة
    [Fact]
    public async Task DecreaseAsync_WithValidQuantity_ShouldDecreaseBalance()
    {
        var repository = CreateRepository();
        var service = new StockService(repository);

        await service.IncreaseAsync(
            1, 10m, StockMovementType.PurchaseReceipt,
            "Purchase", "101", "Test purchase", 1);

        await service.DecreaseAsync(
            1, 4m, StockMovementType.Sale,
            "Sale", "200", "Test sale", 1);

        var balance = await repository.GetBalanceAsync(1);

        Assert.NotNull(balance);
        Assert.Equal(6m, balance.Quantity);
    }

    // 3. منع صرف كمية أكبر من الرصيد
    [Fact]
    public async Task DecreaseAsync_WithInsufficientStock_ShouldThrowException()
    {
        var repository = CreateRepository();
        var service = new StockService(repository);

        await service.IncreaseAsync(
            1, 3m, StockMovementType.PurchaseReceipt,
            "Purchase", "102", "Test purchase", 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DecreaseAsync(
                1, 5m, StockMovementType.Sale,
                "Sale", "201", "Test sale", 1));

        var balance = await repository.GetBalanceAsync(1);
        Assert.NotNull(balance);
        Assert.Equal(3m, balance.Quantity);

        Assert.Single(repository.Movements);
    }

    // 4. زيادة بكمية كسرية
    [Fact]
    public async Task IncreaseAsync_WithFractionalQuantity_ShouldIncreaseBalance()
    {
        var repository = CreateRepository();
        var service = new StockService(repository);

        await service.IncreaseAsync(
            1, 2.750m, StockMovementType.PurchaseReceipt,
            "Purchase", "103", "Weight purchase", 1);

        var balance = await repository.GetBalanceAsync(1);

        Assert.NotNull(balance);
        Assert.Equal(2.750m, balance.Quantity);
    }

    // 5. صرف بكمية كسرية
    [Fact]
    public async Task DecreaseAsync_WithFractionalQuantity_ShouldDecreaseBalance()
    {
        var repository = CreateRepository();
        var service = new StockService(repository);

        await service.IncreaseAsync(
            1, 5.500m, StockMovementType.PurchaseReceipt,
            "Purchase", "104", "Weight purchase", 1);

        await service.DecreaseAsync(
            1, 1.250m, StockMovementType.Sale,
            "Sale", "202", "Weight sale", 1);

        var balance = await repository.GetBalanceAsync(1);

        Assert.NotNull(balance);
        Assert.Equal(4.250m, balance.Quantity);
    }

    // 6. رفض منتج غير موجود عند الزيادة
    [Fact]
    public async Task IncreaseAsync_WithUnknownProduct_ShouldThrowException()
    {
        var repository = new FakeStockRepository();
        var service = new StockService(repository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.IncreaseAsync(
                999, 5m, StockMovementType.PurchaseReceipt,
                "Purchase", "105", "Test purchase", 1));
    }

    // 7. رفض منتج غير موجود عند الصرف
    [Fact]
    public async Task DecreaseAsync_WithUnknownProduct_ShouldThrowException()
    {
        var repository = new FakeStockRepository();
        var service = new StockService(repository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.DecreaseAsync(
                999, 1m, StockMovementType.Sale,
                "Sale", "203", "Test sale", 1));
    }

    // 8. رفض كمية صفر عند الزيادة
    [Fact]
    public async Task IncreaseAsync_WithZeroQuantity_ShouldThrowException()
    {
        var repository = CreateRepository();
        var service = new StockService(repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.IncreaseAsync(
                1, 0m, StockMovementType.PurchaseReceipt,
                "Purchase", "106", "Test purchase", 1));
    }

    // 9. رفض كمية سالبة عند الصرف
    [Fact]
    public async Task DecreaseAsync_WithNegativeQuantity_ShouldThrowException()
    {
        var repository = CreateRepository();
        var service = new StockService(repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.DecreaseAsync(
                1, -2m, StockMovementType.Sale,
                "Sale", "204", "Test sale", 1));
    }

    // 10. رفض أكثر من 3 منازل عشرية
    [Fact]
    public async Task IncreaseAsync_WithMoreThanThreeDecimalPlaces_ShouldThrowException()
    {
        var repository = CreateRepository();
        var service = new StockService(repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.IncreaseAsync(
                1, 1.2345m, StockMovementType.PurchaseReceipt,
                "Purchase", "107", "Test purchase", 1));
    }

    // 11. رفض رقم منتج غير صحيح
    [Fact]
    public async Task IncreaseAsync_WithInvalidProductId_ShouldThrowException()
    {
        var repository = new FakeStockRepository();
        var service = new StockService(repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.IncreaseAsync(
                0, 5m, StockMovementType.PurchaseReceipt,
                "Purchase", "108", "Test purchase", 1));
    }

    // 12. رفض رقم مستخدم غير صحيح
    [Fact]
    public async Task IncreaseAsync_WithInvalidUserId_ShouldThrowException()
    {
        var repository = CreateRepository();
        var service = new StockService(repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.IncreaseAsync(
                1, 5m, StockMovementType.PurchaseReceipt,
                "Purchase", "109", "Test purchase", 0));
    }

    // 13. تسجيل حركة شراء موجبة
    [Fact]
    public async Task IncreaseAsync_ShouldRecordPositiveStockMovement()
    {
        var repository = CreateRepository();
        var service = new StockService(repository);

        await service.IncreaseAsync(
            1, 5m, StockMovementType.PurchaseReceipt,
            "Purchase", "500", "Received purchase", 1);

        var movement = Assert.Single(repository.Movements);

        Assert.Equal(1, movement.ProductId);
        Assert.Equal(StockMovementType.PurchaseReceipt, movement.Type);
        Assert.Equal(5m, movement.QuantityChange);
        Assert.Equal("Purchase", movement.ReferenceType);
        Assert.Equal("500", movement.ReferenceId);
    }

    // 14. تسجيل حركة بيع سالبة
    [Fact]
    public async Task DecreaseAsync_ShouldRecordNegativeStockMovement()
    {
        var repository = CreateRepository();
        var service = new StockService(repository);

        await service.IncreaseAsync(
            1, 10m, StockMovementType.PurchaseReceipt,
            "Purchase", "501", "Received purchase", 1);

        await service.DecreaseAsync(
            1, 3m, StockMovementType.Sale,
            "Sale", "601", "Sold products", 1);

        Assert.Equal(2, repository.Movements.Count);

        var saleMovement = repository.Movements[1];

        Assert.Equal(StockMovementType.Sale, saleMovement.Type);
        Assert.Equal(-3m, saleMovement.QuantityChange);
        Assert.Equal("Sale", saleMovement.ReferenceType);
        Assert.Equal("601", saleMovement.ReferenceId);
    }

    // 15. التأكد من عدم تغيير الرصيد عند رفض الصرف
    [Fact]
    public async Task DecreaseAsync_WhenRejected_ShouldNotChangeBalance()
    {
        var repository = CreateRepository();
        var service = new StockService(repository);

        await service.IncreaseAsync(
            1, 4m, StockMovementType.PurchaseReceipt,
            "Purchase", "502", "Received purchase", 1);

        var movementCountBefore = repository.Movements.Count;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DecreaseAsync(
                1, 8m, StockMovementType.Sale,
                "Sale", "602", "Overselling attempt", 1));

        var balance = await repository.GetBalanceAsync(1);

        Assert.NotNull(balance);
        Assert.Equal(4m, balance.Quantity);
        Assert.Equal(movementCountBefore, repository.Movements.Count);
    }

    private static FakeStockRepository CreateRepository()
    {
        var repository = new FakeStockRepository();
        repository.AddProduct(1);
        return repository;
    }
}