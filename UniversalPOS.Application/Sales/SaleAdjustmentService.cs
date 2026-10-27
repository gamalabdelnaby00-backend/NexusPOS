using UniversalPOS.Application.Abstractions;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.Common;
using UniversalPOS.Application.Security;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Application.Sales;

public enum DiscountKind { Percent, Amount }

/// <summary>Invoice-level discount, configured tax snapshot and customer phone for a draft sale.</summary>
public sealed class SaleAdjustmentService
{
    private readonly ISaleRepository _sales;
    private readonly ISystemSettingsRepository _settings;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CurrentUserSession _session;
    private readonly PermissionChecker _permissions;

    public SaleAdjustmentService(ISaleRepository sales, ISystemSettingsRepository settings,
        IAuditLogRepository audit, IUnitOfWork unitOfWork, CurrentUserSession session, PermissionChecker permissions)
    {
        _sales = sales; _settings = settings; _audit = audit; _unitOfWork = unitOfWork;
        _session = session; _permissions = permissions;
    }

    /// <summary>
    /// Applies a discount. Up to the cashier limit (from settings) needs Sales.ApplyDiscount;
    /// anything larger needs Sales.ApplyHighDiscount (Admin, Manager, Supervisor). Pass 0 to remove it.
    /// </summary>
    public async Task ApplyDiscountAsync(int saleId, DiscountKind kind, decimal value)
    {
        int userId = RequireUser();
        if (value < 0m) throw new ArgumentOutOfRangeException(nameof(value));

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var sale = await LoadOwnDraftAsync(saleId, userId);
            var settings = await _settings.GetAsync();

            decimal subtotal = sale.Subtotal;
            decimal amount = kind == DiscountKind.Percent
                ? decimal.Round(subtotal * value / 100m, 2, MidpointRounding.AwayFromZero)
                : value;

            if (kind == DiscountKind.Percent && value > 100m)
                throw new ArgumentOutOfRangeException(nameof(value), "Discount percent cannot exceed 100.");

            if (amount > 0m)
            {
                decimal percent = subtotal == 0m ? 0m : amount / subtotal * 100m;
                if (percent > settings.MaxCashierDiscountPercent)
                    Demand(PermissionCodes.Sales.ApplyHighDiscount);
                else
                    Demand(PermissionCodes.Sales.ApplyDiscount);
            }

            sale.ApplyDiscount(amount, userId);

            await _audit.AddAsync(new AuditLog(userId, "Sale.Discount", nameof(Sale), sale.Id.ToString(),
                $"amount={amount}; subtotal={subtotal}; limitPercent={settings.MaxCashierDiscountPercent}", null));

            await _sales.UpdateAsync(sale);
        });
    }

    /// <summary>Copies the current tax setting onto the sale (or clears it when tax is disabled).</summary>
    public async Task ApplyConfiguredTaxAsync(int saleId)
    {
        int userId = RequireUser();
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var sale = await LoadOwnDraftAsync(saleId, userId);
            var s = await _settings.GetAsync();

            if (s.TaxEnabled) sale.ApplyTax(s.TaxName, s.TaxRate, s.TaxIncluded);
            else sale.ClearTax();

            await _sales.UpdateAsync(sale);
        });
    }

    public async Task SetCustomerPhoneAsync(int saleId, string? phone)
    {
        int userId = RequireUser();
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var sale = await LoadOwnDraftAsync(saleId, userId);
            sale.SetCustomerPhone(phone);
            await _sales.UpdateAsync(sale);
        });
    }

    private int RequireUser()
    {
        if (!_session.IsAuthenticated || _session.UserId is null)
            throw new InvalidOperationException("A signed-in user is required.");
        return _session.UserId.Value;
    }

    private void Demand(string code)
    {
        if (!_permissions.HasPermission(code))
            throw ServiceErrors.Create(ServiceErrors.PermissionDenied, $"Permission denied: {code}");
    }

    private async Task<Sale> LoadOwnDraftAsync(int saleId, int userId)
    {
        var sale = await _sales.GetByIdAsync(saleId)
            ?? throw new InvalidOperationException("Sale not found.");
        if (sale.UserId != userId)
            throw ServiceErrors.Create(ServiceErrors.ShiftNotOwned, "This sale belongs to another user.");
        if (sale.Status != SaleReturnStatus.Draft)
            throw new InvalidOperationException("Only draft sales can be adjusted.");
        return sale;
    }
}
