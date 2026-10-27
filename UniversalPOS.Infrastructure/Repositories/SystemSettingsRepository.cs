using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Entities.Settings;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Infrastructure.Repositories;

public sealed class SystemSettingsRepository : ISystemSettingsRepository
{
    private readonly AppDbContext _dbContext;

    public SystemSettingsRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SystemSetting> GetAsync()
    {
        var settings = await _dbContext.SystemSettings.OrderBy(s => s.Id).FirstOrDefaultAsync();
        if (settings is not null)
            return settings;

        settings = SystemSetting.CreateDefault();
        await _dbContext.SystemSettings.AddAsync(settings);
        await _dbContext.SaveChangesAsync();
        return settings;
    }

    public async Task SaveChangesAsync() => await _dbContext.SaveChangesAsync();
}
