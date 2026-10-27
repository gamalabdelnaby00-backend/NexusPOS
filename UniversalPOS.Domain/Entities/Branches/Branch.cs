namespace UniversalPOS.Domain.Entities.Branches;

/// <summary>A physical operating branch inside the shared central database.</summary>
public sealed class Branch
{
    private Branch() { } // EF Core

    public int Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>Null means the branch inherits the centrally configured tax rate.</summary>
    public decimal? TaxRateOverride { get; private set; }

    public Branch(string code, string name, string? address = null, string? phone = null)
    {
        SetIdentity(code, name);
        Address = NormalizeOptional(address);
        Phone = NormalizeOptional(phone);
    }

    public void UpdateDetails(string name, string? address, string? phone)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Branch name is required.", nameof(name));
        Name = name.Trim();
        Address = NormalizeOptional(address);
        Phone = NormalizeOptional(phone);
    }

    public void SetTaxRateOverride(decimal? rate)
    {
        if (rate is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(rate), "Tax rate must be between 0 and 100.");
        TaxRateOverride = rate;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    private void SetIdentity(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Branch code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Branch name is required.", nameof(name));
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
