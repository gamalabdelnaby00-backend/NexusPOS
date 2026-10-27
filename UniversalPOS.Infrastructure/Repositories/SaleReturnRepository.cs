using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Infrastructure.Repositories;

public sealed class SaleReturnRepository : ISaleReturnRepository
{
    private readonly AppDbContext _context;

    public SaleReturnRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> GetPreviouslyReturnedQuantityAsync(
        int saleItemId)
    {
        return await _context.SaleReturns
            .Where(r => r.Status == SaleReturnStatus.Completed)
            .SelectMany(r => r.Items)
            .Where(i => i.SaleItemId == saleItemId)
            .SumAsync(i => (decimal?)i.Quantity) ?? 0m;
    }

    public async Task<decimal> GetPreviouslyRefundedAmountAsync(int saleId)
    {
        return await _context.SaleReturnPayments
            .Where(p => _context.SaleReturns.Any(r =>
                r.Id == p.SaleReturnId &&
                r.SaleId == saleId && r.Status == SaleReturnStatus.Completed)).SumAsync(p => (decimal?)p.Amount) ?? 0m;
    }

    public async Task<CashShift?> GetCashShiftAsync(int cashShiftId)
    {
        return await _context.CashShifts.FirstOrDefaultAsync(s => s.Id == cashShiftId);
    }

    public async Task AddAsync(SaleReturn saleReturn)
    {
        ArgumentNullException.ThrowIfNull(saleReturn);
        await _context.SaleReturns.AddAsync(saleReturn);
    }

    public async Task AddPaymentAsync(SaleReturnPayment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        await _context.SaleReturnPayments.AddAsync(payment);
    }

    public async Task AddCashMovementAsync(CashMovement movement)
    {
        ArgumentNullException.ThrowIfNull(movement);
        await _context.CashMovements.AddAsync(movement);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}