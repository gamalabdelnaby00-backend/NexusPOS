using UniversalPOS.Application.Abstractions;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.Security;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Application.CashManagement;

/// <summary>Registers a documented cash-in or cash-out movement against the signed-in user's open shift.</summary>
public sealed class RegisterCashMovementService
{
    private readonly ICashShiftRepository _shifts;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CurrentUserSession _session;
    private readonly PermissionChecker _permissions;

    public RegisterCashMovementService(
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

    public async Task<int> ExecuteAsync(
        int shiftId,
        CashMovementType type,
        decimal amount,
        string reason,
        bool increasesDrawer)
    {
        if (!_session.IsAuthenticated || _session.UserId is null)
            throw new UnauthorizedAccessException("A signed-in user is required.");
        if (shiftId <= 0)
            throw new ArgumentOutOfRangeException(nameof(shiftId));
        if (amount <= 0m || decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive with at most two decimal places.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required for every manual cash movement.", nameof(reason));
        if (type is not (CashMovementType.CashIn or CashMovementType.CashOut or CashMovementType.Expense or CashMovementType.SafeDrop or CashMovementType.Correction))
            throw new ArgumentException("Only manual cash movement types can be registered here.", nameof(type));
        if ((type == CashMovementType.CashIn) != increasesDrawer && type != CashMovementType.Correction)
            throw new ArgumentException("CashIn must increase the drawer and cash-out/expense/safe-drop must decrease it.", nameof(increasesDrawer));

        int actorId = _session.UserId.Value;
        int movementId = 0;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var shift = await _shifts.GetByIdAsync(shiftId)
                ?? throw new InvalidOperationException("Shift was not found.");

            if (shift.UserId != actorId)
            {
                _permissions.DemandPermission(PermissionCodes.CashMovements.ViewAll);
            }

            if (shift.Status != CashShiftStatus.Open)
                throw new InvalidOperationException("Cash movements can only be added to an open shift.");

            _permissions.DemandPermission(increasesDrawer
                ? PermissionCodes.CashMovements.AddIn
                : PermissionCodes.CashMovements.AddOut);

            var movement = new CashMovement(
                shift.Id,
                type,
                amount,
                increasesDrawer,
                reason.Trim(),
                actorId);

            await _shifts.AddCashMovementAsync(movement);
            await _audit.AddAsync(new AuditLog(
                actorId,
                "CashMovement.Register",
                nameof(CashMovement),
                shift.Id.ToString(),
                $"type={type}; amount={amount}; increasesDrawer={increasesDrawer}; owner={shift.UserId}; reason={reason.Trim()}",
                null));
            await _shifts.SaveChangesAsync();
            movementId = movement.Id;
        });

        return movementId;
    }
}
