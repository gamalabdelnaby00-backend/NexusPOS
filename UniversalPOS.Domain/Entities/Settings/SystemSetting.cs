namespace UniversalPOS.Domain.Entities.Settings;

/// <summary>Central application settings (a single row). Tax is off until an Admin or Manager enables it.</summary>
public sealed class SystemSetting
{
    private SystemSetting() { } // EF Core

    public static SystemSetting CreateDefault() => new();

    public int Id { get; private set; }

    public bool TaxEnabled { get; private set; }
    public string TaxName { get; private set; } = "Tax";
    public decimal TaxRate { get; private set; }
    /// <summary>True: prices already include tax (the tax is extracted). False: tax is added on top of the invoice.</summary>
    public bool TaxIncluded { get; private set; } = true;

    /// <summary>Largest invoice discount (percent of subtotal) a cashier may give alone.</summary>
    public decimal MaxCashierDiscountPercent { get; private set; } = 10m;

    public string DefaultLanguage { get; private set; } = "ar-EG";
    public string DefaultTheme { get; private set; } = "Light";
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; private set; }

    public void ConfigureTax(bool enabled, string name, decimal rate, bool included, int? updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tax name is required.", nameof(name));
        if (rate is < 0m or > 100m) throw new ArgumentOutOfRangeException(nameof(rate));
        if (updatedByUserId is <= 0) throw new ArgumentOutOfRangeException(nameof(updatedByUserId));
        TaxEnabled = enabled;
        TaxName = name.Trim();
        TaxRate = rate;
        TaxIncluded = included;
        Touch(updatedByUserId);
    }

    public void ConfigureDiscountLimit(decimal maxCashierPercent, int? updatedByUserId)
    {
        if (maxCashierPercent is < 0m or > 100m) throw new ArgumentOutOfRangeException(nameof(maxCashierPercent));
        if (updatedByUserId is <= 0) throw new ArgumentOutOfRangeException(nameof(updatedByUserId));
        MaxCashierDiscountPercent = maxCashierPercent;
        Touch(updatedByUserId);
    }

    public void ConfigureAppearance(string language, string theme, int? updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(language)) throw new ArgumentException("Language is required.", nameof(language));
        if (string.IsNullOrWhiteSpace(theme)) throw new ArgumentException("Theme is required.", nameof(theme));
        if (updatedByUserId is <= 0) throw new ArgumentOutOfRangeException(nameof(updatedByUserId));
        DefaultLanguage = language.Trim();
        DefaultTheme = theme.Trim();
        Touch(updatedByUserId);
    }

    private void Touch(int? userId)
    {
        UpdatedByUserId = userId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
