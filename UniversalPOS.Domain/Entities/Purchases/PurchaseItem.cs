namespace UniversalPOS.Domain.Entities;

public class PurchaseItem
{
    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal LineTotal => UnitCost * Quantity;

    private PurchaseItem() { }

    public PurchaseItem(int productId, decimal unitCost, decimal quantity)
    {
        if (productId <= 0) throw new ArgumentOutOfRangeException(nameof(productId));
        if (unitCost < 0) throw new ArgumentOutOfRangeException(nameof(unitCost));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        ProductId = productId;
        UnitCost = unitCost;
        Quantity = quantity;
    }
}
