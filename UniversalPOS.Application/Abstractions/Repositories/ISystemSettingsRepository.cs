using UniversalPOS.Domain.Entities.Settings;

namespace UniversalPOS.Application.Abstractions.Repositories;

public interface ISystemSettingsRepository
{
    /// <summary>Returns the single settings row, creating the default one if missing.</summary>
    Task<SystemSetting> GetAsync();
    Task SaveChangesAsync();
}
