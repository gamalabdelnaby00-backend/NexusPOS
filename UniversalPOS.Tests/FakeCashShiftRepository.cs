using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Tests;

public sealed class FakeCashShiftRepository : ICashShiftRepository
{
    private readonly Dictionary<int, CashShift> _shifts = new();
    private readonly List<CashMovement> _movements = new();
    private int _nextId = 1;

    public IReadOnlyList<CashMovement> Movements => _movements;

    /// <summary>Adds a shift and assigns the next id (the real database does this on insert).</summary>
    public CashShift Seed(CashShift shift)
    {
        typeof(CashShift).GetProperty(nameof(CashShift.Id))!.SetValue(shift, _nextId++);
        _shifts[shift.Id] = shift;
        return shift;
    }

    public Task<CashShift?> GetOpenShiftAsync(int userId, int terminalId) =>
        Task.FromResult(_shifts.Values.FirstOrDefault(s =>
            s.UserId == userId && s.TerminalId == terminalId && s.Status == CashShiftStatus.Open));

    public Task<bool> HasActiveShiftForUserAsync(int userId) =>
        Task.FromResult(_shifts.Values.Any(s => s.UserId == userId && s.Status == CashShiftStatus.Open));

    public Task<bool> HasActiveShiftForTerminalAsync(int terminalId) =>
        Task.FromResult(_shifts.Values.Any(s => s.TerminalId == terminalId && s.Status == CashShiftStatus.Open));

    public Task AddAsync(CashShift cashShift)
    {
        Seed(cashShift);
        return Task.CompletedTask;
    }

    public Task<CashShift?> GetByIdAsync(int shiftId)
    {
        _shifts.TryGetValue(shiftId, out var shift);
        return Task.FromResult(shift);
    }

    public Task<CashShift?> GetOpenShiftForUserAsync(int userId) =>
        Task.FromResult(_shifts.Values.FirstOrDefault(s => s.UserId == userId && s.Status == CashShiftStatus.Open));

    public Task AddCashMovementAsync(CashMovement movement)
    {
        _movements.Add(movement);
        return Task.CompletedTask;
    }

    public Task<decimal> GetNetCashMovementsAsync(int shiftId)
    {
        decimal net = _movements
            .Where(m => m.CashShiftId == shiftId)
            .Sum(m => m.IncreasesDrawer ? m.Amount : -m.Amount);
        return Task.FromResult(net);
    }

    public Task SaveChangesAsync() => Task.CompletedTask;
}
