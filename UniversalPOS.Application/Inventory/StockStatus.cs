namespace UniversalPOS.Application.Inventory;

public enum StockLevel
{
    /// <summary>Zero units: not available for sale.</summary>
    OutOfStock,
    /// <summary>Fewer than 10 units: red, needs re-supply.</summary>
    Critical,
    /// <summary>10 to 20 units inclusive: orange, running low.</summary>
    Low,
    /// <summary>More than 20 units: green, healthy.</summary>
    Good
}

/// <summary>Single source of the stock thresholds so every screen shows the same status.</summary>
public static class StockStatus
{
    public const decimal GoodAbove = 20m;
    public const decimal LowFrom = 10m;

    public static StockLevel Classify(decimal quantity)
    {
        if (quantity <= 0m) return StockLevel.OutOfStock;
        if (quantity > GoodAbove) return StockLevel.Good;
        if (quantity >= LowFrom) return StockLevel.Low;
        return StockLevel.Critical;
    }

    public static bool CanSell(decimal quantity) => quantity > 0m;

    /// <summary>Arabic text so the status never relies on colour alone.</summary>
    public static string ArabicText(StockLevel level) => level switch
    {
        StockLevel.Good => "المخزون جيد",
        StockLevel.Low => "منخفض ويقترب من النفاد",
        StockLevel.Critical => "منخفض جدًا - يحتاج توريد",
        _ => "غير متاح"
    };

    public static string EnglishText(StockLevel level) => level switch
    {
        StockLevel.Good => "In stock",
        StockLevel.Low => "Low - running out",
        StockLevel.Critical => "Very low - reorder",
        _ => "Unavailable"
    };

    /// <summary>Hex colour for the indicator (green / orange / red / grey).</summary>
    public static string ColorHex(StockLevel level) => level switch
    {
        StockLevel.Good => "#2E9E5B",
        StockLevel.Low => "#F0932B",
        StockLevel.Critical => "#D63031",
        _ => "#7F8C8D"
    };
}
