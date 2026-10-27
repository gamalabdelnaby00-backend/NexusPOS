
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Infrastructure.Repositories;

public class PosTerminalRepository : IPosTerminalRepository
{
    private readonly AppDbContext _dbContext;

    public PosTerminalRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PosTerminal?> GetByIdAsync(int terminalId)
    {
        return await _dbContext.PosTerminals
            .FirstOrDefaultAsync(t => t.Id == terminalId);
    }
    public async Task<PosTerminal?> GetByCodeAsync(string code)
    {
        return await _dbContext.PosTerminals
            .FirstOrDefaultAsync(t => t.Code == code);
    }
}
