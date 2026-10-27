
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Infrastructure.Repositories;

public class CashShiftRepository : ICashShiftRepository
{
    private readonly AppDbContext _dbContext;

    public CashShiftRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> HasActiveShiftForUserAsync(int userId)
    {
        return await _dbContext.CashShifts
            .AnyAsync(s =>
                s.UserId == userId &&
                s.Status == CashShiftStatus.Open);
    }

    public async Task<bool> HasActiveShiftForTerminalAsync(int terminalId)
    {
        return await _dbContext.CashShifts
            .AnyAsync(s =>
                s.TerminalId == terminalId &&
                s.Status == CashShiftStatus.Open);
    }

    public async Task AddAsync(CashShift cashShift)
    {
        await _dbContext.CashShifts.AddAsync(cashShift);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<CashShift?> GetOpenShiftAsync(int userId, int terminalId)
    {
        return await _dbContext.CashShifts
            .FirstOrDefaultAsync(shift =>
                shift.UserId == userId &&
                shift.TerminalId == terminalId &&
                shift.Status == CashShiftStatus.Open);
    }


    public async Task<CashShift?> GetByIdAsync(int shiftId)
    {
        return await _dbContext.CashShifts
            .FirstOrDefaultAsync(shift => shift.Id == shiftId);
    }

    public async Task<CashShift?> GetOpenShiftForUserAsync(int userId)
    {
        return await _dbContext.CashShifts
            .FirstOrDefaultAsync(shift =>
                shift.UserId == userId &&
                shift.Status == CashShiftStatus.Open);
    }

    public async Task AddCashMovementAsync(CashMovement movement)
    {
        ArgumentNullException.ThrowIfNull(movement);
        await _dbContext.CashMovements.AddAsync(movement);
    }

    public async Task<decimal> GetNetCashMovementsAsync(int shiftId)
    {
        // Persisted movements only; callers must not have unsaved movements for this shift.
        decimal increases = await _dbContext.CashMovements
            .Where(m => m.CashShiftId == shiftId && m.IncreasesDrawer)
            .SumAsync(m => (decimal?)m.Amount) ?? 0m;

        decimal decreases = await _dbContext.CashMovements
            .Where(m => m.CashShiftId == shiftId && !m.IncreasesDrawer)
            .SumAsync(m => (decimal?)m.Amount) ?? 0m;

        return increases - decreases;
    }

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}
