namespace UniversalPOS.Domain.Entities.Products.Translations;

/// <summary>Localized display text for a category. LanguageCode uses BCP 47 tags such as ar-EG and en-US.</summary>
public sealed class CategoryTranslation
{
    private CategoryTranslation() { } // EF Core

    public CategoryTranslation(int categoryId, string languageCode, string name)
    {
        if (categoryId <= 0) throw new ArgumentOutOfRangeException(nameof(categoryId));
        if (string.IsNullOrWhiteSpace(languageCode)) throw new ArgumentException("Language code is required.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Translated category name is required.", nameof(name));
        CategoryId = categoryId;
        LanguageCode = languageCode.Trim();
        Name = name.Trim();
    }

    public int CategoryId { get; private set; }
    public string LanguageCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Translated category name is required.", nameof(name));
        Name = name.Trim();
    }
}
