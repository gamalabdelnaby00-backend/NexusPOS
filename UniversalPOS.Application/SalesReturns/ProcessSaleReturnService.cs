using UniversalPOS.Application.Abstractions;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Application.SalesReturns;

public sealed class ProcessSaleReturnService
{
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleReturnRepository _returnRepository;
    private readonly IStockRepository _stockRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CurrentUserSession _currentUser;
    public ProcessSaleReturnService(ISaleRepository saleRepository,ISaleReturnRepository returnRepository,IStockRepository stockRepository,IUnitOfWork unitOfWork,CurrentUserSession currentUser)
    {
        _saleRepository = saleRepository;
        _returnRepository = returnRepository;
        _stockRepository = stockRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<int> ExecuteAsync(int saleId,string reason,IReadOnlyCollection<SaleReturnItemRequest> items,PaymentMethod refundMethod,string? externalReference = null)
    {
        if (!_currentUser.UserId.HasValue)
            throw new InvalidOperationException("A signed-in user is required.");

        int userId = _currentUser.UserId.Value;

        if (saleId <= 0)
            throw new ArgumentOutOfRangeException(nameof(saleId));

        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
            throw new ArgumentException("At least one return item is required.",nameof(items));

        if (!Enum.IsDefined(refundMethod))
            throw new ArgumentException("Invalid refund method.",nameof(refundMethod));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Return reason is required.", nameof(reason));

        if (externalReference?.Length > 200)
            throw new ArgumentException("External refund reference cannot exceed 200 characters.", nameof(externalReference));

        if (items.Any(x => x is null))
            throw new ArgumentException("Return items cannot contain null values.",nameof(items));

        if (items.GroupBy(x => x.SaleItemId).Any(g => g.Count() > 1))
            throw new InvalidOperationException("The same sale item cannot be added twice.");

        int? cashShiftId = null;

        if (refundMethod == PaymentMethod.Cash)
        {
            if (!_currentUser.CashShiftId.HasValue)
                throw new InvalidOperationException("An active cash shift is required for cash refunds.");
            cashShiftId = _currentUser.CashShiftId.Value;
        }

        int returnId = 0;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var sale = await _saleRepository.GetByIdAsync(saleId);

            if (sale is null)
                throw new InvalidOperationException("Sale was not found.");

            if (sale.Status != SaleReturnStatus.Completed)
                throw new InvalidOperationException("Only completed sales can be returned.");

            // Validate the cash shift inside the same serializable transaction as the
            // refund, inventory restock, and cash-drawer movement. Checking before the
            // transaction allowed a shift to be closed between validation and refund.
            if (cashShiftId.HasValue)
            {
                var shift = await _returnRepository.GetCashShiftAsync(cashShiftId.Value);
                if (shift is null || shift.Status != CashShiftStatus.Open || shift.UserId != userId)
                    throw new InvalidOperationException("The current user's cash shift is not open.");
            }

            var saleItems = sale.Items.ToDictionary(x => x.Id);

            var saleReturn = new SaleReturn(saleId,userId,reason);

            foreach (var request in items)
            {
                if (request.SaleItemId <= 0)
                    throw new ArgumentOutOfRangeException(nameof(request.SaleItemId));

                if (request.Quantity <= 0 || decimal.Round(request.Quantity, 3) != request.Quantity)
                {
                    throw new ArgumentOutOfRangeException(nameof(request.Quantity),"Return quantity must be positive and have at most 3 decimal places.");
                }

                if (!saleItems.TryGetValue(request.SaleItemId, out var saleItem))
                {
                    throw new InvalidOperationException($"Sale item {request.SaleItemId} does not belong to sale {saleId}.");
                }

                decimal previouslyReturned = await _returnRepository.GetPreviouslyReturnedQuantityAsync(saleItem.Id);

                decimal remainingQuantity = saleItem.Quantity - previouslyReturned;

                if (request.Quantity > remainingQuantity)
                {
                    throw new InvalidOperationException($"Return quantity exceeds the remaining quantity for sale item {saleItem.Id}.");
                }

                var returnItem = new SaleReturnItem(saleItem.Id,saleItem.ProductId,
                    request.Quantity,saleItem.UnitPrice,request.IsRestockable);
                saleReturn.AddItem(returnItem);
            }

            decimal totalRefund = saleReturn.TotalRefund;

            if (totalRefund <= 0)
                throw new InvalidOperationException("The refund amount must be greater than zero.");

            decimal previouslyRefunded = await _returnRepository.GetPreviouslyRefundedAmountAsync(saleId);

            if (previouslyRefunded + totalRefund > sale.PaidAmount)
            {
                throw new InvalidOperationException("The total refunds cannot exceed the amount paid for the sale.");
            }

            // Save the return first to generate its database ID.
            // This remains inside the transaction.
            await _returnRepository.AddAsync(saleReturn);
            await _returnRepository.SaveChangesAsync();

            returnId = saleReturn.Id;

            foreach (var returnItem in saleReturn.Items)
            {
                if (!returnItem.IsRestockable)
                    continue;

                var balance = await _stockRepository.GetBalanceAsync(returnItem.ProductId);

                if (balance is null)
                {
                    balance = new StockBalance(returnItem.ProductId, 0m);
                    balance.Increase(returnItem.Quantity);
                    await _stockRepository.AddBalanceAsync(balance);
                }
                else
                {
                    balance.Increase(returnItem.Quantity);
                    await _stockRepository.UpdateBalanceAsync(balance);
                }
                await _stockRepository.AddMovementAsync(new StockMovement(returnItem.ProductId,StockMovementType.CustomerReturn,
                        returnItem.Quantity,"SaleReturn",saleReturn.Id.ToString(),"Stock returned from a customer sale return",userId));
            }

            // Complete the return before committing.
            saleReturn.Complete();

            var payment = new SaleReturnPayment(saleReturn.Id,userId,refundMethod,totalRefund,cashShiftId,
                originalPaymentId: null,externalReference: externalReference);
            await _returnRepository.AddPaymentAsync(payment);

            if (refundMethod == PaymentMethod.Cash)
            {
                var movement = new CashMovement(cashShiftId!.Value,CashMovementType.CashRefund,totalRefund,
                    increasesDrawer: false,reason: $"Refund for sale return #{saleReturn.Id}",createdByUserId: userId);
                await _returnRepository.AddCashMovementAsync(movement);
            }
            await _returnRepository.SaveChangesAsync();
            await _stockRepository.SaveChangesAsync();
        });

        return returnId;
    }
}