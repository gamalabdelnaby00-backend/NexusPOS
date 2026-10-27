using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Application.Abstractions.Repositories;

public interface ICashShiftRepository
{
    Task<CashShift?> GetOpenShiftAsync(int userId, int terminalId);
    Task<bool> HasActiveShiftForUserAsync(int userId);
    Task<bool> HasActiveShiftForTerminalAsync(int terminalId);
    Task AddAsync(CashShift cashShift);

    Task<CashShift?> GetByIdAsync(int shiftId);

    /// <summary>The user's currently Open shift, if any (PendingApproval shifts are not sellable).</summary>
    Task<CashShift?> GetOpenShiftForUserAsync(int userId);

    /// <summary>Adds a cash movement to the current unit of work without saving.</summary>
    Task AddCashMovementAsync(CashMovement movement);

    /// <summary>Sum of drawer-increasing movements minus drawer-decreasing movements for the shift. Excludes the opening float.</summary>
    Task<decimal> GetNetCashMovementsAsync(int shiftId);

    Task SaveChangesAsync();
}
