
namespace UniversalPOS.Application.Authentication;

public sealed class CurrentUserSession
{
    public int? UserId { get; private set; }
    public string? UserName { get; private set; }
    public string? DisplayName { get; private set; }

    public int? CashShiftId { get; private set; }
    public int? TerminalId { get; private set; }

    public IReadOnlyCollection<string> Roles { get; private set; }
        = Array.Empty<string>();
    public IReadOnlyCollection<string> Permissions { get; private set; }
    = Array.Empty<string>();
    public bool IsAuthenticated => UserId.HasValue;

    public void SignIn(
        int userId,
        string userName,
        string displayName,
        IEnumerable<string> roles,
        IEnumerable<string> permissions)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));

        UserId = userId;
        UserName = userName;
        DisplayName = displayName;

        Roles = roles.Distinct(
            StringComparer.OrdinalIgnoreCase).ToArray();

        Permissions = permissions.Distinct(
            StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public void SetActiveShift(int cashShiftId, int terminalId)
    {
        if (!IsAuthenticated)
            throw new InvalidOperationException(
                "A user must sign in before opening a shift.");

        if (cashShiftId <= 0 || terminalId <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(cashShiftId));

        CashShiftId = cashShiftId;
        TerminalId = terminalId;
    }

    public void ClearActiveShift()
    {
        CashShiftId = null;
        TerminalId = null;
    }

    public void SignOut()
    {
        UserId = null;
        UserName = null;
        DisplayName = null;
        CashShiftId = null;
        TerminalId = null;
        Roles = Array.Empty<string>();
        Permissions = Array.Empty<string>();
    }
}
