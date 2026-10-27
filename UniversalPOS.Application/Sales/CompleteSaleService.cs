
using UniversalPOS.Application.Abstractions;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Application.Common;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Application.Sales;

public class CompleteSaleService
{
    private readonly ISaleRepository _saleRepository;
    private readonly IStockRepository _stockRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICashShiftRepository? _shiftRepository;

    public CompleteSaleService(
        ISaleRepository saleRepository,
        IStockRepository stockRepository,
        IUnitOfWork unitOfWork,
        ICashShiftRepository? shiftRepository = null)
    {
        _saleRepository = saleRepository;
        _stockRepository = stockRepository;
        _unitOfWork = unitOfWork;
        _shiftRepository = shiftRepository;
    }

    public async Task ExecuteAsync(int saleId)
    {
        if (saleId <= 0)
            throw new ArgumentOutOfRangeException(nameof(saleId));

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // 1. Load sale.
            var sale = await _saleRepository.GetByIdAsync(saleId);

            if (sale is null)
                throw new InvalidOperationException("Sale not found.");

            // 2. Validate the sale before changing anything.
            if (sale.Status != SaleReturnStatus.Draft)
                throw new InvalidOperationException(
                    "Only draft sales can be completed.");

            if (sale.UserId is null or <= 0)
                throw new InvalidOperationException(
                    "Sale must be assigned to a valid user before completion.");

            if (!sale.Items.Any())
                throw new InvalidOperationException(
                    "Cannot complete a sale without items.");

            if (sale.PaidAmount < sale.TotalAmount)
                throw new InvalidOperationException(
                    "Sale must be fully paid before completion.");

            // 3. Aggregate quantities for duplicate product items.
            var requiredQuantities = sale.Items
                .GroupBy(item => item.ProductId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(item => item.Quantity));

            // 4. Load and validate ALL stock balances first.
            var balances = new Dictionary<int, StockBalance>();

            foreach (var requirement in requiredQuantities)
            {
                var productId = requirement.Key;
                var requiredQuantity = requirement.Value;

                var balance = await _stockRepository
                    .GetBalanceAsync(productId);

                if (balance is null)
                    throw new InvalidOperationException(
                        $"Stock balance was not found for product {productId}.");

                if (balance.Quantity < requiredQuantity)
                    throw ServiceErrors.Create(ServiceErrors.InsufficientStock,
                        $"Insufficient stock for product {productId}. " +
                        $"Available: {balance.Quantity}, " +
                        $"Required: {requiredQuantity}.");

                balances.Add(productId, balance);
            }

            // 4b. Cash received must be recorded against an open shift owned by the seller.
            decimal cashReceived = sale.Payments
                .Where(p => p.Method == PaymentMethod.Cash)
                .Sum(p => p.Amount);

            CashShift? cashShift = null;
            // Sales made without a shift (Admin) do not touch any drawer.
            if (cashReceived > 0m && _shiftRepository is not null && sale.CashShiftId is not null)
            {
                cashShift = await _shiftRepository.GetByIdAsync(sale.CashShiftId.Value);

                if (cashShift is null || cashShift.Status != CashShiftStatus.Open)
                    throw ServiceErrors.Create(ServiceErrors.ShiftNotOpen,
                        "The cash shift is not open.");

                if (cashShift.UserId != sale.UserId)
                    throw ServiceErrors.Create(ServiceErrors.ShiftNotOwned,
                        "The cash shift belongs to a different user.");
            }

            // 5. Complete only after all validations succeed.
            sale.Complete();

            // 6. Deduct stock once per product and record movements.
            foreach (var requirement in requiredQuantities)
            {
                var productId = requirement.Key;
                var quantity = requirement.Value;
                var balance = balances[productId];

                balance.Decrease(quantity);

                await _stockRepository.UpdateBalanceAsync(balance);

                var movement = new StockMovement(
                    productId,
                    StockMovementType.Sale,
                    -quantity,
                    "Sale",
                    sale.Id.ToString(),
                    "Stock deducted for completed sale",
                    sale.UserId.Value);

                await _stockRepository.AddMovementAsync(movement);
            }

            // 6b. Record the cash that entered the drawer, in the same transaction as the sale.
            if (cashShift is not null)
            {
                await _shiftRepository!.AddCashMovementAsync(new CashMovement(
                    cashShift.Id,
                    CashMovementType.CashSale,
                    cashReceived,
                    increasesDrawer: true,
                    reason: $"Cash sale #{sale.Id}",
                    createdByUserId: sale.UserId.Value));
            }

            // 7. Persist the completed sale.
            await _saleRepository.UpdateAsync(sale);
        });
    }
}
