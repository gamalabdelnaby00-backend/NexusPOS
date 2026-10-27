
using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Application.Abstractions.Repositories;

public interface IPosTerminalRepository
{
    Task<PosTerminal?> GetByIdAsync(int terminalId);
    Task<PosTerminal?> GetByCodeAsync(string code);
}
