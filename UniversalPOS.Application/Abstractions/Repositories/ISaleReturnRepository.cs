
using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Application.Abstractions.Repositories;

public interface ISaleReturnRepository
{
    Task<decimal> GetPreviouslyReturnedQuantityAsync(int saleItemId);

    Task<decimal> GetPreviouslyRefundedAmountAsync(int saleId);

    Task<CashShift?> GetCashShiftAsync(int cashShiftId);

    Task AddAsync(SaleReturn saleReturn);

    Task AddPaymentAsync(SaleReturnPayment payment);

    Task AddCashMovementAsync(CashMovement movement);

    Task SaveChangesAsync();
}
