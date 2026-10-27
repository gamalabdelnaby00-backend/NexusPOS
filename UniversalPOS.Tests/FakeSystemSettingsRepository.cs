using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Entities.Settings;

namespace UniversalPOS.Tests;

public sealed class FakeSystemSettingsRepository : ISystemSettingsRepository
{
    public SystemSetting Settings { get; } = SystemSetting.CreateDefault();

    public Task<SystemSetting> GetAsync() => Task.FromResult(Settings);

    public Task SaveChangesAsync() => Task.CompletedTask;
}
