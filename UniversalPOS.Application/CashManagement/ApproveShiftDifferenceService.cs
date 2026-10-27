using UniversalPOS.Application.Abstractions;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.Security;
using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Application.CashManagement;

/// <summary>Approves the cash difference of a shift that closed with a surplus or shortage.</summary>
public sealed class ApproveShiftDifferenceService
{
    private readonly ICashShiftRepository _shifts;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CurrentUserSession _session;
    private readonly PermissionChecker _permissions;

    public ApproveShiftDifferenceService(
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

    public async Task ExecuteAsync(int shiftId)
    {
        if (!_session.IsAuthenticated || _session.UserId is null)
            throw new InvalidOperationException("A signed-in user is required.");

        _permissions.DemandPermission(PermissionCodes.Shifts.ApproveDifference);
        int approverId = _session.UserId.Value;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var shift = await _shifts.GetByIdAsync(shiftId)
                ?? throw new InvalidOperationException("Shift was not found.");

            shift.ApproveDifference(approverId);

            await _audit.AddAsync(new AuditLog(
                approverId,
                "Shift.ApproveDifference",
                nameof(CashShift),
                shift.Id.ToString(),
                $"owner={shift.UserId}; difference={shift.Difference}",
                null));
        });
    }
}
