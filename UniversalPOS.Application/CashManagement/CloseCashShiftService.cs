using UniversalPOS.Application.Abstractions;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.Common;
using UniversalPOS.Application.Security;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Application.CashManagement;

public sealed record CloseShiftResult(
    int ShiftId,
    decimal OpeningFloat,
    decimal ExpectedCash,
    decimal CountedCash,
    decimal Difference,
    CashShiftStatus Status);

/// <summary>
/// Closes a cash shift. Expected cash = opening float + cash sales - cash refunds (+/- other drawer movements).
/// A non-zero difference leaves the shift PendingApproval until an authorized user approves it.
/// </summary>
public sealed class CloseCashShiftService
{
    private readonly ICashShiftRepository _shifts;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CurrentUserSession _session;
    private readonly PermissionChecker _permissions;

    public CloseCashShiftService(
        ICashShiftRepository shifts,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        CurrentUserSession session,
        PermissionChecker permissions)
    {
        _shifts = shifts;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _session = session;
        _permissions = permissions;
    }

    /// <summary>Preview of the cash the drawer should contain right now.</summary>
    public async Task<decimal> GetExpectedCashAsync(int shiftId)
    {
        var shift = await _shifts.GetByIdAsync(shiftId)
            ?? throw new InvalidOperationException("Shift was not found.");
        DemandAccess(shift);
        return shift.OpeningFloat + await _shifts.GetNetCashMovementsAsync(shiftId);
    }

    public async Task<CloseShiftResult> ExecuteAsync(int shiftId, decimal countedCash, string? notes = null)
    {
        if (!_session.IsAuthenticated || _session.UserId is null)
            throw new InvalidOperationException("A signed-in user is required.");
        if (shiftId <= 0)
            throw new ArgumentOutOfRangeException(nameof(shiftId));
        if (countedCash < 0m || decimal.Round(countedCash, 2) != countedCash)
            throw new ArgumentOutOfRangeException(nameof(countedCash),
                "Counted cash must be zero or greater with at most 2 decimal places.");

        int actorId = _session.UserId.Value;
        CloseShiftResult? result = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var shift = await _shifts.GetByIdAsync(shiftId)
                ?? throw new InvalidOperationException("Shift was not found.");

            DemandAccess(shift);

            if (shift.Status != CashShiftStatus.Open)
                throw ServiceErrors.Create(ServiceErrors.ShiftNotOpen, "Only open shifts can be closed.");

            decimal expected = shift.OpeningFloat + await _shifts.GetNetCashMovementsAsync(shiftId);
            if (expected < 0m)
                throw new InvalidOperationException("Expected cash cannot be negative; review the shift movements.");

            shift.RequestClose(expected, countedCash, notes);

            await _audit.AddAsync(new AuditLog(
                actorId,
                "Shift.Close",
                nameof(CashShift),
                shift.Id.ToString(),
                $"owner={shift.UserId}; expected={expected}; counted={countedCash}; difference={shift.Difference}; status={shift.Status}",
                null));

            result = new CloseShiftResult(shift.Id, shift.OpeningFloat, expected, countedCash,
                shift.Difference ?? 0m, shift.Status);
        });

        // The owner's session no longer has a usable shift once it is closed or pending approval.
        if (result is not null && _session.CashShiftId == shiftId)
            _session.ClearActiveShift();

        return result!;
    }

    private void DemandAccess(CashShift shift)
    {
        int actorId = _session.UserId ?? throw new InvalidOperationException("A signed-in user is required.");

        if (shift.UserId == actorId)
        {
            _permissions.DemandPermission(PermissionCodes.Shifts.Close);
            return;
        }

        // Closing or viewing another user's shift needs an explicit permission.
        _permissions.DemandPermission(PermissionCodes.Shifts.Reconcile);
    }
}
