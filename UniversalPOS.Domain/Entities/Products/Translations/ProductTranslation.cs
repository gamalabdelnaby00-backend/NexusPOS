namespace UniversalPOS.Domain.Entities.Products.Translations;

/// <summary>Localized display text for a product. LanguageCode uses BCP 47 tags such as ar-EG and en-US.</summary>
public sealed class ProductTranslation
{
    private ProductTranslation() { } // EF Core

    public ProductTranslation(int productId, string languageCode, string name, string? description = null)
    {
        if (productId <= 0) throw new ArgumentOutOfRangeException(nameof(productId));
        if (string.IsNullOrWhiteSpace(languageCode)) throw new ArgumentException("Language code is required.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Translated product name is required.", nameof(name));

        ProductId = productId;
        LanguageCode = languageCode.Trim();
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public int ProductId { get; private set; }
    public string LanguageCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Translated product name is required.", nameof(name));
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
