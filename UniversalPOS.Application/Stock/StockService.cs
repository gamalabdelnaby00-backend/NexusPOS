
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Application.Stock;

public class StockService
{
    private readonly IStockRepository _stockRepository;

    public StockService(IStockRepository stockRepository)
    {
        _stockRepository = stockRepository;
    }

    public async Task IncreaseAsync(
        int productId,
        decimal quantity,
        StockMovementType type,
        string referenceType,
        string referenceId,
        string reason,
        int userId)
    {
        ValidateInput(productId, quantity, userId);

        if (!await _stockRepository.ProductExistsAsync(productId))
        {
            throw new KeyNotFoundException(
                $"Product with ID {productId} was not found.");
        }

        var balance = await _stockRepository.GetBalanceAsync(productId);

        if (balance is null)
        {
            balance = new StockBalance(productId);
            await _stockRepository.AddBalanceAsync(balance);
        }

        balance.Increase(quantity);

        var movement = new StockMovement(
            productId,
            type,
            quantity,
            referenceType,
            referenceId,
            reason,
            userId);

        await _stockRepository.AddMovementAsync(movement);

        await _stockRepository.SaveChangesAsync();
    }

    public async Task DecreaseAsync(
        int productId,
        decimal quantity,
        StockMovementType type,
        string referenceType,
        string referenceId,
        string reason,
        int userId)
    {
        ValidateInput(productId, quantity, userId);

        if (!await _stockRepository.ProductExistsAsync(productId))
        {
            throw new KeyNotFoundException(
                $"Product with ID {productId} was not found.");
        }

        var balance = await _stockRepository.GetBalanceAsync(productId);

        if (balance is null)
        {
            throw new InvalidOperationException(
                $"Stock balance was not found for product {productId}.");
        }

        balance.Decrease(quantity);

        var movement = new StockMovement(
            productId,
            type,
            -quantity,
            referenceType,
            referenceId,
            reason,
            userId);

        await _stockRepository.AddMovementAsync(movement);

        await _stockRepository.SaveChangesAsync();
    }

    private static void ValidateInput(
        int productId,
        decimal quantity,
        int userId)
    {
        if (productId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(productId));
        }

        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Quantity must be greater than zero.");
        }

        if (decimal.Round(quantity, 3) != quantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Quantity cannot have more than 3 decimal places.");
        }
    }
}
