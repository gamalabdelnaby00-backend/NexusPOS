using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Application.Authentication;

/// <summary>
/// Reconciles the in-memory session with the database so a sale is never attached to a stale,
/// closed, or another user's shift. It restores an existing open shift of the signed-in user
/// and never creates one.
/// </summary>
public sealed class ActiveShiftService
{
    private readonly CurrentUserSession _session;
    private readonly ICashShiftRepository _shifts;

    public ActiveShiftService(CurrentUserSession session, ICashShiftRepository shifts)
    {
        _session = session;
        _shifts = shifts;
    }

    /// <summary>Returns the verified active shift id, or null when the user has no open shift.</summary>
    public async Task<int?> RestoreAsync()
    {
        if (!_session.IsAuthenticated || _session.UserId is null)
            return null;

        int userId = _session.UserId.Value;

        if (_session.CashShiftId is int sessionShiftId)
        {
            var current = await _shifts.GetByIdAsync(sessionShiftId);
            if (current is not null && current.UserId == userId &&
                current.Status == CashShiftStatus.Open)
            {
                _session.SetActiveShift(current.Id, current.TerminalId);
                return current.Id;
            }

            // Closed, missing, or owned by someone else: drop it.
            _session.ClearActiveShift();
        }

        var open = await _shifts.GetOpenShiftForUserAsync(userId);
        if (open is null)
            return null;

        _session.SetActiveShift(open.Id, open.TerminalId);
        return open.Id;
    }
}
