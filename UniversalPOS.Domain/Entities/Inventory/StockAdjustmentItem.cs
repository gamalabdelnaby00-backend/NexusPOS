namespace UniversalPOS.Domain.Entities;

public class StockAdjustmentItem
{
    public int Id { get; private set; }
    public int StockAdjustmentId { get; private set; }
    public int ProductId { get; private set; }
    public decimal SystemQuantity { get; private set; }
    public decimal CountedQuantity { get; private set; }
    public decimal Difference => CountedQuantity - SystemQuantity;
    private StockAdjustmentItem() { }
    public StockAdjustmentItem(int productId, decimal systemQuantity, decimal countedQuantity)
    {
        if (productId <= 0) throw new ArgumentOutOfRangeException(nameof(productId));
        if (systemQuantity < 0) throw new ArgumentOutOfRangeException(nameof(systemQuantity));
        if (countedQuantity < 0) throw new ArgumentOutOfRangeException(nameof(countedQuantity));
        ProductId = productId; SystemQuantity = systemQuantity; CountedQuantity = countedQuantity;
    }
}
