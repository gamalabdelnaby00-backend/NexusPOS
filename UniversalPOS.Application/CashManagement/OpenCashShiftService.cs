
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Application.CashManagement;

public class OpenCashShiftService
{
    private readonly IUserRepository _userRepository;
    private readonly IPosTerminalRepository _terminalRepository;
    private readonly ICashShiftRepository _cashShiftRepository;

    public OpenCashShiftService(
        IUserRepository userRepository,
        IPosTerminalRepository terminalRepository,
        ICashShiftRepository cashShiftRepository)
    {
        _userRepository = userRepository;
        _terminalRepository = terminalRepository;
        _cashShiftRepository = cashShiftRepository;
    }

    public async Task<int> ExecuteAsync(
        int userId,
        int terminalId,
        decimal openingFloat)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));

        if (terminalId <= 0)
            throw new ArgumentOutOfRangeException(nameof(terminalId));

        if (openingFloat < 0)
            throw new ArgumentOutOfRangeException(nameof(openingFloat));

        var user = await _userRepository.GetByIdAsync(userId);

        if (user is null || !user.IsActive)
            throw new InvalidOperationException(
                "User does not exist or is inactive.");

        var terminal =
            await _terminalRepository.GetByIdAsync(terminalId);

        if (terminal is null || !terminal.IsActive)
            throw new InvalidOperationException(
                "POS terminal does not exist or is inactive.");

        // Reuse the existing shift if this user already has one
        // open on this same terminal.
        var existingShift =
            await _cashShiftRepository.GetOpenShiftAsync(
                userId,
                terminalId);

        if (existingShift is not null)
            return existingShift.Id;

        // Do not allow the same user to open another terminal's shift.
        if (await _cashShiftRepository.HasActiveShiftForUserAsync(userId))
        {
            throw new InvalidOperationException(
                "You already have an open shift on another terminal. " +
                "Continue using that terminal or close its shift first.");
        }

        // Do not allow two cashiers to share the same terminal shift.
        if (await _cashShiftRepository.HasActiveShiftForTerminalAsync(terminalId))
        {
            throw new InvalidOperationException(
                "This terminal already has an active cash shift.");
        }

        var cashShift = new CashShift(
            userId,
            terminalId,
            openingFloat);

        await _cashShiftRepository.AddAsync(cashShift);

        return cashShift.Id;
    }
}
