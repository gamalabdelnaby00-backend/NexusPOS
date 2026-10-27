using UniversalPOS.Application.Abstractions;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.Security;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Entities.Settings;

namespace UniversalPOS.Application.Settings;

/// <summary>Tax and discount-limit settings. Only users with Settings.Manage (Admin, Manager) can change them.</summary>
public sealed class SettingsService
{
    private readonly ISystemSettingsRepository _settings;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CurrentUserSession _session;
    private readonly PermissionChecker _permissions;

    public SettingsService(ISystemSettingsRepository settings, IAuditLogRepository audit,
        IUnitOfWork unitOfWork, CurrentUserSession session, PermissionChecker permissions)
    {
        _settings = settings; _audit = audit; _unitOfWork = unitOfWork;
        _session = session; _permissions = permissions;
    }

    public Task<SystemSetting> GetAsync() => _settings.GetAsync();

    public async Task ConfigureTaxAsync(bool enabled, string name, decimal rate, bool included)
    {
        int userId = Demand();
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var s = await _settings.GetAsync();
            string before = $"enabled={s.TaxEnabled}; name={s.TaxName}; rate={s.TaxRate}; included={s.TaxIncluded}";
            s.ConfigureTax(enabled, name, rate, included, userId);
            await _audit.AddAsync(new AuditLog(userId, "Settings.Tax", nameof(SystemSetting), s.Id.ToString(),
                $"before[{before}] after[enabled={enabled}; name={name}; rate={rate}; included={included}]", null));
        });
    }

    public async Task ConfigureDiscountLimitAsync(decimal maxCashierPercent)
    {
        int userId = Demand();
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var s = await _settings.GetAsync();
            decimal before = s.MaxCashierDiscountPercent;
            s.ConfigureDiscountLimit(maxCashierPercent, userId);
            await _audit.AddAsync(new AuditLog(userId, "Settings.DiscountLimit", nameof(SystemSetting), s.Id.ToString(),
                $"before={before}; after={maxCashierPercent}", null));
        });
    }

    private int Demand()
    {
        if (!_session.IsAuthenticated || _session.UserId is null)
            throw new InvalidOperationException("A signed-in user is required.");
        _permissions.DemandPermission(PermissionCodes.Settings.Manage);
        return _session.UserId.Value;
    }
}
